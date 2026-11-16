using Godot;
using JustCity.Script.Level.Building;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class SaveData : Resource
{
    [Export] public Version Version { get; private set; } = new Version();
    [Export] public GameTime PlayTime { get; private set; } = new GameTime();
    [Export] public Vector2 Position { get; set; } = new Vector2(0, 0);
    [Export] public Vector2 Zoom { get; set; } = new Vector2(1, 1);
    [Export] public float Balance { get; set; } = 0.0f;
    [Export] public RoadNetwork RoadNetwork { get; set; } = new RoadNetwork();

    public SaveData(Version version, GameTime playtime, Vector2 position, Vector2 zoom, float balance, RoadNetwork roadNetwork)
    {
        Version = version;
        PlayTime = playtime;
        Position = position;
        Zoom = zoom;
        Balance = balance;
        RoadNetwork = roadNetwork ?? new RoadNetwork();
    }

    public SaveData() {}
}
