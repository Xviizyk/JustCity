using Godot;

namespace JustCity.Script.Level.Building.Buildings;

public partial class Housing : Node
{
    [ExportGroup("Housing")]
    [Export] public Structure Data { get; set; }

    public override void _Process(double delta)
    {
        
    }
}