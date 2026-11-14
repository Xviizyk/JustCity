using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JustCity.Script.Level.Building;

public partial class RoadRenderer : Node2D
{
    [Export] public bool ShowDebug { get; set; } = false;
    [Export] public float OutlineWidth { get; set; } = 2f;

    private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.18f);
    private static readonly Color OutlineColor = new(0.55f, 0.55f, 0.55f);
    private static readonly Color AsphaltColor = new(1f, 1f, 1f);
    private static readonly Color CenterColor = new(0.85f, 0.8f, 0.2f);
    private static readonly Color LaneColor = new(0.6f, 0.6f, 0.6f);

    private RoadNetwork _network;

    public RoadNetwork Network
    {
        get => _network;
        set
        {
            _network?.TopologyChanged -= QueueRedraw;
            _network = value;
            _network?.TopologyChanged += QueueRedraw;
            QueueRedraw();
        }
    }

    public override void _ExitTree()
    {
        _network?.TopologyChanged -= QueueRedraw;
    }

    public override void _Draw()
    {
        if (_network == null) return;

        var degree = new Dictionary<int, int>();
        var nodeWidth = new Dictionary<int, float>();
        foreach (RoadSegment s in _network.Segments)
        {
            foreach (int id in new[] { s.StartNodeId, s.EndNodeId })
            {
                degree[id] = degree.GetValueOrDefault(id) + 1;
                nodeWidth[id] = Mathf.Max(nodeWidth.GetValueOrDefault(id), s.Width);
            }
        }

        foreach (int level in _network.Segments.Select(s => s.Level).Distinct().OrderBy(l => l))
        {
            List<RoadSegment> segs = _network.Segments.Where(s => s.Level == level).ToList();
            List<RoadNode> nodes = _network.Nodes.Where(n => n.Level == level && nodeWidth.ContainsKey(n.Id)).ToList();

            if (level > 0)
            {
                Vector2 off = new(6f, 8f);
                foreach (RoadSegment s in segs) Stroke(s, s.Width + OutlineWidth * 2f, ShadowColor, off);
                foreach (RoadNode n in nodes) DrawCircle(n.Position + off, nodeWidth[n.Id] / 2f + OutlineWidth, ShadowColor);
            }

            foreach (RoadSegment s in segs) Stroke(s, s.Width + OutlineWidth * 2f, OutlineColor, Vector2.Zero);
            foreach (RoadNode n in nodes) DrawCircle(n.Position, nodeWidth[n.Id] / 2f + OutlineWidth, OutlineColor);
            foreach (RoadSegment s in segs) Stroke(s, s.Width, AsphaltColor, Vector2.Zero);
            foreach (RoadNode n in nodes) DrawCircle(n.Position, nodeWidth[n.Id] / 2f, AsphaltColor);
            foreach (RoadSegment s in segs) DrawMarkings(s, degree);
        }

        if (ShowDebug)
        {
            foreach (RoadSegment s in _network.Segments) DrawPolyline(_network.PolyOf(s), Colors.Black, 1f, true);
            foreach (RoadNode n in _network.Nodes) DrawCircle(n.Position, 4f, Colors.Black);
        }
    }

    private void Stroke(RoadSegment s, float width, Color color, Vector2 offset)
    {
        Vector2[] src = _network.PolyOf(s);
        var pts = new Vector2[src.Length];
        for (int i = 0; i < src.Length; i++) pts[i] = src[i] + offset;
        DrawPolyline(pts, color, width, true);
        DrawCircle(pts[0], width / 2f, color);
        DrawCircle(pts[^1], width / 2f, color);
    }

    private void DrawMarkings(RoadSegment s, Dictionary<int, int> degree)
    {
        float len = s.Length;
        float trimA = degree.GetValueOrDefault(s.StartNodeId) >= 3 ? s.Width * 0.5f : 0f;
        float trimB = degree.GetValueOrDefault(s.EndNodeId) >= 3 ? s.Width * 0.5f : 0f;
        float from = trimA;
        float to = len - trimB;
        if (to - from < 4f) return;

        var center = new List<Vector2>();
        for (float o = from; o < to; o += 4f) center.Add(s.Curve.SampleBaked(o));
        center.Add(s.Curve.SampleBaked(to));
        if (center.Count >= 2) DrawPolyline(center.ToArray(), CenterColor, 1.5f, true);

        const float dash = 8f;
        const float gap = 8f;
        for (int lane = 1; lane < s.LanesPerDirection; lane++)
        {
            float shift = lane * s.LaneWidth;
            foreach (int sign in new[] { -1, 1 })
            {
                for (float o = from; o + dash <= to; o += dash + gap)
                {
                    Vector2 a = s.Curve.SampleBaked(o);
                    Vector2 b = s.Curve.SampleBaked(o + dash);
                    Vector2 n = (b - a).Orthogonal().Normalized() * shift * sign;
                    DrawLine(a + n, b + n, LaneColor, 1.5f, true);
                }
            }
        }
    }
}
