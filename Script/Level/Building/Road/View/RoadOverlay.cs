using System.Collections.Generic;
using Godot;
using JustCity.Script.Core.Utils;
using JustCity.Script.Level.Building.Road.Geometry;
using JustCity.Script.Level.Building.Road.Graph;
using JustCity.Script.Level.Building.Road.Tool;

namespace JustCity.Script.Level.Building.Road.View;

public partial class RoadOverlay : Node2D
{
    [ExportGroup("Style")]
    [Export] public Color Accent = new(0.20f, 0.45f, 0.95f);
    [Export] public Color Ink = new(0.17f, 0.18f, 0.20f);
    [Export] public Color Bad = new(0.90f, 0.35f, 0.30f);

    [ExportGroup("Nodes")]
    [Export] public RoadBuilder Builder;
    [Export] public Label InfoLabel;
    [Export] public Label ErrorLabel;
    [Export] public Label LevelLabel;
    [Export] public Label HintLabel;

    private float px = 1f;

    public override void _Process(double delta)
    {
        if (Builder == null || !Builder.Active) HideLabels();
    }

    public override void _Draw()
    {
        HideLabels();
        if (Builder == null || !Builder.Active || Builder.Build == null) return;
        px = 1f / Builder.Zoom;
        if (Builder.Mode == BuildMode.Clear) DrawClear();
        else if (Builder.Mode == BuildMode.Parallel) DrawParallel();
        else if (Builder.Mode == BuildMode.Turn) DrawTurns();
        else DrawBuild();
    }

    private void DrawBuild()
    {
        BuildTool build = Builder.Build;
        RoadSnap hover = Builder.Hover;
        if (hover.Edge != null) DrawHoveredRoad(hover);
        if (hover.Node != null) DrawLaneEnds(hover);
        if (!build.Placing && !hover.Free)
        {
            List<Vector2> dirs = Builder.Network.StartDirections(hover);
            for (int i = 0; i < dirs.Count; i++) Arrow(hover.Position, dirs[i], Alpha(Accent, i < dirs.Count / 3 ? 0.85f : 0.3f));
        }
        DrawGuide();
        if (build.Placing)
        {
            DrawCircle(build.Start.Position, 3f * px, Accent);
            if (build.Dir != Vector2.Zero && !Builder.Shift) DrawLine(build.Start.Position, build.Start.Position + build.Dir * 16f * px, Alpha(Accent, 0.6f), 1.5f * px, true);
        }
        if (build.Placing && build.Curve != null) DrawPreview(build);
        DrawCursor(build);
        DrawBuildTexts(build);
    }

    private void DrawHoveredRoad(RoadSnap hover)
    {
        Vector2[] pts = Builder.Network.Poly(hover.Edge);
        DrawPolyline(pts, Alpha(Accent, 0.22f), hover.Edge.Width, true);
        if (hover.Lane < 0) return;

        RoadGeometry.Nearest(pts, hover.Foot, out float offset, out _);
        float lateral = (hover.Position - hover.Foot).Dot(RoadGeometry.Left(RoadGeometry.TangentAt(pts, offset)));
        DrawPolyline(RoadGeometry.Offset(pts, lateral), Alpha(Accent, 0.4f), hover.Edge.LaneWidth, true);
    }

    private void DrawLaneEnds(RoadSnap hover)
    {
        foreach (RoadGraph.LaneEnd end in RoadGraph.FreeLaneEnds(Builder.Network.Links.ArmsOf(hover.Node)))
        {
            bool chosen = hover.Lane == end.Lane && (hover.Flow > 0) == end.Incoming;
            DrawArc(end.Position, (chosen ? 5f : 3f) * px, 0f, Mathf.Tau, 16, chosen ? Accent : Alpha(Ink, 0.5f), 1.5f * px, true);
        }
    }

