using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Geometry;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.Tool;

public class BuildTool(RoadBuilder builder) : RoadTool(builder)
{
    public bool Placing;
    public RoadSnap Start = new(Vector2.Zero);
    public Vector2 Dir;
    public List<Vector2> Candidates = [];
    public int Chain;

    public Curve2D Curve;
    public BuildType Used;
    public bool Valid;
    public bool Clamped;
    public string Reason;
    public float Turn;
    public Vector2 At;
    public bool OneWay;
    public int Lanes = 1;
    public Curve2D Shown;
    public List<Vector2> Cuts = [];

    private Vector2 autoDir;
    private Vector2 smooth;
    private float buffer;
    private bool chainOneWay;
    private bool chainReverse;

    public override void Pressed()
    {
        if (Placing)
        {
            if (!Commit() && Curve != null) buffer = Builder.ClickBuffer;
            return;
        }
        Start = Builder.Hover;
        Candidates = Builder.Network.StartDirections(Start);
        autoDir = Vector2.Zero;
        Dir = Start.Direction;
        Placing = true;
        Chain = 0;
        chainOneWay = false;
        smooth = Start.Position;
        Shown = null;
        Curve = null;
    }

    public override bool Cancel()
    {
        bool was = Placing;
        Placing = false;
        Curve = null;
        Valid = false;
        Shown = null;
        Cuts.Clear();
        buffer = 0f;
        Chain = 0;
        chainOneWay = false;
        return was;
    }

    public override void Update(Vector2 mouse, float delta, bool moved)
    {
        if (Placing)
        {
            if (moved) UpdatePreview();
            smooth = smooth.Lerp(Builder.Hover.Position, 1f - Mathf.Exp(-Builder.PreviewSmoothing * delta));
            if (Curve != null) Shown = smooth.DistanceTo(Builder.Hover.Position) * Builder.Zoom < 0.5f ? Curve : MakeCurve(smooth, Builder.Hover, out _, out _);
        }

        if (buffer > 0f)
        {
            buffer -= delta;
            if (Valid && Commit()) buffer = 0f;
            else if (buffer <= 0f) Builder.Deny();
        }
    }

