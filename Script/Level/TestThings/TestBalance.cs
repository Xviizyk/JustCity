using Godot;

namespace JustCity.Script.Level.TestThings;

public partial class TestBalance : Node
{
    public override void _Process(double delta)
    {
        Core.Core.Instance.CurrentSaveData.Balance += 1;
    }
}