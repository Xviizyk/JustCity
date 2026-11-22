using Godot;

namespace JustCity.Script.Level.Building.Road.Graph;

[GlobalClass]
public partial class RoadEdge : Resource
{
    [ExportGroup("Graph")]
    [Export] public int StartNodeId { get; set; }
    [Export] public int EndNodeId { get; set; }
    [Export] public int StartArm { get; set; }
    [Export] public int EndArm { get; set; }
    public Curve2D Curve { get; set; } = new();
    
    [ExportGroup("Shape")]
    [Export] public int LanesPerDirection { get; set; } = 1;
    [Export] public float LaneWidth { get; set; } = 12f;
    [Export] public bool OneWay { get; set; } = false;
    [Export] public float Width { get; set; } = 24f;
    [Export] public int Level { get; set; } = 0;

    [Export]
    public Vector2[] CurveData
    {
        get => GetCurvePoints();
        set => SetCurvePoints(value);
    }

    public RoadEdge Derive(int startId, int endId, Curve2D curve, int startArm, int endArm)
    {
        return new RoadEdge
        {
            StartNodeId = startId, EndNodeId = endId, Curve = curve, StartArm = startArm, EndArm = endArm,
            LanesPerDirection = LanesPerDirection, LaneWidth = LaneWidth, OneWay = OneWay, Width = Width, Level = Level
        };
    }

    public Vector2[] GetCurvePoints()
    {
        if (Curve == null) return [];
        Vector2[] data = new Vector2[Curve.PointCount * 3];
        for (int i = 0; i < Curve.PointCount; i++)
        {
            data[i * 3] = Curve.GetPointPosition(i);
            data[i * 3 + 1] = Curve.GetPointIn(i);
            data[i * 3 + 2] = Curve.GetPointOut(i);
        }
        return data;
    }

    public void SetCurvePoints(Vector2[] value)
    {
        Curve = new Curve2D();
        if (value == null) return;
        for (int i = 0; i + 2 < value.Length; i += 3) Curve.AddPoint(value[i], value[i + 1], value[i + 2]);
    }
}
