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

[GlobalClass]
public partial class RoadNetwork : Resource
{
    [Export] public Array<RoadNode> Nodes { get; set; } = new();
    [Export] public Array<RoadSegment> Segments { get; set; } = new();
    [Export] public int NextId { get; set; } = 1;

    public event Action TopologyChanged;

    private const float EndMargin = 6f;
    private readonly Dictionary<RoadSegment, Vector2[]> _polyCache = new();

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

    public RoadNode CreateNode(Vector2 pos, int level)
    {
        var n = new RoadNode { Id = NextId++, Position = pos, Level = level };
        Nodes.Add(n);
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

        Segments.Remove(seg);
        _polyCache.Remove(seg);
        Segments.Add(first);
        Segments.Add(second);
        return node;
    }

    public void AddRoad(Curve2D curve, int lanes, float laneWidth, int level)
    {
        Vector2[] pts = RoadPoly.Of(curve);
        if (pts.Length < 2) return;

        RoadNode a = NodeAt(pts[0], level);
        RoadNode b = NodeAt(pts[^1], level);
        if (a == b) return;

        float total = RoadPoly.Length(pts);

        var cuts = new List<(float off, Vector2 p)>();
        foreach (RoadSegment s in Segments)
        {
            if (s.Level != level) continue;
            Vector2[] sp = PolyOf(s);
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
            Segments.Add(new RoadSegment
            {
                StartNodeId = nodes[i].Id,
                EndNodeId = nodes[i + 1].Id,
                Curve = sub,
                LanesPerDirection = lanes,
                LaneWidth = laneWidth,
                Level = level
            });
        }

        _polyCache.Clear();
        TopologyChanged?.Invoke();
    }
}
