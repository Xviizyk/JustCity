using Godot;
using JustCity.Script.Level.Building;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class SaveData : Resource
{
    [Export] public Version Version { get; private set; }
    [Export] public GameTime PlayTime { get; private set; }
    [Export] public Vector2 Position { get; set; }
    [Export] public Vector2 Zoom { get; set; }
    [Export] public float Balance { get; set; }
    [Export] public RoadNetwork RoadNetwork { get; set; }

    public SaveData(Version version, GameTime playtime, Vector2 position, Vector2 zoom, float balance, RoadNetwork roadNetwork)
    {
        Version = version;
        PlayTime = playtime;
        Position = position;
        Zoom = zoom;
        Balance = balance;
        RoadNetwork = roadNetwork ?? new RoadNetwork();
    }

    public SaveData()
    {
        Version = new Version();
        PlayTime = new GameTime();
        Position = new Vector2(0, 0);
        Zoom = new Vector2(1, 1);
        Balance = 0.0f;
        RoadNetwork = new RoadNetwork();
    }
}