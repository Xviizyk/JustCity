using System;
using System.Dynamic;
using Godot;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class GameTime : Resource
{
    [ExportGroup("Time")]
    [Export] public int Days { get; private set; } = 0;
    [Export] public int Hours { get; private set; } = 0;
    [Export] public int Minutes { get; private set; } = 0;
    [Export] public int Seconds { get; private set; } = 0;
    [Export] public long RawTime { get; private set; } = 0;

    public GameTime() {}

    public void UpdateTime()
    {
        RawTime++;
        Compute();
    }

    public void SetTime(long rawTime)
    {
        RawTime = rawTime;
        Compute();
    }

    private void Compute()
    {
        Days = (int)(RawTime / 86400L);
        Hours = (int)(RawTime % 86400L / 3600);
        Minutes = (int)(RawTime % 3600L / 60);
        Seconds = (int)(RawTime % 60);
    }
}
