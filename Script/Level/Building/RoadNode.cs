using Godot;

namespace JustCity.Script.Level.Building;

[GlobalClass]
public partial class RoadNode : Resource
{
    [Export] public int Id { get; set; }
    [Export] public Vector2 Position { get; set; }
    [Export] public int Level { get; set; }

    public RoadNode();
}