    public void UpdateDir(Vector2 mouse)
    {
        if (Start.Direction != Vector2.Zero)
        {
            Dir = Start.Direction;
            return;
        }
        Vector2 toMouse = mouse - Start.Position;
        if (toMouse.Length() < 6f / Builder.Zoom) return;

        Vector2 best = Vector2.Zero;
        float bestAngle = Mathf.DegToRad(Builder.AutoDirConeDeg);
        foreach (Vector2 d in Candidates)
        {
            float angle = Mathf.Abs(d.AngleTo(toMouse)) - (d == autoDir ? Mathf.DegToRad(8f) : 0f);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = d;
            }
        }
        autoDir = best;
        Dir = best;
    }

    public BuildType SnapType()
    {
        BuildType type = Builder.Shift ? BuildType.Straight : Builder.Type;
        if (type == BuildType.Smart) type = Dir != Vector2.Zero ? BuildType.Smooth : BuildType.Straight;
        if (type == BuildType.Smooth && Dir == Vector2.Zero) type = BuildType.Straight;
        return type;
    }

    private Curve2D MakeCurve(Vector2 end, RoadSnap target, out bool clamped, out BuildType used)
    {
        clamped = false;
        Vector2 p0 = Start.Position;
        bool nodeEnd = target.Node != null && target.Direction != Vector2.Zero;
        used = Builder.Shift ? BuildType.Straight : Builder.Type;
        if (used == BuildType.Smart)
        {
            used = BuildType.Straight;
            if (Dir != Vector2.Zero)
            {
                RoadGeometry.Arc(p0, Dir, end, Builder.MinRadius, out bool tight);
                used = nodeEnd || !tight ? BuildType.Smooth : BuildType.GridSmooth;
            }
        }
        if (used == BuildType.Smooth && Dir == Vector2.Zero) used = BuildType.Straight;

        if (used == BuildType.Straight) return RoadGeometry.Line(p0, end);
        if (used == BuildType.Circular) return RoadGeometry.Circular(p0, end, Dir);
        if (used == BuildType.Smooth)
        {
            if (nodeEnd) return RoadGeometry.Hermite(p0, Dir, end, -target.Direction);
            return RoadGeometry.Arc(p0, Dir, end, Builder.MinRadius, out clamped);
        }
        List<Vector2> path = RoadGrid.Path(p0, Dir, end, Builder.MinRadius);
        return RoadGeometry.ToCurve(used == BuildType.GridSmooth ? RoadGrid.Round(path, Builder.MinRadius) : [.. path]);
    }

    private void UpdatePreview()
    {
        RoadSnap end = Builder.Hover;
        Curve = null;
        Valid = false;
        Clamped = false;
        Reason = null;
        Turn = 0f;
        Cuts.Clear();
        OneWay = chainOneWay || Start.Lane >= 0 || end.Lane >= 0;
        Lanes = OneWay ? 1 : Builder.LanesPerDirection;
        if (Start.Position.DistanceTo(end.Position) < Builder.DeadZonePx / Builder.Zoom)
        {
            Shown = null;
            return;
        }

        Curve = MakeCurve(end.Position, end, out Clamped, out Used);
        Reason = Validate(end, out At);
        Valid = Reason == null;
        if (Dir != Vector2.Zero) Turn = Mathf.Abs(Dir.AngleTo(RoadGeometry.EndTangent(Curve)));
        Cuts = Builder.Network.PreviewCuts(Curve, Builder.Level);
    }

    private bool Reversed(RoadSnap end, out bool conflict)
    {
        bool reverse = Start.Flow < 0 || end.Flow > 0, forward = Start.Flow > 0 || end.Flow < 0;
        if (chainOneWay)
        {
            reverse = reverse || chainReverse;
            forward = forward || !chainReverse;
        }
        conflict = reverse && forward;
        return reverse;
    }

    private string Validate(RoadSnap end, out Vector2 at)
    {
        Vector2[] pts = RoadGeometry.Poly(Curve);
        at = end.Position;
        bool round = Used == BuildType.Smooth || Used == BuildType.GridSmooth || Used == BuildType.Circular;
        float minRadius = RoadGeometry.MinRadius(pts, out Vector2 sharp);
        if (RoadGeometry.Length(pts) < Builder.MinLength)
        {
            at = pts[pts.Length / 2];
            return "ROAD_ERR_SHORT";
        }
        if (round && minRadius < Builder.MinRadius * 0.9f)
        {
            at = sharp;
            return "ROAD_ERR_SHARP";
        }

        string junction = Builder.Network.JunctionProblem(Curve, Builder.Level, OneWay, Start, end, Builder.MinJunctionAngleDeg - Builder.JunctionAngleTolerance, Builder.MaxJunctionArms, out Vector2 problem);
        if (junction != null)
        {
            at = problem;
            return junction;
        }
        Reversed(end, out bool conflict);
        if (conflict) return "ROAD_ERR_FLOW";
        if (Builder.Data.Balance < Builder.CostOf(Curve, Lanes, OneWay)) return "ROAD_ERR_MONEY";
        return null;
    }

    private bool Commit()
    {
        if (Curve == null || !Valid) return false;
        RoadSnap end = Builder.Hover;
        bool reverse = Reversed(end, out _);
        if (!Builder.BuildPaid(Curve, Lanes, OneWay, reverse, Start, end, Dir, Candidates)) return false;
        if (!end.Free)
        {
            Builder.Cancel();
            return true;
        }

        Vector2 endPoint = Curve.GetPointPosition(Curve.PointCount - 1);
        Dir = RoadGeometry.EndTangent(Curve);
        Start = new RoadSnap(endPoint, null, null, Dir);
        Candidates = [];
        chainOneWay = OneWay;
        chainReverse = reverse;
        Chain++;
        Curve = null;
        Valid = false;
        Shown = null;
        Cuts.Clear();
        smooth = endPoint;
        return true;
    }

    public void RestoreAfterUndo(RoadHistory.Entry e)
    {
        Chain--;
        if (Chain == 0) chainOneWay = false;
        Start = e.Start;
        Dir = e.Dir;
        Candidates = e.Candidates;
        autoDir = Vector2.Zero;
        Curve = null;
        Valid = false;
        Shown = null;
        Cuts.Clear();
        smooth = Start.Position;
    }
}
