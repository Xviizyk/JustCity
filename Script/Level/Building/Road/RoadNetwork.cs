using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace JustCity.Script.Level.Building;

public readonly record struct RoadSnap(Vector2 Position, RoadNode Node, RoadSegment Segment, Vector2 Direction)
{
	public bool IsFree => Node == null && Segment == null;
}

public sealed class RoadOp
{
	public List<RoadNode> Nodes = new();
	public List<RoadSegment> Added = new();
	public List<RoadSegment> Removed = new();
	public List<RoadSegment> Built = new();
	public List<RoadNode> RemovedNodes = new();
	public List<LinkChange> Links = new();
}

[GlobalClass]
public partial class RoadNetwork : Resource
{
	[Export] public Array<RoadNode> Nodes { get; set; } = new();
	[Export] public Array<RoadSegment> Segments { get; set; } = new();
	[Export] public int NextId { get; set; } = 1;

	public event Action TopologyChanged;

	private const float EndMargin = 6f;
	private readonly System.Collections.Generic.Dictionary<RoadSegment, Vector2[]> _polyCache = new();
	private RoadOp _rec;

	public Vector2[] PolyOf(RoadSegment s)
	{
		if (!_polyCache.TryGetValue(s, out Vector2[] pts))
		{
			pts = RoadPoly.Of(s.Curve);
			_polyCache[s] = pts;
		}
		return pts;
	}

	public RoadNode NodeById(int id)
	{
		foreach (RoadNode n in Nodes) if (n.Id == id) return n;
		return null;
	}

	public int Degree(RoadNode n) => Segments.Count(s => s.StartNodeId == n.Id || s.EndNodeId == n.Id);

	public Vector2 OutwardDirection(RoadNode n)
	{
		RoadSegment only = null;
		int count = 0;
		foreach (RoadSegment s in Segments)
		{
			if (s.StartNodeId == n.Id || s.EndNodeId == n.Id)
			{
				only = s;
				count++;
			}
		}
		if (count != 1) return Vector2.Zero;
		return only.StartNodeId == n.Id ? -only.StartTangent : only.EndTangent;
	}

	public RoadSnap Snap(Vector2 p, float radius, int level)
	{
		RoadNode bestNode = null;
		float bd = radius;
		foreach (RoadNode n in Nodes)
		{
			if (n.Level != level) continue;
			float d = n.Position.DistanceTo(p);
			if (d < bd) { bd = d; bestNode = n; }
		}
		if (bestNode != null) return new RoadSnap(bestNode.Position, bestNode, null, OutwardDirection(bestNode));

		RoadSegment bestSeg = null;
		Vector2 bestPoint = default;
		float sd = radius;
		foreach (RoadSegment s in Segments)
		{
			if (s.Level != level) continue;
			float d = RoadPoly.Nearest(PolyOf(s), p, out _, out Vector2 pt);
			if (d < sd) { sd = d; bestSeg = s; bestPoint = pt; }
		}
		if (bestSeg != null) return new RoadSnap(bestPoint, null, bestSeg, Vector2.Zero);

		return new RoadSnap(p, null, null, Vector2.Zero);
	}

	public List<Vector2> StartDirections(RoadSnap s)
	{
		var res = new List<Vector2>();
		if (s.Node != null)
		{
			foreach (RoadSegment seg in Segments)
			{
				if (seg.StartNodeId == s.Node.Id) res.Add(-seg.StartTangent);
				else if (seg.EndNodeId == s.Node.Id) res.Add(seg.EndTangent);
			}
		}
		else if (s.Segment != null)
		{
			Vector2[] pts = PolyOf(s.Segment);
			RoadPoly.Nearest(pts, s.Position, out float off, out _);
			Vector2 t = RoadPoly.TangentAt(pts, off);
			res.Add(t);
			res.Add(-t);
		}
		int n = res.Count;
		for (int i = 0; i < n; i++)
		{
			Vector2 l = RoadGeometry.Left(res[i]);
			res.Add(l);
			res.Add(-l);
		}
		return res;
	}

	public bool RayHit(Vector2 origin, Vector2 dir, RoadSegment s, out Vector2 hit)
	{
		hit = default;
		const float L = 6000f;
		Vector2 end = origin + dir * L;
		Vector2[] sp = PolyOf(s);
		float bestT = float.MaxValue;
		bool found = false;
		for (int j = 1; j < sp.Length; j++)
		{
			if (RoadPoly.Intersect(origin, end, sp[j - 1], sp[j], out float t, out Vector2 ip) && t * L > 8f && t < bestT)
			{
				bestT = t;
				hit = ip;
				found = true;
			}
		}
		return found;
	}

	private void AddSeg(RoadSegment s, bool built)
	{
		Segments.Add(s);
		_rec?.Added.Add(s);
		if (built) _rec?.Built.Add(s);
	}

	private void RemoveSeg(RoadSegment s)
	{
		Segments.Remove(s);
		_polyCache.Remove(s);
		if (_rec == null) return;
		if (_rec.Added.Remove(s)) _rec.Built.Remove(s);
		else _rec.Removed.Add(s);
	}

	public RoadNode CreateNode(Vector2 pos, int level)
	{
		var n = new RoadNode { Id = NextId++, Position = pos, Level = level };
		Nodes.Add(n);
		_rec?.Nodes.Add(n);
		return n;
	}

	public RoadNode NodeAt(Vector2 p, int level, float tol = 3f)
	{
		foreach (RoadNode n in Nodes) if (n.Level == level && n.Position.DistanceTo(p) <= tol) return n;

		foreach (RoadSegment s in Segments)
		{
			if (s.Level != level) continue;
			float d = RoadPoly.Nearest(PolyOf(s), p, out _, out Vector2 pt);
			if (d <= tol) return SplitSegment(s, pt);
		}
		return CreateNode(p, level);
	}

