using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Geometry;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.Tool;

public class ParallelTool(RoadBuilder builder) : RoadTool(builder)
{
    public RoadEdge Reference;
    public RoadEdge Hovered;
    public float Gap;
    public Curve2D Curve;
    public bool Valid;
    public string Reason;
    public Vector2 At;
    public List<Vector2> Cuts = [];

    private float side = 1f;

    public override void Pressed()
    {
        if (Reference == null) Reference = Hovered;
        else if (Curve == null || !Valid || !Builder.BuildPaid(Curve, Builder.LanesPerDirection, Reference.OneWay, false, null, null, Vector2.Zero, null)) Builder.Deny();
    }

    public override bool Cancel()
    {
        bool was = Reference != null;
        Reference = null;
        return was;
    }

    public override void Update(Vector2 mouse, float delta, bool moved)
    {
        if (!moved) return;
        Curve = null;
        Valid = false;
        Reason = null;
        Cuts.Clear();
        float zoom = Builder.Zoom;

        if (Reference != null && !Builder.Network.Segments.Contains(Reference)) Reference = null;
        if (Reference == null)
        {
            Hovered = null;
            float nearest = 12f / zoom;
            foreach (RoadEdge e in Builder.Network.Near(mouse, nearest + Builder.MaxLanes * Builder.LaneWidth, Builder.Level))
            {
                float dist = RoadGeometry.Nearest(Builder.Network.Poly(e), mouse, out _, out _) - e.Width * 0.5f;
                if (dist < nearest)
                {
                    nearest = dist;
                    Hovered = e;
                }
            }
            return;
        }

        Vector2[] pts = Builder.Network.Poly(Reference);
        RoadGeometry.Nearest(pts, mouse, out float offset, out Vector2 foot);
        float across = (mouse - foot).Dot(RoadGeometry.Left(RoadGeometry.TangentAt(pts, offset)));
        if (Mathf.Abs(across) >= 4f / zoom) side = across >= 0f ? 1f : -1f;

        float half = Builder.LanesPerDirection * Builder.LaneWidth * (Reference.OneWay ? 0.5f : 1f);
        float minGap = Reference.Width * 0.5f + half + Builder.ParallelMargin;
        float step = Builder.ParallelStep;
        while (step * zoom < 6f) step *= 2f;
        Gap = Mathf.Abs(across) <= minGap ? minGap : minGap + Mathf.Round((Mathf.Abs(across) - minGap) / step) * step;

        Vector2[] shifted = RoadGeometry.Offset(pts, side * Gap);
        if (shifted.Length < 2) return;
        Curve = RoadGeometry.ToCurve(shifted);

        Vector2[] curvePts = RoadGeometry.Poly(Curve);
        Reason = Validate(curvePts);
        Valid = Reason == null;
        Cuts = Builder.Network.PreviewCuts(Curve, Builder.Level);
    }

    private string Validate(Vector2[] pts)
    {
        At = pts[^1];
        float minRadius = RoadGeometry.MinRadius(pts, out Vector2 sharp);
        if (RoadGeometry.Length(pts) < Builder.MinLength)
        {
            At = pts[pts.Length / 2];
            return "ROAD_ERR_SHORT";
        }
        if (minRadius < Builder.MinRadius * 0.9f)
        {
            At = sharp;
            return "ROAD_ERR_SHARP";
        }
        string junction = Builder.Network.JunctionProblem(Curve, Builder.Level, Reference.OneWay, null, null, Builder.MinJunctionAngleDeg - Builder.JunctionAngleTolerance, Builder.MaxJunctionArms, out Vector2 problem);
        if (junction != null)
        {
            At = problem;
            return junction;
        }
        return Builder.Data.Balance < Builder.CostOf(Curve, Builder.LanesPerDirection, Reference.OneWay) ? "ROAD_ERR_MONEY" : null;
    }
}