    private void DrawGuide()
    {
        RoadSnapper s = Builder.Snapper;
        Color guide = Alpha(Accent, 0.5f);
        if (s.Kind == GuideKind.Ray) DrawDashedLine(s.GuideA, Builder.Hover.Position + s.GuideB * 30f * px, Alpha(Ink, 0.25f), px, 6f * px);
        else if (s.Kind == GuideKind.Angle) DrawDashedLine(s.GuideA, s.GuideB, Alpha(Ink, 0.2f), px, 6f * px);
        else if (s.Kind == GuideKind.Align)
        {
            DrawDashedLine(s.GuideA, s.GuideB, guide, px, 5f * px);
            DrawCircle(s.GuideA, 2.5f * px, guide);
        }
        else if (s.Kind == GuideKind.Rhythm)
        {
            Vector2 across = RoadGeometry.Left(s.GuideB) * 8f * px;
            DrawLine(s.GuideA - across, s.GuideA + across, guide, 1.5f * px, true);
        }
        else if (s.Kind == GuideKind.Perp)
        {
            Vector2 incoming = (s.GuideB - s.GuideA).Normalized(), side = RoadGeometry.Left(incoming);
            float size = 8f * px;
            DrawPolyline([s.GuideB - incoming * size, s.GuideB - incoming * size + side * size, s.GuideB + side * size], guide, 1.5f * px, true);
        }
    }

    private void DrawPreview(BuildTool build)
    {
        Color color = Accent.Lerp(Bad, Builder.BadAmount);
        float half = build.Lanes * Builder.LaneWidth * (build.OneWay ? 0.5f : 1f);
        DrawPolyline(RoadGeometry.Poly(build.Shown ?? build.Curve), Alpha(color, build.Clamped ? 0.28f : 0.45f), half * 2f, true);

        float length = build.Curve.GetBakedLength();
        List<Vector2> tris = [];
        if (!build.Start.Free) Ghost(build.Start, new Arm(build.Curve.GetPointPosition(0), RoadGeometry.StartTangent(build.Curve), half, length), tris);
        if (!Builder.Hover.Free) Ghost(Builder.Hover, new Arm(build.Curve.GetPointPosition(build.Curve.PointCount - 1), -RoadGeometry.EndTangent(build.Curve), half, length), tris);
        RoadCorners.Draw(this, tris, Alpha(color, 0.45f), Vector2.Zero);

        Cuts(build.Cuts, Accent);
        if (build.Clamped)
        {
            Vector2 real = build.Curve.GetPointPosition(build.Curve.PointCount - 1);
            DrawDashedLine(real, Builder.Hover.Position, Alpha(Ink, 0.3f), px, 5f * px);
            DrawCircle(real, 3f * px, color);
        }
        if (build.Reason != null) Cross(build.At);
    }

    private void Ghost(RoadSnap snap, Arm mine, List<Vector2> tris)
    {
        List<Arm> arms = Builder.Network.ArmsAtPoint(snap.Foot, Builder.Level);
        if (arms.Count == 0) return;
        arms.Add(mine);
        RoadCorners.Build(snap.Foot, arms, Builder.Renderer.FilletRadius, tris, null);
    }

    private void DrawCursor(BuildTool build)
    {
        RoadSnap hover = Builder.Hover;
        bool denied = build.Placing && build.Curve != null && !build.Valid;
        Color ring = Alpha(Ink, 0.4f);
        if (denied) ring = Bad;
        else if (!hover.Free) ring = Accent;

        Vector2 at = Builder.RingPosition;
        if (hover.Node != null) DrawArc(at, Builder.RingRadius * px, 0f, Mathf.Tau, 28, ring, 1.5f * px, true);
        else if (hover.Edge != null)
        {
            DrawArc(at, 7f * px, 0f, Mathf.Tau, 24, ring, 1.5f * px, true);
            DrawCircle(at, 2.5f * px, ring);
        }
        else DrawCircle(at, 3f * px, ring);

        if (denied) DrawLine(at + new Vector2(-6f, 6f) * px, at + new Vector2(6f, -6f) * px, Bad, 1.5f * px, true);
    }

