using System.Linq;
using Core.Save;
using Godot;

namespace Core;

public partial class Core : Node
{
    [ExportGroup("Save System")]
    [Export] public SaveData[] SaveDataArray;
    [Export] public ConfigData CurrentConfigData;
    [Export] public SaveData CurrentSaveData;
    [Export] public bool IsDebug;

    public static Core Instance { get; private set; } = null!;

    private double secondTimer = 0.0;

    public override void _EnterTree()
    {
        Instance = this;

        string[] args = OS.GetCmdlineArgs();
        if (args.Contains("D01") || args.Contains("d01")) IsDebug = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        secondTimer += delta;

        if (secondTimer >= 1.0)
        {
            CurrentSaveData.PlayTime.UpdateTime();
            secondTimer = 0.0;
        }
    }
}