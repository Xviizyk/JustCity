using Godot;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class SaveData : Resource
{
    [Export] public Version Version { get; set; } = new Version();
    [Export] public GameTime PlayTime { get; set; } = new GameTime();
    [Export] public Vector2 Position { get; set; } = new Vector2(0, 0);
    [Export] public Vector2 Zoom { get; set; } = new Vector2(1, 1);
    [Export] public float Balance { get; set; } = 0.0f;
    [Export] public RoadGraph RoadNetwork { get; set; } = new RoadGraph();
    [Export] public Seed Seed { get; set; } = new Seed();

    public SaveData(Version version, GameTime playtime, Vector2 position, Vector2 zoom, float balance, RoadGraph roadNetwork, Seed seed)
    {
        Version = version;
        PlayTime = playtime;
        Position = position;
        Zoom = zoom;
        Balance = balance;
        RoadNetwork = roadNetwork ?? new RoadGraph();
        Seed = seed;
    }

    public SaveData() {}
}