    private void DrawBuildTexts(BuildTool build)
    {
        float shake = Builder.Shake > 0f ? Mathf.Sin(Builder.Shake * 60f) * 4f * (Builder.Shake / 0.25f) : 0f;
        if (Builder.Level > 0) ShowText(LevelLabel, "L" + Builder.Level, Alpha(Ink, 0.6f), MouseOnScreen() + new Vector2(18f, -26f));

        string target = Target(Builder.Hover);
        if (build.Placing && build.Curve != null)
        {
            string info = "$" + Builder.CostOf(build.Curve, build.Lanes, build.OneWay).ToString("0") + "  ·  " + build.Curve.GetBakedLength().ToString("0");
            if (build.Turn > 0.01f) info += "  ·  " + Mathf.RadToDeg(build.Turn).ToString("0") + "°";
            info += "  ·  " + build.Lanes + (build.OneWay ? "x1" : "x2");
            if (target != "") info += "  ·  " + target;
            CursorText(info, Ink, 0, shake);
            if (build.Reason != null) CursorText(Utils.Tr(build.Reason), Bad, 1, shake);
        }
        else if (!build.Placing)
        {
            if (target != "") CursorText(target, Alpha(Ink, 0.6f), 0, shake);
            else if (Builder.LanesPerDirection > 1) CursorText(Builder.LanesPerDirection + "x2", Alpha(Ink, 0.45f), 0, shake);
        }
    }

    private string Target(RoadSnap hover)
    {
        if (hover.Lane >= 0) return Utils.Tr("ROAD_SNAP_LANE") + " " + (hover.Lane + 1);
        if (hover.Node != null) return Builder.Network.Links.ArmsOf(hover.Node).Count == 1 ? Utils.Tr("ROAD_SNAP_END") : Utils.Tr("ROAD_SNAP_NODE");
        if (hover.Edge != null) return Utils.Tr("ROAD_SNAP_ROAD");
        return "";
    }

    private void DrawClear()
    {
        ClearTool clear = Builder.Clear;
        Vector2 hover = Builder.Hover.Position;
        for (int i = 0; i + 1 < clear.Guides.Count; i += 2)
        {
            DrawDashedLine(clear.Guides[i], clear.Guides[i + 1], Alpha(Accent, 0.5f), px, 5f * px);
            DrawCircle(clear.Guides[i], 2.5f * px, Alpha(Accent, 0.5f));
        }

        ClearPlan plan = clear.Plan;
        if (clear.Clearing)
        {
            Rect2 rect = ClearTool.RectOf(clear.Start, hover);
            DrawRect(rect, Alpha(Bad, 0.07f), true);
            DrawRect(rect, Alpha(Bad, 0.7f), false, 1.5f * px);
            if (plan != null)
            {
                foreach (ClearPlan.Item item in plan.Items) for (int i = 0; i < item.Inside.Count; i++) if (item.Inside[i]) DrawPolyline(RoadGeometry.Sub(item.Pts, item.Offsets[i], item.Offsets[i + 1]), Alpha(Bad, 0.45f), item.Edge.Width, true);
                Cuts(plan.Cuts, Bad);
            }
        }
        DrawLine(hover + new Vector2(-6f, 0f) * px, hover + new Vector2(6f, 0f) * px, Bad, 1.5f * px, true);
        DrawLine(hover + new Vector2(0f, -6f) * px, hover + new Vector2(0f, 6f) * px, Bad, 1.5f * px, true);

        if (!clear.Clearing) CursorText(Utils.Tr("ROAD_MODE_CLEAR"), Ink);
        else if (plan != null && plan.Items.Count > 0)
        {
            CursorText("$" + plan.Cost.ToString("0") + "  ·  " + plan.Length.ToString("0"), Ink);
            if (Builder.Data.Balance < plan.Cost) CursorText(Utils.Tr("ROAD_ERR_MONEY"), Bad, 1);
        }
        else if (plan != null) CursorText(Utils.Tr("ROAD_CLEAR_EMPTY"), Bad);
        ShowText(HintLabel, Utils.Tr("ROAD_CLEAR_HINT"), Alpha(Ink, 0.45f), HintPosition());
    }

