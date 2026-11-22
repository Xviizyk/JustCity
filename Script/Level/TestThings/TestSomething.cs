using Godot;
using JustCity.Script.Core;
using JustCity.Script.Level.Landscape;

namespace JustCity.Script.Level.TestThings;

public partial class TestSomething : Node
{
    [Export] public MapGenerator map;

    public override void _Ready()
    {
        map?.GenerateOnce(new Vector2(0, 0));
    }

    public override void _Process(double delta)
    {
        if (this.IsDebug()) this.CurrentSaveData().Balance += 1;
    }
}