	public RoadNode SplitSegment(RoadSegment seg, Vector2 point)
	{
		Vector2[] pts = PolyOf(seg);
		RoadPoly.Nearest(pts, point, out float off, out Vector2 pp);
		float total = RoadPoly.Length(pts);

		if (off < 1f) return NodeById(seg.StartNodeId);
		if (off > total - 1f) return NodeById(seg.EndNodeId);

		RoadNode node = CreateNode(pp, seg.Level);
		RoadSegment first = seg.Derive(seg.StartNodeId, node.Id, RoadPoly.ToCurve(RoadPoly.Sub(pts, 0f, off)));
		RoadSegment second = seg.Derive(node.Id, seg.EndNodeId, RoadPoly.ToCurve(RoadPoly.Sub(pts, off, total)));

		RemoveSeg(seg);
		AddSeg(first, false);
		AddSeg(second, false);
		return node;
	}

	public bool AddRoad(Curve2D curve, int lanes, float laneWidth, int level) =>
		Build(curve, lanes, laneWidth, level) != null;

	public RoadOp Build(Curve2D curve, int lanes, float laneWidth, int level)
	{
		var op = new RoadOp();
		_rec = op;
		bool ok;
		try { ok = BuildInternal(curve, lanes, laneWidth, level); }
		finally { _rec = null; }

		if (!ok)
		{
			if (op.Added.Count > 0 || op.Nodes.Count > 0 || op.Removed.Count > 0) Undo(op);
			return null;
		}
		return op;
	}

	private bool BuildInternal(Curve2D curve, int lanes, float laneWidth, int level)
	{
		Vector2[] pts = RoadPoly.Of(curve);
		if (pts.Length < 2) return false;

		RoadNode a = NodeAt(pts[0], level);
		RoadNode b = NodeAt(pts[^1], level);
		if (a == b) return false;

		float total = RoadPoly.Length(pts);
		List<(float off, Vector2 p)> cuts = CollectCuts(pts, total, level);

		var nodes = new List<RoadNode> { a };
		var offs = new List<float> { 0f };
		float lastOff = -100f;
		foreach (var (off, p) in cuts)
		{
			if (off - lastOff < 2f) continue;
			lastOff = off;
			RoadNode n = NodeAt(p, level);
			if (n == nodes[^1] || n == b) continue;
			nodes.Add(n);
			offs.Add(off);
		}
		nodes.Add(b);
		offs.Add(total);

		for (int i = 0; i < nodes.Count - 1; i++)
		{
			Curve2D sub = nodes.Count == 2 ? curve : RoadPoly.ToCurve(RoadPoly.Sub(pts, offs[i], offs[i + 1]));
			AddSeg(new RoadSegment
			{
				StartNodeId = nodes[i].Id,
				EndNodeId = nodes[i + 1].Id,
				Curve = sub,
				LanesPerDirection = lanes,
				LaneWidth = laneWidth,
				Level = level
			}, true);
		}

		_polyCache.Clear();
		TopologyChanged?.Invoke();
		return true;
	}

	public void Undo(RoadOp op)
	{
		foreach (RoadSegment s in op.Added) Segments.Remove(s);
		foreach (RoadSegment s in op.Removed) Segments.Add(s);
		foreach (RoadNode n in op.Nodes) Nodes.Remove(n);
		foreach (RoadNode n in op.RemovedNodes) Nodes.Add(n);
		_polyCache.Clear();
		TopologyChanged?.Invoke();
	}

	public void Redo(RoadOp op)
	{
		foreach (RoadSegment s in op.Removed) Segments.Remove(s);
		foreach (RoadNode n in op.RemovedNodes) Nodes.Remove(n);
		foreach (RoadNode n in op.Nodes) Nodes.Add(n);
		foreach (RoadSegment s in op.Added) Segments.Add(s);
		_polyCache.Clear();
		TopologyChanged?.Invoke();
	}

	public List<Vector2> PreviewCuts(Curve2D curve, int level)
	{
		Vector2[] pts = RoadPoly.Of(curve);
		var res = new List<Vector2>();
		if (pts.Length < 2) return res;
		float last = -100f;
		foreach (var (off, p) in CollectCuts(pts, RoadPoly.Length(pts), level))
		{
			if (off - last < 2f) continue;
			last = off;
			res.Add(p);
		}
		return res;
	}

	private List<(float off, Vector2 p)> CollectCuts(Vector2[] pts, float total, int level)
	{
		var cuts = new List<(float off, Vector2 p)>();
		Rect2 box = Bounds(pts);
		foreach (RoadSegment s in Segments)
		{
			if (s.Level != level) continue;
			Vector2[] sp = PolyOf(s);
			if (!box.Intersects(Bounds(sp))) continue;
			float acc = 0f;
			for (int i = 1; i < pts.Length; i++)
			{
				float la = pts[i - 1].DistanceTo(pts[i]);
				for (int j = 1; j < sp.Length; j++)
				{
					if (RoadPoly.Intersect(pts[i - 1], pts[i], sp[j - 1], sp[j], out float t, out Vector2 ip))
					{
						float off = acc + la * t;
						if (off > EndMargin && off < total - EndMargin) cuts.Add((off, ip));
					}
				}
				acc += la;
			}
		}
		cuts.Sort((x, y) => x.off.CompareTo(y.off));
		return cuts;
	}

	private static Rect2 Bounds(Vector2[] p)
	{
		Vector2 mn = p[0], mx = p[0];
		foreach (Vector2 v in p) { mn = mn.Min(v); mx = mx.Max(v); }
		return new Rect2(mn, mx - mn).Grow(1f);
	}
}
