using Godot;
using JustCity.Script.Level.Building.Road.Geometry;

namespace JustCity.Script.Level.Building.Road.Graph;

public class Arm
{
    public RoadEdge Edge;
    public bool AtEnd;
    public int Id;
    public Vector2 Origin;
    public Vector2 Out;
    public float Half;
    public float Reach;
    public float Straight;
    public int In;
    public int Exit;

    public Arm(RoadEdge edge, bool atEnd)
    {
        Edge = edge;
        AtEnd = atEnd;
        Id = atEnd ? edge.EndArm : edge.StartArm;
        Origin = edge.Curve.GetPointPosition(atEnd ? edge.Curve.PointCount - 1 : 0);
        Out = atEnd ? -RoadGeometry.EndTangent(edge.Curve) : RoadGeometry.StartTangent(edge.Curve);
        Half = edge.Width * 0.5f;
        Reach = edge.Curve.GetBakedLength();
        Straight = StraightLength(edge.Curve, atEnd, Origin, Out, 0.5f);
        In = edge.OneWay && !atEnd ? 0 : edge.LanesPerDirection;
        Exit = edge.OneWay && atEnd ? 0 : edge.LanesPerDirection;
    }

    public Arm(Vector2 origin, Vector2 direction, float half, float reach)
    {
        Id = -1;
        Origin = origin;
        Out = direction;
        Half = half;
        Reach = reach;
        Straight = reach;
    }

    private static float StraightLength(Curve2D c, bool atEnd, Vector2 origin, Vector2 dir, float tol)
    {
        float len = c.GetBakedLength();
        Vector2 side = RoadGeometry.Left(dir);
        const float step = 2f;
        for (float s = step; s < len; s += step)
        {
            Vector2 p = c.SampleBaked(atEnd ? len - s : s);
            if (Mathf.Abs((p - origin).Dot(side)) > tol) return s - step;
        }
        return len;
    }

    public bool Covers(Vector2 p)
    {
        Vector2 d = p - Origin;
        return Mathf.Abs(d.Dot(Out)) < 1.5f && Mathf.Abs(d.Dot(RoadGeometry.Left(Out))) <= Half + 1.5f;
    }

    public Vector2 LanePoint(int lane, bool incoming)
    {
        float width = Edge.LaneWidth;
        float shift = Edge.OneWay ? (lane - ((incoming ? In : Exit) - 1) / 2f) * width : (lane + 0.5f) * width;
        Vector2 side = RoadGeometry.Left(Out);
        return Origin + (incoming ? -side : side) * shift;
    }
}