using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Geometry;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.View;

public partial class RoadRenderer : Node2D
{
    [Export] public bool ShowDebug { get; set; } = false;
    [Export] public float OutlineWidth { get; set; } = 1.5f;
    [Export] public float GrowTime { get; set; } = 0.22f;
    [Export] public float FilletRadius { get; set; } = 8f;
    public RoadGraph Network { get; set; }
    
    private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.12f);
    private static readonly Color OutlineColor = new(0.64f, 0.64f, 0.64f);
    private static readonly Color RoadColor = new(1f, 1f, 1f);
    private static readonly Color BridgeColor = new(0.84f, 0.84f, 0.86f);
    private static readonly Color CenterColor = new(0f, 0f, 0f, 0.9f);
    private static readonly Color LaneColor = new(0f, 0f, 0f, 0.6f);

    private readonly Dictionary<RoadEdge, float> age = new();
    private readonly Dictionary<int, List<RoadEdge>> edgesByLevel = new();
    private readonly List<CornerFill> corners = [];
    private readonly Dictionary<RoadEdge, Marks> marks = new();
    private RoadGraph built;
    private int builtVersion = -1;

    private class CornerFill
    {
        public int Level;
        public List<RoadEdge> Edges;
        public List<Vector2> Triangles = [];
        public List<Vector2[]> Rims = [];
    }

    private class Marks
    {
        public Vector2[] Center = [];
        public Vector2[] Dashes = [];
    }

    public void Grow(List<RoadEdge> edges)
    {
        foreach (RoadEdge e in edges) age[e] = 0f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Network != built || (Network != null && Network.Version != builtVersion)) QueueRedraw();
        if (age.Count == 0) return;
        foreach (RoadEdge e in new List<RoadEdge>(age.Keys))
        {
            age[e] += (float)delta;
            if (age[e] >= GrowTime) age.Remove(e);
        }
        QueueRedraw();
    }

    private float Progress(RoadEdge e)
    {
        if (!age.TryGetValue(e, out float value)) return 1f;
        float t = Mathf.Clamp(value / Mathf.Max(0.01f, GrowTime), 0f, 1f);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    public override void _Draw()
    {
        if (Network == null) return;
        if (Network != built || Network.Version != builtVersion) Rebuild();

        List<int> levels = [.. edgesByLevel.Keys];
        levels.Sort();
        foreach (int level in levels)
        {
            List<RoadEdge> edges = edgesByLevel[level];
            List<CornerFill> settled = [];
            foreach (CornerFill c in corners) if (c.Level == level && c.Edges.TrueForAll(IsGrown)) settled.Add(c);

            Color fill = level > 0 ? BridgeColor : RoadColor;
            float line = OutlineWidth * 2f;
            if (level > 0)
            {
                foreach (RoadEdge e in edges) Stroke(e, e.Width + line, ShadowColor, new Vector2(6f, 8f), OutlineWidth);
                foreach (CornerFill c in settled) RoadCorners.Draw(this, c.Triangles, ShadowColor, new Vector2(6f, 8f));
            }
            foreach (RoadEdge e in edges) Stroke(e, e.Width + line, OutlineColor, Vector2.Zero, OutlineWidth);
            foreach (CornerFill c in settled) foreach (Vector2[] rim in c.Rims) DrawPolyline(rim, OutlineColor, line, true);
            foreach (RoadEdge e in edges) Stroke(e, e.Width, fill, Vector2.Zero, 0f);
            foreach (CornerFill c in settled) RoadCorners.Draw(this, c.Triangles, fill, Vector2.Zero);
            foreach (RoadEdge e in edges)
            {
                if (Progress(e) < 1f || !marks.ContainsKey(e)) continue;
                if (marks[e].Center.Length >= 2) DrawPolyline(marks[e].Center, CenterColor, 1f, true);
                if (marks[e].Dashes.Length >= 2) DrawMultiline(marks[e].Dashes, LaneColor, 1.5f, true);
            }
        }

        if (!ShowDebug) return;
        foreach (RoadEdge e in Network.Segments) DrawPolyline(Network.Poly(e), Colors.Black, 1f, true);
        foreach (RoadJunction n in Network.Nodes) DrawCircle(n.Position, 4f, Colors.Black);
    }

    private bool IsGrown(RoadEdge e)
    {
        return Progress(e) >= 1f;
    }

    private void Stroke(RoadEdge e, float width, Color color, Vector2 offset, float extend)
    {
        Vector2[] pts = Network.Poly(e);
        if (Progress(e) < 1f) pts = RoadGeometry.Sub(pts, 0f, Mathf.Max(0.5f, RoadGeometry.Length(pts) * Progress(e)));

        List<Vector2> line = [];
        foreach (Vector2 p in pts) if (line.Count == 0 || line[^1].DistanceSquaredTo(p + offset) > 0.5f) line.Add(p + offset);
        if (line.Count < 2) return;
        line[0] += (line[0] - line[1]).Normalized() * extend;
        line[^1] += (line[^1] - line[^2]).Normalized() * extend;
        DrawPolyline([.. line], color, width, true);
    }

    private void Rebuild()
    {
        built = Network;
        builtVersion = Network.Version;
        edgesByLevel.Clear();
        corners.Clear();
        marks.Clear();

        Dictionary<int, List<Arm>> arms = new();
        Dictionary<int, List<RoadEdge>> edgesAtNode = new();
        Dictionary<int, float> widest = new();
        foreach (RoadEdge e in Network.Segments)
        {
            if (!edgesByLevel.ContainsKey(e.Level)) edgesByLevel[e.Level] = [];
            edgesByLevel[e.Level].Add(e);
            for (int i = 0; i < 2; i++)
            {
                int id = i == 0 ? e.StartNodeId : e.EndNodeId;
                if (!arms.TryGetValue(id, out List<Arm> value))
                {
                    value = [];
                    arms[id] = value;
                    edgesAtNode[id] = [];
                    widest[id] = 0f;
                }

                value.Add(new Arm(e, i == 1));
                edgesAtNode[id].Add(e);
                widest[id] = Mathf.Max(widest[id], e.Width);
            }
        }

        Dictionary<int, Vector2> position = new();
        foreach (RoadJunction n in Network.Nodes)
        {
            position[n.Id] = n.Position;
            if (!arms.ContainsKey(n.Id)) continue;
            CornerFill c = new() { Level = n.Level, Edges = edgesAtNode[n.Id] };
            RoadCorners.Build(n.Position, arms[n.Id], FilletRadius, c.Triangles, c.Rims);
            if (c.Triangles.Count > 0) corners.Add(c);
        }

        foreach (RoadEdge e in Network.Segments)
        {
            float[] trim = new float[2];
            for (int i = 0; i < 2; i++)
            {
                int id = i == 0 ? e.StartNodeId : e.EndNodeId;
                Vector2 end = e.Curve.GetPointPosition(i == 0 ? 0 : e.Curve.PointCount - 1);
                if (arms[id].Count >= 3) trim[i] = Mathf.Max(e.Width * 0.5f, widest[id] * 0.5f - (position.TryGetValue(id, out Vector2 value) ? value.DistanceTo(end) : 0f));
            }
            marks[e] = MakeMarks(e, trim[0], trim[1]);
        }
    }

    private static Marks MakeMarks(RoadEdge e, float trimStart, float trimEnd)
    {
        Marks m = new();
        float first = trimStart, last = e.Curve.GetBakedLength() - trimEnd;
        if (last - first < 4f) return m;

        List<Vector2> center = [];
        for (float o = first; o < last; o += 4f) center.Add(e.Curve.SampleBaked(o));
        center.Add(e.Curve.SampleBaked(last));
        if (!e.OneWay) m.Center = [.. center];

        List<float> shifts = [];
        for (int lane = 1; lane < e.LanesPerDirection; lane++)
        {
            if (e.OneWay) shifts.Add((lane - e.LanesPerDirection / 2f) * e.LaneWidth);
            else
            {
                shifts.Add(lane * e.LaneWidth);
                shifts.Add(-lane * e.LaneWidth);
            }
        }

        List<Vector2> dashes = [];
        foreach (float shift in shifts)
        {
            for (float o = first; o + 8f <= last; o += 16f)
            {
                Vector2 a = e.Curve.SampleBaked(o), b = e.Curve.SampleBaked(o + 8f);
                Vector2 side = (b - a).Orthogonal().Normalized() * shift;
                dashes.Add(a + side);
                dashes.Add(b + side);
            }
        }
        m.Dashes = [.. dashes];
        return m;
    }
}