    private void DrawParallel()
    {
        ParallelTool parallel = Builder.Parallel;
        RoadEdge shown = parallel.Reference ?? parallel.Hovered;
        if (shown != null) DrawPolyline(Builder.Network.Poly(shown), Alpha(Accent, 0.28f), shown.Width, true);

        if (parallel.Reference != null && parallel.Curve != null)
        {
            float width = Builder.LanesPerDirection * Builder.LaneWidth * (parallel.Reference.OneWay ? 1f : 2f);
            DrawPolyline(RoadGeometry.Poly(parallel.Curve), Alpha(Accent.Lerp(Bad, Builder.BadAmount), 0.45f), width, true);
            Cuts(parallel.Cuts, Accent);
            if (parallel.Reason != null) Cross(parallel.At);
        }
        MouseDot();

        if (parallel.Reference == null) CursorText(Utils.Tr("ROAD_PAR_PICK"), Ink);
        else if (parallel.Curve != null)
        {
            CursorText("$" + Builder.CostOf(parallel.Curve, Builder.LanesPerDirection, parallel.Reference.OneWay).ToString("0") + "  ·  " + parallel.Gap.ToString("0"), Ink);
            if (parallel.Reason != null) CursorText(Utils.Tr(parallel.Reason), Bad, 1);
        }
        ShowText(HintLabel, Utils.Tr("ROAD_PAR_HINT"), Alpha(Ink, 0.45f), HintPosition());
    }

    private void DrawTurns()
    {
        TurnTool turn = Builder.Turn;
        if (turn.HoverNode != null && turn.HoverNode != turn.Node) DrawArc(turn.HoverNode.Position, 14f * px, 0f, Mathf.Tau, 28, Alpha(Accent, 0.8f), 1.5f * px, true);
        if (turn.Node != null)
        {
            bool picked = turn.FromArm >= 0;
            float ring = 14f;
            foreach (TurnTool.Marker m in turn.Markers) ring = Mathf.Max(ring, m.Position.DistanceTo(turn.Node.Position) + 6f);
            DrawArc(turn.Node.Position, ring, 0f, Mathf.Tau, 48, Alpha(Accent, 0.25f), px, true);

            foreach (Link link in turn.Links)
            {
                TurnTool.Marker a = turn.Find(link.FromArm, link.FromLane, true), b = turn.Find(link.ToArm, link.ToLane, false);
                if (a == null || b == null) continue;
                if (link.FromArm == turn.FromArm && link.FromLane == turn.FromLane) DrawLink(a, b, Alpha(Accent, 0.95f), 2.5f * px);
                else DrawLink(a, b, Alpha(Ink, picked ? 0.15f : 0.4f), 2f * px);
            }
            for (int i = 0; i < turn.Markers.Count; i++) DrawMarker(turn, turn.Markers[i], i == turn.HoverMarker, picked);
        }
        MouseDot();

        if (turn.MessageTime > 0f && turn.Message != null) CursorText(Utils.Tr(turn.Message), Bad);
        else if (turn.Node == null) CursorText(Utils.Tr(turn.HoverNode != null ? "ROAD_TURN_CLICK" : "ROAD_TURN_MODE"), Ink);
        else if (turn.HoverMarker >= 0)
        {
            TurnTool.Marker m = turn.Markers[turn.HoverMarker];
            if (m.Incoming) CursorText(Utils.Tr("ROAD_TURN_PICK_LANE"), Ink);
            else if (turn.FromArm >= 0) CursorText(Utils.Tr(turn.Links.Contains(new Link(turn.FromArm, turn.FromLane, m.Arm.Id, m.Lane)) ? "ROAD_TURN_REMOVE" : "ROAD_TURN_ADD"), Ink);
        }
        ShowText(HintLabel, Utils.Tr("ROAD_TURN_HINT"), Alpha(Ink, 0.45f), HintPosition());
    }

