using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JustCity.Script.Level.Building;

public sealed class SegClear
{
	public RoadSegment Seg;
	public Vector2[] Pts;
	public List<(float a, float b, bool inside)> Parts = new();
	public float InsideLength;
}

public sealed class ClearPlan
{
	public List<SegClear> Items = new();
	public List<Vector2> Cuts = new();
	public float Cost;
	public float Length;
	public Rect2 Rect;
}

public partial class RoadNetwork
{
	public ClearPlan PlanClear(Rect2 rect, int level, float costPerPx, float bridgeMul)
	{
		var plan = new ClearPlan { Rect = rect };
		Vector2 mn = rect.Position, mx = rect.End;

		foreach (RoadSegment s in Segments)
		{
			if (s.Level != level) continue;
			Vector2[] pts = PolyOf(s);
			if (pts.Length < 2 || !rect.Intersects(Bounds(pts))) continue;

			float total = RoadPoly.Length(pts);
			var bps = new List<float> { 0f, total };
			float acc = 0f;
			for (int i = 1; i < pts.Length; i++)
			{
				float len = pts[i - 1].DistanceTo(pts[i]);
				if (len > 1e-4f && ClipT(pts[i - 1], pts[i], rect, out float t0, out float t1))
				{
					if (t0 > 1e-4f) bps.Add(acc + len * t0);
					if (t1 < 1f - 1e-4f) bps.Add(acc + len * t1);
				}
				acc += len;
			}
			bps.Sort();

			var parts = new List<(float a, float b, bool inside)>();
			for (int k = 0; k < bps.Count - 1; k++)
			{
				float pa = bps[k], pb = bps[k + 1];
				if (pb - pa < 0.05f) continue;
				Vector2 m = RoadPoly.PointAt(pts, (pa + pb) * 0.5f);
				bool ins = m.X > mn.X && m.X < mx.X && m.Y > mn.Y && m.Y < mx.Y;
				if (parts.Count > 0 && parts[^1].inside == ins) parts[^1] = (parts[^1].a, pb, ins);
				else parts.Add((pa, pb, ins));
			}
			if (parts.Count == 0 || !parts.Any(p => p.inside)) continue;
			parts[0] = (0f, parts[0].b, parts[0].inside);
			parts[^1] = (parts[^1].a, total, parts[^1].inside);

			var item = new SegClear { Seg = s, Pts = pts, Parts = parts };
			foreach (var (pa, pb, ins) in parts)
			{
				if (!ins) continue;
				item.InsideLength += pb - pa;
				if (pa > 0.5f) plan.Cuts.Add(RoadPoly.PointAt(pts, pa));
				if (pb < total - 0.5f) plan.Cuts.Add(RoadPoly.PointAt(pts, pb));
			}
			plan.Items.Add(item);
			float rate = costPerPx * s.LanesPerDirection * (s.Level > 0 ? bridgeMul : 1f);
			plan.Cost += item.InsideLength * rate;
			plan.Length += item.InsideLength;
		}
		return plan;
	}

	public RoadOp Clear(ClearPlan plan)
	{
		if (plan == null || plan.Items.Count == 0) return null;

		var op = new RoadOp();
		_rec = op;
		var touched = new HashSet<int>();
		try
		{
			foreach (SegClear it in plan.Items)
			{
				RoadSegment s = it.Seg;
				float total = RoadPoly.Length(it.Pts);
				touched.Add(s.StartNodeId);
				touched.Add(s.EndNodeId);
				RemoveSeg(s);

				foreach (var (a, b, inside) in it.Parts)
				{
					if (inside || b - a < 4f) continue;
					int startId = a <= 0.01f ? s.StartNodeId : CreateNode(RoadPoly.PointAt(it.Pts, a), s.Level).Id;
					int endId = b >= total - 0.01f ? s.EndNodeId : CreateNode(RoadPoly.PointAt(it.Pts, b), s.Level).Id;
					AddSeg(s.Derive(startId, endId, RoadPoly.ToCurve(RoadPoly.Sub(it.Pts, a, b))), false);
				}
			}

			foreach (int id in touched)
			{
				if (Segments.Any(x => x.StartNodeId == id || x.EndNodeId == id)) continue;
				RoadNode n = NodeById(id);
				if (n == null) continue;
				Nodes.Remove(n);
				op.RemovedNodes.Add(n);
			}
		}
		finally { _rec = null; }

		_polyCache.Clear();
		TopologyChanged?.Invoke();
		return op;
	}

	private static bool ClipT(Vector2 a, Vector2 b, Rect2 r, out float t0, out float t1)
	{
		t0 = 0f;
		t1 = 1f;
		Vector2 d = b - a;
		return ClipEdge(-d.X, a.X - r.Position.X, ref t0, ref t1)
			&& ClipEdge(d.X, r.End.X - a.X, ref t0, ref t1)
			&& ClipEdge(-d.Y, a.Y - r.Position.Y, ref t0, ref t1)
			&& ClipEdge(d.Y, r.End.Y - a.Y, ref t0, ref t1);
	}

	private static bool ClipEdge(float p, float q, ref float t0, ref float t1)
	{
		if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
		float t = q / p;
		if (p < 0f)
		{
			if (t > t1) return false;
			if (t > t0) t0 = t;
		}
		else
		{
			if (t < t0) return false;
			if (t < t1) t1 = t;
		}
		return true;
	}
}
