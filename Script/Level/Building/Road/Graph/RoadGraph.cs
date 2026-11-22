using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using JustCity.Script.Level.Building.Road.Geometry;

namespace JustCity.Script.Level.Building.Road.Graph;

[GlobalClass]
public partial class RoadGraph : Resource
{
    [Export] public Array<RoadJunction> Nodes { get; set; } = new();
    [Export] public Array<RoadEdge> Segments { get; set; } = new();
    [Export] public int NextId { get; set; } = 1;

    public event Action TopologyChanged;
    public int Version { get; private set; }
    public RoadLinks Links { get; private set; }

    private readonly System.Collections.Generic.Dictionary<RoadEdge, Vector2[]> polyCache = new();
    private readonly System.Collections.Generic.Dictionary<RoadEdge, Rect2> boundsCache = new();
    private RoadEdit recording;

    public RoadGraph()
    {
        Links = new RoadLinks(this);
    }

    public void RoadChanged()
    {
        polyCache.Clear();
        boundsCache.Clear();
        Version++;
        TopologyChanged?.Invoke();
    }

    public Vector2[] Poly(RoadEdge e)
    {
        if (!polyCache.ContainsKey(e)) polyCache[e] = RoadGeometry.Poly(e.Curve);
        return polyCache[e];
    }

    public Rect2 BoundsOf(RoadEdge e)
    {
        if (!boundsCache.ContainsKey(e)) boundsCache[e] = RoadGeometry.Bounds(Poly(e));
        return boundsCache[e];
    }

    public List<RoadEdge> Near(Vector2 point, float reach, int level)
    {
        List<RoadEdge> res = [];
        foreach (RoadEdge e in Segments) if (e.Level == level && BoundsOf(e).Grow(reach).HasPoint(point)) res.Add(e);
        return res;
    }

    public RoadJunction NodeById(int id)
    {
        foreach (RoadJunction n in Nodes) if (n.Id == id) return n;
        return null;
    }

    public RoadSnap Snap(Vector2 point, float radius, int level)
    {
        RoadJunction node = null;
        float best = radius;
        foreach (RoadJunction n in Nodes)
        {
            if (n.Level == level && n.Position.DistanceTo(point) < best)
            {
                best = n.Position.DistanceTo(point);
                node = n;
            }
        }
        if (node != null)
        {
            List<Arm> arms = Links.ArmsOf(node);
            RoadSnap snap = new(node.Position, node, null, arms.Count == 1 ? -arms[0].Out : Vector2.Zero);
            foreach (LaneEnd end in FreeLaneEnds(arms))
                if (end.Position.DistanceTo(point) < best)
                {
                    best = end.Position.DistanceTo(point);
                    snap.Position = end.Position;
                    snap.Lane = end.Lane;
                    snap.Flow = end.Incoming ? 1 : -1;
                    snap.Direction = -end.Arm.Out;
                }
            return snap;
        }

        RoadEdge edge = null;
        Vector2 foot = Vector2.Zero;
        best = radius;
        foreach (RoadEdge e in Near(point, radius, level))
        {
            float dist = RoadGeometry.Nearest(Poly(e), point, out _, out Vector2 closest);
            if (dist < best)
            {
                best = dist;
                edge = e;
                foot = closest;
            }
        }
        return edge == null ? new RoadSnap(point) : new RoadSnap(foot, null, edge, Vector2.Zero);
    }

    public class LaneEnd
    {
        public Arm Arm;
        public int Lane;
        public bool Incoming;
        public Vector2 Position;
    }

    public static List<LaneEnd> FreeLaneEnds(List<Arm> arms)
    {
        List<LaneEnd> res = [];
        foreach (Arm a in arms)
        {
            bool open = a.In + a.Exit > 1;
            foreach (Arm b in arms) if (b != a && b.Out.Dot(a.Out) > -0.5f) open = false;
            if (!open) continue;

            for (int i = 0; i < 2; i++)
            {
                for (int lane = 0; lane < (i == 0 ? a.In : a.Exit); lane++)
                {
                    Vector2 p = a.LanePoint(lane, i == 0);
                    bool taken = false;
                    foreach (Arm b in arms)
                        if (b != a && b.Covers(p)) taken = true;
                    if (!taken) res.Add(new LaneEnd { Arm = a, Lane = lane, Incoming = i == 0, Position = p });
                }
            }
        }
        return res;
    }

