using Godot;

namespace JustCity.Script.Level.Building.Road.Graph;

[GlobalClass]
public partial class RoadJunction : Resource
{
    [Export] public int Id { get; set; }
    [Export] public Vector2 Position { get; set; }
    [Export] public int Level { get; set; }
    [Export] public int[] LinkData { get; set; } = [];
    [Export] public bool CustomLinks { get; set; } = false;
}