    private void DrawMarker(TurnTool turn, TurnTool.Marker m, bool hovered, bool picked)
    {
        if (m.Incoming)
        {
            Vector2 side = RoadGeometry.Left(m.Dir);
            Color color = hovered || (m.Arm.Id == turn.FromArm && m.Lane == turn.FromLane) ? Accent : Alpha(Ink, 0.7f);
            DrawColoredPolygon([m.Position + m.Dir * 5f * px, m.Position - m.Dir * 3f * px + side * 4f * px, m.Position - m.Dir * 3f * px - side * 4f * px], color);
        }
        else if (picked && turn.Links.Contains(new Link(turn.FromArm, turn.FromLane, m.Arm.Id, m.Lane))) DrawCircle(m.Position, 4.5f * px, Accent);
        else
        {
            bool sameArm = picked && m.Arm.Id == turn.FromArm;
            DrawArc(m.Position, 4f * px, 0f, Mathf.Tau, 16, hovered ? Accent : Alpha(Ink, sameArm ? 0.2f : 0.7f), 1.5f * px, true);
        }
    }

    private void DrawLink(TurnTool.Marker a, TurnTool.Marker b, Color color, float width)
    {
        float handle = Mathf.Max(10f, a.Position.DistanceTo(b.Position) * 0.45f);
        Vector2[] pts = new Vector2[17];
        for (int i = 0; i <= 16; i++) pts[i] = a.Position.BezierInterpolate(a.Position + a.Dir * handle, b.Position - b.Dir * handle, b.Position, i / 16f);
        DrawPolyline(pts, color, width, true);
    }

    private static Color Alpha(Color c, float a)
    {
        return new Color(c.R, c.G, c.B, a);
    }

    private void Arrow(Vector2 at, Vector2 dir, Color color)
    {
        Vector2 tip = at + dir * 34f * px, back = tip - dir * 6f * px, side = RoadGeometry.Left(dir) * 4f * px;
        DrawLine(at + dir * 14f * px, tip, color, 1.5f * px, true);
        DrawLine(tip, back + side, color, 1.5f * px, true);
        DrawLine(tip, back - side, color, 1.5f * px, true);
    }

    private void Cross(Vector2 at)
    {
        DrawLine(at + new Vector2(-5f, -5f) * px, at + new Vector2(5f, 5f) * px, Bad, 1.5f * px, true);
        DrawLine(at + new Vector2(-5f, 5f) * px, at + new Vector2(5f, -5f) * px, Bad, 1.5f * px, true);
    }

    private void Cuts(List<Vector2> cuts, Color inner)
    {
        foreach (Vector2 c in cuts)
        {
            DrawCircle(c, 5f * px, Colors.White);
            DrawCircle(c, 3f * px, inner);
        }
    }

    private void MouseDot()
    {
        DrawCircle(GetLocalMousePosition(), 3f * px, Alpha(Ink, 0.4f));
    }

    private Vector2 MouseOnScreen()
    {
        return GetViewport().GetMousePosition();
    }

    private Vector2 HintPosition()
    {
        return new Vector2(16f, GetViewport().GetVisibleRect().Size.Y - 28f);
    }

    private void CursorText(string text, Color color, int line = 0, float shake = 0f)
    {
        ShowText(line == 0 ? InfoLabel : ErrorLabel, text, color, MouseOnScreen() + new Vector2(18f + shake, 22f + 20f * line));
    }

    private void HideLabels()
    {
        foreach (Label l in new Label[] { InfoLabel, ErrorLabel, LevelLabel, HintLabel }) l?.Visible = false;
    }

    private void ShowText(Label label, string text, Color color, Vector2 screen)
    {
        if (label != null)
        {
            label.Text = text;
            label.Modulate = color;
            label.Position = screen;
            label.Visible = true;
            return;
        }
        Font font = ThemeDB.FallbackFont;
        DrawSetTransform(GetGlobalTransformWithCanvas().AffineInverse() * screen, 0f, new Vector2(px, px));
        DrawString(font, new Vector2(0f, font.GetAscent(13)), text, HorizontalAlignment.Left, -1, 13, color);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