    public List<Arm> ArmsAtPoint(Vector2 point, int level, float tolerance = 3f)
    {
        foreach (RoadJunction n in Nodes) if (n.Level == level && n.Position.DistanceTo(point) <= tolerance) return Links.ArmsOf(n);

        List<Arm> arms = [];
        foreach (RoadEdge e in Near(point, tolerance, level))
        {
            Vector2[] pts = Poly(e);
            if (RoadGeometry.Nearest(pts, point, out float offset, out Vector2 foot) > tolerance) continue;
            Vector2 tangent = RoadGeometry.TangentAt(pts, offset);
            arms.Add(new Arm(foot, tangent, e.Width * 0.5f, RoadGeometry.Length(pts) - offset) { Edge = e });
            arms.Add(new Arm(foot, -tangent, e.Width * 0.5f, offset) { Edge = e });
            break;
        }
        return arms;
    }

    public string JunctionProblem(Curve2D curve, int level, bool oneWay, RoadSnap start, RoadSnap end, float minAngleDeg, int maxArms, out Vector2 at)
    {
        at = Vector2.Zero;
        if (oneWay) return null;

        string reason;
        if (start != null && !start.Free)
        {
            at = start.Foot;
            reason = GapProblem(ArmsAtPoint(start.Foot, level), [RoadGeometry.StartTangent(curve)], minAngleDeg, maxArms);
            if (reason != null) return reason;
        }
        if (end != null && !end.Free)
        {
            at = end.Foot;
            reason = GapProblem(ArmsAtPoint(end.Foot, level), [-RoadGeometry.EndTangent(curve)], minAngleDeg, maxArms);
            if (reason != null) return reason;
        }

        Vector2[] pts = RoadGeometry.Poly(curve);
        float[] offsets = Cuts(pts, RoadGeometry.Length(pts), level, out Vector2[] points);
        for (int i = 0; i < offsets.Length; i++)
        {
            at = points[i];
            Vector2 along = RoadGeometry.TangentAt(pts, offsets[i]);
            reason = GapProblem(ArmsAtPoint(points[i], level), [along, -along], minAngleDeg, maxArms);
            if (reason != null) return reason;
        }
        return null;
    }

    private static string GapProblem(List<Arm> arms, Vector2[] newDirs, float minAngleDeg, int maxArms)
    {
        List<float> angles = [];
        foreach (Arm a in arms) if (a.Edge == null || !a.Edge.OneWay) angles.Add(a.Out.Angle());
        foreach (Vector2 d in newDirs) angles.Add(d.Angle());
        if (angles.Count > maxArms) return "ROAD_ERR_ARMS";

        angles.Sort();
        for (int i = 0; i < angles.Count && angles.Count > 1; i++)
        {
            float next = i + 1 < angles.Count ? angles[i + 1] : angles[0] + Mathf.Tau;
            if (next - angles[i] < Mathf.DegToRad(minAngleDeg)) return "ROAD_ERR_ANGLE";
        }
        return null;
    }

    public RoadSnap OnLane(RoadEdge edge, Vector2 foot, Vector2 cursor, int minLanes, bool hasApproach, Vector2 approach)
    {
        RoadSnap snap = new(foot, null, edge, Vector2.Zero);
        if (edge.OneWay || edge.LanesPerDirection < minLanes) return snap;

        Vector2[] pts = Poly(edge);
        RoadGeometry.Nearest(pts, foot, out float offset, out _);
        Vector2 normal = RoadGeometry.Left(RoadGeometry.TangentAt(pts, offset));
        float across = (cursor - foot).Dot(normal);
        float side = across < 0f ? -1f : 1f;
        if (hasApproach && Mathf.Abs((approach - foot).Dot(normal)) > 1f) side = (approach - foot).Dot(normal) < 0f ? -1f : 1f;

        snap.Lane = Mathf.Clamp((int)(Mathf.Abs(across) / edge.LaneWidth), 0, edge.LanesPerDirection - 1);
        snap.Position = foot + normal * side * (snap.Lane + 0.5f) * edge.LaneWidth;
        return snap;
    }

    public List<Vector2> StartDirections(RoadSnap snap)
    {
        List<Vector2> dirs = [];
        if (snap.Node != null && snap.Lane >= 0) dirs.Add(snap.Direction);
        else if (snap.Node != null) foreach (Arm a in Links.ArmsOf(snap.Node)) dirs.Add(-a.Out);
        else if (snap.Edge != null)
        {
            Vector2[] pts = Poly(snap.Edge);
            RoadGeometry.Nearest(pts, snap.Foot, out float offset, out _);
            dirs.Add(RoadGeometry.TangentAt(pts, offset));
            dirs.Add(-dirs[0]);
        }
        int count = dirs.Count;
        for (int i = 0; i < count; i++)
        {
            dirs.Add(RoadGeometry.Left(dirs[i]));
            dirs.Add(-RoadGeometry.Left(dirs[i]));
        }
        return dirs;
    }

