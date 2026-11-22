using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.Tool;

public class ClearTool(RoadBuilder builder) : RoadTool(builder)
{
    public bool Clearing;
    public Vector2 Start;
    public ClearPlan Plan;
    public List<Vector2> Guides = [];

    private bool dragging;
    private Vector2 pressScreen;

    public static Rect2 RectOf(Vector2 a, Vector2 b)
    {
        return new Rect2(a.Min(b), a.Max(b) - a.Min(b));
    }

    public RoadSnap Snap(Vector2 mouse)
    {
        Guides.Clear();
        if (Builder.Alt) return new RoadSnap(mouse);

        float zoom = Builder.Zoom;
        Vector2 point = Builder.GridSize > 0f ? Builder.Grid(mouse) : mouse;
        float bestX = 8f, bestY = 8f;
        RoadJunction nodeX = null, nodeY = null;
        foreach (RoadJunction n in Builder.Network.Nodes)
        {
            if (n.Level != Builder.Level) continue;
            if (Mathf.Abs(n.Position.X - mouse.X) * zoom < bestX)
            {
                bestX = Mathf.Abs(n.Position.X - mouse.X) * zoom;
                nodeX = n;
            }
            if (Mathf.Abs(n.Position.Y - mouse.Y) * zoom < bestY)
            {
                bestY = Mathf.Abs(n.Position.Y - mouse.Y) * zoom;
                nodeY = n;
            }
        }

        bool snappedX = nodeX != null, snappedY = nodeY != null;
        if (snappedX)
        {
            point.X = nodeX.Position.X;
            Builder.SnapKeyNow = "cx" + nodeX.Id;
        }
        if (snappedY)
        {
            point.Y = nodeY.Position.Y;
            Builder.SnapKeyNow += "cy" + nodeY.Id;
        }

        if (Clearing)
        {
            if (!snappedX && Mathf.Abs(Start.X - mouse.X) * zoom < 8f)
            {
                point.X = Start.X;
                snappedX = true;
            }
            if (!snappedY && Mathf.Abs(Start.Y - mouse.Y) * zoom < 8f)
            {
                point.Y = Start.Y;
                snappedY = true;
            }
            float width = mouse.X - Start.X, height = mouse.Y - Start.Y;
            if (!snappedX && !snappedY && Mathf.Abs(Mathf.Abs(width) - Mathf.Abs(height)) * zoom < 8f)
            {
                float side = (Mathf.Abs(width) + Mathf.Abs(height)) * 0.5f;
                point = Start + new Vector2(Mathf.Sign(width), Mathf.Sign(height)) * side;
                Builder.SnapKeyNow = "square";
            }
        }

        if (nodeX != null)
        {
            Guides.Add(nodeX.Position);
            Guides.Add(point);
        }
        if (nodeY != null)
        {
            Guides.Add(nodeY.Position);
            Guides.Add(point);
        }
        return new RoadSnap(point);
    }

    public override void Pressed()
    {
        if (Clearing)
        {
            Commit();
            return;
        }
        Clearing = true;
        dragging = true;
        Start = Builder.Hover.Position;
        pressScreen = Builder.GetViewport().GetMousePosition();
        Plan = null;
    }

    public override void Released()
    {
        if (Clearing && dragging && Builder.GetViewport().GetMousePosition().DistanceTo(pressScreen) > 12f) Commit();
        dragging = false;
    }

    public override bool Cancel()
    {
        bool was = Clearing;
        Clearing = false;
        dragging = false;
        Plan = null;
        return was;
    }

    public override void Update(Vector2 mouse, float delta, bool moved)
    {
        if (!moved) return;
        Rect2 rect = RectOf(Start, Builder.Hover.Position);
        bool tiny = rect.Size.X < 2f / Builder.Zoom && rect.Size.Y < 2f / Builder.Zoom;
        Plan = Clearing && !tiny ? ClearPlan.Make(Builder.Network, rect, Builder.Level, Builder.ClearCostPerPixel, Builder.BridgeCostMultiplier) : null;
    }

    private void Commit()
    {
        ClearPlan plan = ClearPlan.Make(Builder.Network, RectOf(Start, Builder.Hover.Position), Builder.Level, Builder.ClearCostPerPixel, Builder.BridgeCostMultiplier);
        RoadEdit edit = plan.Items.Count > 0 && Builder.Data.Balance >= plan.Cost ? Builder.Network.Clear(plan) : null;
        if (edit == null)
        {
            Builder.Deny();
            return;
        }
        Builder.Data.Balance -= plan.Cost;
        Builder.History.Add(edit, plan.Cost, null, Vector2.Zero, null);
        Cancel();
    }
}
