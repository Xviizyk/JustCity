using Godot;

namespace JustCity.Script.Level.Building.Road.Graph;

public class RoadSnap
{
    public Vector2 Position;
    public Vector2 Foot;
    public Vector2 Direction;
    public RoadJunction Node;
    public RoadEdge Edge;
    public bool Free;
    public int Lane = -1;
    public int Flow;

    public RoadSnap(Vector2 position)
    {
        Position = position;
        Foot = position;
        Free = true;
    }

    public RoadSnap(Vector2 position, RoadJunction node, RoadEdge edge, Vector2 direction)
    {
        Position = position;
        Foot = position;
        Direction = direction;
        Node = node;
        Edge = edge;
        Free = node == null && edge == null;
    }
}
