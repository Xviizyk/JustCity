using Godot;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class Seed : Resource
{
    [Export] public ulong Value { get; set; }

    public Seed()
    {
        Value = (ulong)GD.RandRange(0L, long.MaxValue);
    }

    public Seed(ulong seed)
    {
        Value = seed;
    }
}