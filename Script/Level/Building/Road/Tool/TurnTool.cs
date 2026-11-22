using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.Tool;

public class TurnTool(RoadBuilder builder) : RoadTool(builder)
{
    public class Marker
    {
        public Arm Arm;
        public int Lane;
        public bool Incoming;
        public Vector2 Position;
        public Vector2 Dir;
    }

    public RoadJunction Node;
    public RoadJunction HoverNode;
    public int FromArm = -1;
    public int FromLane = -1;
    public string Message;
    public float MessageTime;
    public int HoverMarker = -1;
    public List<Marker> Markers = [];
    public List<Link> Links = [];

    public override void Update(Vector2 mouse, float delta, bool moved)
    {
        MessageTime = Mathf.Max(0f, MessageTime - delta);
        HoverMarker = -1;
        HoverNode = null;
        float zoom = Builder.Zoom;
        if (Node != null && !Builder.Network.Nodes.Contains(Node))
        {
            Node = null;
            FromArm = -1;
        }

        if (Node != null)
        {
            Refresh();
            float best = 14f;
            for (int i = 0; i < Markers.Count; i++)
            {
                if (Markers[i].Position.DistanceTo(mouse) * zoom < best)
                {
                    best = Markers[i].Position.DistanceTo(mouse) * zoom;
                    HoverMarker = i;
                }
            }
            if (FromArm >= 0 && Find(FromArm, FromLane, true) == null) FromArm = -1;
        }
        if (HoverMarker >= 0) return;

        float nearest = 18f;
        foreach (RoadJunction n in Builder.Network.Nodes)
        {
            if (n.Level == Builder.Level && n.Position.DistanceTo(mouse) * zoom < nearest)
            {
                nearest = n.Position.DistanceTo(mouse) * zoom;
                HoverNode = n;
            }
        }
        if (HoverNode != null && Builder.Network.Links.ArmsOf(HoverNode).Count < 2) HoverNode = null;
    }

    public override void Pressed()
    {
        if (HoverMarker >= 0) Click(Markers[HoverMarker]);
        else if (HoverNode == null) Cancel();
        else
        {
            Node = HoverNode;
            FromArm = -1;
        }
    }

    private void Click(Marker m)
    {
        if (m.Incoming)
        {
            bool same = m.Arm.Id == FromArm && m.Lane == FromLane;
            FromArm = same ? -1 : m.Arm.Id;
            FromLane = same ? -1 : m.Lane;
        }
        else if (FromArm < 0) Fail("ROAD_LINK_PICK", 1.6f);
        else
        {
            RoadEdit edit = Builder.Network.Links.TryToggle(Node, new Link(FromArm, FromLane, m.Arm.Id, m.Lane), out string reason);
            if (edit == null) Fail(reason, 2.2f);
            else Builder.History.Add(edit, 0f, null, Vector2.Zero, null);
        }
    }

    private void Fail(string key, float seconds)
    {
        Message = key;
        MessageTime = seconds;
        Builder.Deny();
    }

    public override bool Cancel()
    {
        if (FromArm >= 0)
        {
            FromArm = -1;
            return true;
        }
        bool was = Node != null;
        Node = null;
        return was;
    }

    public void Reset()
    {
        RoadEdit edit = Node == null ? null : Builder.Network.Links.Reset(Node);
        if (edit == null) return;
        Builder.History.Add(edit, 0f, null, Vector2.Zero, null);
        FromArm = -1;
    }

    public Marker Find(int armId, int lane, bool incoming)
    {
        foreach (Marker m in Markers) if (m.Arm.Id == armId && m.Lane == lane && m.Incoming == incoming) return m;
        return null;
    }

    private void Refresh()
    {
        Markers.Clear();
        List<Arm> arms = Builder.Network.Links.ArmsOf(Node);
        float radius = 14f;
        foreach (Arm a in arms) radius = Mathf.Max(radius, a.Half + 14f);

        foreach (Arm a in arms) for (int i = 0; i < 2; i++) for (int lane = 0; lane < (i == 0 ? a.In : a.Exit); lane++) Markers.Add(new Marker { Arm = a, Lane = lane, Incoming = i == 0, Position = a.LanePoint(lane, i == 0) + a.Out * radius, Dir = i == 0 ? -a.Out : a.Out });
        Links = RoadLinks.LinksOf(Node);
    }
}