    public bool RayHit(Vector2 origin, Vector2 dir, RoadEdge e, out Vector2 hit)
    {
        const float RayLength = 6000f;
        hit = Vector2.Zero;
        float bestT = float.MaxValue;
        Vector2[] pts = Poly(e);
        for (int i = 1; i < pts.Length; i++)
            if (RoadGeometry.Intersect(origin, origin + dir * RayLength, pts[i - 1], pts[i], out float t, out Vector2 p) && t * RayLength > 8f && t < bestT)
            {
                bestT = t;
                hit = p;
            }
        return bestT < float.MaxValue;
    }

    private void AddEdge(RoadEdge e, bool built)
    {
        Segments.Add(e);
        if (recording == null) return;
        recording.Added.Add(e);
        if (built) recording.Built.Add(e);
    }

    private void RemoveEdge(RoadEdge e)
    {
        Segments.Remove(e);
        polyCache.Remove(e);
        boundsCache.Remove(e);
        if (recording == null) return;
        if (recording.Added.Remove(e)) recording.Built.Remove(e);
        else recording.Removed.Add(e);
    }

    public RoadJunction CreateNode(Vector2 position, int level)
    {
        RoadJunction n = new() { Id = NextId++, Position = position, Level = level };
        Nodes.Add(n);
        recording?.Nodes.Add(n);
        return n;
    }

    public RoadJunction NodeAt(Vector2 point, int level, float tolerance = 3f)
    {
        foreach (RoadJunction n in Nodes) if (n.Level == level && n.Position.DistanceTo(point) <= tolerance) return n;
        foreach (RoadEdge e in Near(point, tolerance, level)) if (RoadGeometry.Nearest(Poly(e), point, out _, out Vector2 closest) <= tolerance) return SplitEdge(e, closest);
        return CreateNode(point, level);
    }

    public RoadJunction SplitEdge(RoadEdge e, Vector2 point)
    {
        Vector2[] pts = Poly(e);
        RoadGeometry.Nearest(pts, point, out float offset, out Vector2 onLine);
        float total = RoadGeometry.Length(pts);
        if (offset < 1f) return NodeById(e.StartNodeId);
        if (offset > total - 1f) return NodeById(e.EndNodeId);

        RoadJunction n = CreateNode(onLine, e.Level);
        RoadEdge first = e.Derive(e.StartNodeId, n.Id, RoadGeometry.ToCurve(RoadGeometry.Sub(pts, 0f, offset)), 0, 0);
        RoadEdge second = e.Derive(n.Id, e.EndNodeId, RoadGeometry.ToCurve(RoadGeometry.Sub(pts, offset, total)), 0, 0);
        RemoveEdge(e);
        AddEdge(first, false);
        AddEdge(second, false);
        return n;
    }

    public RoadEdit Build(Curve2D curve, int lanes, float laneWidth, int level, bool oneWay, RoadSnap start, RoadSnap end)
    {
        RoadEdit edit = new();
        recording = edit;
        bool ok = false;
        try
        {
            ok = BuildEdges(curve, lanes, laneWidth, level, oneWay, start, end);
        }
        finally
        {
            recording = null;
        }
        if (!ok && (edit.Added.Count > 0 || edit.Nodes.Count > 0 || edit.Removed.Count > 0)) Undo(edit);
        return ok ? edit : null;
    }

