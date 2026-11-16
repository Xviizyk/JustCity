using Godot;

namespace JustCity.Script.Level.Building;

[GlobalClass]
public partial class RoadSegment : Resource
{
	[ExportGroup("Graph")]
	[Export] public int StartNodeId { get; set; }
	[Export] public int EndNodeId { get; set; }
	[Export] public int StartArm { get; set; }
	[Export] public int EndArm { get; set; }
	public Curve2D Curve { get; set; } = new();

	[Export] public Vector2[] CurveData
	{
		get
		{
			var a = new Vector2[Curve.PointCount * 3];
			for (int i = 0; i < Curve.PointCount; i++)
			{
				a[i * 3] = Curve.GetPointPosition(i);
				a[i * 3 + 1] = Curve.GetPointIn(i);
				a[i * 3 + 2] = Curve.GetPointOut(i);
			}
			return a;
		}
		set
		{
			Curve = new Curve2D();
			for (int i = 0; i + 2 < value.Length; i += 3)
				Curve.AddPoint(value[i], value[i + 1], value[i + 2]);
		}
	}

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

	public RoadSegment Derive(int startId, int endId, Curve2D curve, int startArm = 0, int endArm = 0) => new()
	{
		StartNodeId = startId,
		EndNodeId = endId,
		Curve = curve,
		StartArm = startArm,
		EndArm = endArm,
		LanesPerDirection = LanesPerDirection,
		LaneWidth = LaneWidth,
		Level = Level
	};
}
