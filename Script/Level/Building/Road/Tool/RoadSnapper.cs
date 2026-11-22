using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Geometry;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.Tool;

public enum GuideKind { None, Ray, Angle, Align, Perp, Rhythm }

public class RoadSnapper(RoadBuilder builder)
{
    private class Candidate
    {
        public Vector2 Position;
        public RoadEdge Edge;
        public GuideKind Kind;
        public Vector2 GuideA;
        public Vector2 GuideB;
        public string Key;
    }

    public GuideKind Kind;
    public Vector2 GuideA;
    public Vector2 GuideB;

    private readonly RoadBuilder builder = builder;
    private Vector2 mouse;
    private float zoom;
    private Candidate best;
    private float bestScore;

    public RoadSnap Find(Vector2 point)
    {
        Kind = GuideKind.None;
        builder.SnapKeyNow = "";
        if (builder.Alt) return new RoadSnap(point);

        mouse = point;
        zoom = builder.Zoom;
        best = null;
        bestScore = float.MaxValue;
        BuildTool build = builder.Build;

        RoadSnap snap = builder.Network.Snap(point, builder.SnapRadius / zoom * (builder.Hover.Node != null ? 1.4f : 1f), builder.Level);
        if (snap.Node != null)
        {
            builder.SnapKeyNow = "n" + snap.Node.Id + "l" + snap.Lane + "f" + snap.Flow;
            return snap;
        }

        if (snap.Edge != null) ConsiderEdge(snap);
        if (build.Placing) ConsiderFromStart();
        ConsiderAlignment();

        if (best == null) return new RoadSnap(!build.Placing && builder.GridSize > 0f ? builder.Grid(point) : point);

        Kind = best.Kind;
        GuideA = best.GuideA;
        GuideB = best.GuideB;
        builder.SnapKeyNow = best.Key;
        if (best.Edge == null) return new RoadSnap(best.Position);
        return builder.Network.OnLane(best.Edge, best.Position, point, builder.LaneSnapMinLanes, build.Placing, build.Start.Position);
    }

    private void Consider(Vector2 position, RoadEdge edge, GuideKind kind, Vector2 guideA, Vector2 guideB, string key, float strength, float maxPixels)
    {
        float pixels = mouse.DistanceTo(position) * zoom;
        if (pixels > maxPixels || pixels / strength >= bestScore) return;
        bestScore = pixels / strength;
        best = new Candidate { Position = position, Edge = edge, Kind = kind, GuideA = guideA, GuideB = guideB, Key = key };
    }

    private void ConsiderEdge(RoadSnap snap)
    {
        RoadEdge edge = snap.Edge;
        string id = edge.GetHashCode().ToString();
        Vector2[] pts = builder.Network.Poly(edge);
        Consider(snap.Position, edge, GuideKind.None, Vector2.Zero, Vector2.Zero, "s" + id, 1f, builder.SnapRadius);

        RoadGeometry.Nearest(pts, snap.Position, out float offset, out _);
        float index = Mathf.Round(offset / builder.RhythmStep);
        float spot = index * builder.RhythmStep;
        if (spot > 12f && spot < RoadGeometry.Length(pts) - 12f)
        {
            Vector2 point = RoadGeometry.PointAt(pts, spot);
            Consider(point, edge, GuideKind.Rhythm, point, RoadGeometry.TangentAt(pts, spot), "h" + id + ":" + index, 1.3f, 9f);
        }

        if (!builder.Build.Placing) return;
        Vector2 start = builder.Build.Start.Position;
        RoadGeometry.Nearest(pts, start, out _, out Vector2 foot);
        if (foot.DistanceTo(start) * zoom > 14f) Consider(foot, edge, GuideKind.Perp, start, foot, "p" + id, 1.4f, 14f);
    }

    private void ConsiderFromStart()
    {
        BuildTool build = builder.Build;
        Vector2 start = build.Start.Position, toMouse = mouse - start;
        float len = toMouse.Length();
        if (len <= 4f / zoom) return;

        BuildType type = build.SnapType();
        if (type != BuildType.Straight && type != BuildType.Smooth) return;
        if (type == BuildType.Smooth)
        {
            Vector2 normal = RoadGeometry.Left(build.Dir);
            float step = Mathf.DegToRad(builder.AngleStepDeg) / 2f;
            float index = Mathf.Round(Mathf.Atan2(toMouse.Dot(normal), toMouse.Dot(build.Dir)) / step);
            Vector2 point = start + (build.Dir * Mathf.Cos(index * step) + normal * Mathf.Sin(index * step)) * len;
            Consider(point, null, GuideKind.Angle, start, point, "t" + (int)index, 1f, builder.SnapPx);
            return;
        }

        float lengthStep = builder.LengthStep;
        while (lengthStep * zoom < 6f) lengthStep *= 2f;
        List<RoadEdge> near = builder.Network.Near(mouse, (builder.SnapPx + 2f) / zoom, builder.Level);
        for (float angle = 0f; angle < Mathf.Tau - 0.01f; angle += Mathf.DegToRad(builder.AngleStepDeg))
            ConsiderRay(start, toMouse, Vector2.FromAngle(angle), 1f, lengthStep, near);
        foreach (Vector2 d in build.Candidates) ConsiderRay(start, toMouse, d, 1.3f, lengthStep, near);
    }

    private void ConsiderRay(Vector2 start, Vector2 toMouse, Vector2 dir, float strength, float lengthStep, List<RoadEdge> near)
    {
        if (Mathf.Abs(Mathf.Wrap(toMouse.Angle() - dir.Angle(), -Mathf.Pi, Mathf.Pi)) > Mathf.DegToRad(20f)) return;
        float along = toMouse.Dot(dir);
        if (along <= 0f) return;

        Vector2 point = start + dir * along;
        Vector2 stepped = start + dir * Mathf.Max(lengthStep, Mathf.Round(along / lengthStep) * lengthStep);
        if (stepped.DistanceTo(point) * zoom < 5f) point = stepped;
        string key = "r" + Mathf.RoundToInt(Mathf.RadToDeg(dir.Angle()) * 10f);
        Consider(point, null, GuideKind.Ray, start, dir, key, strength, builder.SnapPx);

        foreach (RoadEdge e in near)
            if (builder.Network.RayHit(start, dir, e, out Vector2 hit)) Consider(hit, e, GuideKind.Ray, start, dir, key, 1.6f, builder.SnapPx + 2f);
    }

    private void ConsiderAlignment()
    {
        foreach (RoadJunction n in builder.Network.Nodes)
        {
            if (n.Level != builder.Level || n.Position.DistanceTo(mouse) * zoom < 14f) continue;
            if (builder.Build.Placing && n.Position.DistanceTo(builder.Build.Start.Position) < 1f) continue;
            Vector2 sameX = new(n.Position.X, mouse.Y), sameY = new(mouse.X, n.Position.Y);
            Consider(sameX, null, GuideKind.Align, n.Position, sameX, "x" + n.Id, 0.8f, 8f);
            Consider(sameY, null, GuideKind.Align, n.Position, sameY, "y" + n.Id, 0.8f, 8f);
        }
    }
}