    private bool BuildEdges(Curve2D curve, int lanes, float laneWidth, int level, bool oneWay, RoadSnap start, RoadSnap end)
    {
        Vector2[] pts = RoadGeometry.Poly(curve);
        RoadJunction first = NodeAt(start != null && !start.Free ? start.Foot : pts[0], level);
        RoadJunction last = NodeAt(end != null && !end.Free ? end.Foot : pts[^1], level);
        if (first == last) return false;

        float total = RoadGeometry.Length(pts);
        List<RoadJunction> nodes = [first];
        List<float> offsets = [0f];
        float[] cutOffsets = Cuts(pts, total, level, out Vector2[] cutPoints);
        for (int i = 0; i < cutOffsets.Length; i++)
        {
            RoadJunction n = NodeAt(cutPoints[i], level);
            if (n == nodes[^1] || n == last) continue;
            nodes.Add(n);
            offsets.Add(cutOffsets[i]);
        }
        nodes.Add(last);
        offsets.Add(total);

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            RoadEdge e = new()
            {
                StartNodeId = nodes[i].Id, EndNodeId = nodes[i + 1].Id, LanesPerDirection = lanes, LaneWidth = laneWidth,
                OneWay = oneWay, Width = lanes * laneWidth * (oneWay ? 1f : 2f), Level = level,
                Curve = nodes.Count > 2 ? RoadGeometry.ToCurve(RoadGeometry.Sub(pts, offsets[i], offsets[i + 1])) : curve
            };
            AddEdge(e, true);
        }
        RoadChanged();
        return true;
    }

    private float[] Cuts(Vector2[] pts, float total, int level, out Vector2[] points)
    {
        const float EndMargin = 6f;
        List<float> offsets = [];
        List<Vector2> found = [];
        Rect2 box = RoadGeometry.Bounds(pts);
        foreach (RoadEdge e in Segments)
        {
            if (e.Level != level || !box.Intersects(BoundsOf(e))) continue;
            Vector2[] other = Poly(e);
            float walked = 0f;
            for (int i = 1; i < pts.Length; i++)
            {
                float len = pts[i - 1].DistanceTo(pts[i]);
                for (int j = 1; j < other.Length; j++)
                {
                    if (RoadGeometry.Intersect(pts[i - 1], pts[i], other[j - 1], other[j], out float t, out Vector2 p) && walked + len * t > EndMargin && walked + len * t < total - EndMargin)
                    {
                        offsets.Add(walked + len * t);
                        found.Add(p);
                    }
                }
                walked += len;
            }
        }

        float[] keys = [.. offsets];
        Vector2[] values = [.. found];
        System.Array.Sort(keys, values);
        List<float> spacedOffsets = [];
        List<Vector2> spacedPoints = [];
        for (int i = 0; i < keys.Length; i++)
        {
            if (spacedOffsets.Count == 0 || keys[i] - spacedOffsets[^1] >= 2f)
            {
                spacedOffsets.Add(keys[i]);
                spacedPoints.Add(values[i]);
            }
        }
        points = [.. spacedPoints];
        return [.. spacedOffsets];
    }

    public List<Vector2> PreviewCuts(Curve2D curve, int level)
    {
        Vector2[] pts = RoadGeometry.Poly(curve);
        Cuts(pts, RoadGeometry.Length(pts), level, out Vector2[] points);
        return [.. points];
    }

    private static void ApplyLinks(RoadEdit.LinkChange c, bool undo)
    {
        c.Node.LinkData = (int[])(undo ? c.Before : c.After).Clone();
        c.Node.CustomLinks = undo ? c.BeforeCustom : c.AfterCustom;
    }

    public void Undo(RoadEdit edit)
    {
        foreach (RoadEdge e in edit.Added) Segments.Remove(e);
        foreach (RoadEdge e in edit.Removed) Segments.Add(e);
        foreach (RoadJunction n in edit.Nodes) Nodes.Remove(n);
        foreach (RoadJunction n in edit.RemovedNodes) Nodes.Add(n);
        for (int i = edit.Links.Count - 1; i >= 0; i--) ApplyLinks(edit.Links[i], true);
        RoadChanged();
    }

    public void Redo(RoadEdit edit)
    {
        foreach (RoadEdge e in edit.Removed) Segments.Remove(e);
        foreach (RoadJunction n in edit.RemovedNodes) Nodes.Remove(n);
        foreach (RoadJunction n in edit.Nodes) Nodes.Add(n);
        foreach (RoadEdge e in edit.Added) Segments.Add(e);
        foreach (RoadEdit.LinkChange c in edit.Links) ApplyLinks(c, false);
        RoadChanged();
    }

    public RoadEdit Clear(ClearPlan plan)
    {
        if (plan.Items.Count == 0) return null;
        RoadEdit edit = new();
        recording = edit;
        List<int> touched = [];
        try
        {
            foreach (ClearPlan.Item item in plan.Items)
            {
                RoadEdge e = item.Edge;
                float total = RoadGeometry.Length(item.Pts);
                touched.Add(e.StartNodeId);
                touched.Add(e.EndNodeId);
                RemoveEdge(e);
                for (int i = 0; i < item.Inside.Count; i++)
                {
                    float begin = item.Offsets[i], end = item.Offsets[i + 1];
                    if (item.Inside[i] || end - begin < 4f) continue;
                    int startId = begin > 0.01f ? CreateNode(RoadGeometry.PointAt(item.Pts, begin), e.Level).Id : e.StartNodeId;
                    int endId = end < total - 0.01f ? CreateNode(RoadGeometry.PointAt(item.Pts, end), e.Level).Id : e.EndNodeId;
                    AddEdge(e.Derive(startId, endId, RoadGeometry.ToCurve(RoadGeometry.Sub(item.Pts, begin, end)), 0, 0), false);
                }
            }
            foreach (int id in touched)
            {
                RoadJunction n = NodeById(id);
                if (n == null || IsUsed(id)) continue;
                Nodes.Remove(n);
                edit.RemovedNodes.Add(n);
            }
        }
        finally
        {
            recording = null;
        }
        RoadChanged();
        return edit;
    }

    private bool IsUsed(int nodeId)
    {
        foreach (RoadEdge e in Segments) if (e.StartNodeId == nodeId || e.EndNodeId == nodeId) return true;
        return false;
    }
}
