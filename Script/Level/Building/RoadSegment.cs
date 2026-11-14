using Godot;

namespace JustCity.Script.Level.Building;

[GlobalClass]
public partial class RoadSegment : Resource
{
    [ExportGroup("Graph")]
    [Export] public int StartNodeId { get; set; }
    [Export] public int EndNodeId { get; set; }
    [Export] public Curve2D Curve { get; set; } = new();

    [ExportGroup("Shape")]
    [Export] public int LanesPerDirection { get; set; } = 1;
    [Export] public float LaneWidth { get; set; } = 12f;
    [Export] public int Level { get; set; } = 0;

    public float Width => LanesPerDirection * LaneWidth * 2f;
    public float Length => Curve.GetBakedLength();

    public Vector2 StartTangent
    {
        get
        {
            if (Length < 1f) return Vector2.Right;
            return (Curve.SampleBaked(1f) - Curve.SampleBaked(0f)).Normalized();
        }
    }

    public Vector2 EndTangent
    {
        get
        {
            float len = Length;
            if (len < 1f) return Vector2.Right;
            return (Curve.SampleBaked(len) - Curve.SampleBaked(len - 1f)).Normalized();
        }
    }

    public RoadSegment() { }

    public RoadSegment Derive(int startId, int endId, Curve2D curve) => new()
    {
        StartNodeId = startId,
        EndNodeId = endId,
        Curve = curve,
        LanesPerDirection = LanesPerDirection,
        LaneWidth = LaneWidth,
        Level = Level
    };
}
