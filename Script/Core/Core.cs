using System.ComponentModel;
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

    [ExportGroup("Cursor")]
    [Export] public string CursorFolder = "res://Resources/Textures/Cursor/";
    [Export] public string MinimalisticCursorTextureDir = "MinimalisticCursor.png";
    [Export] public string DefaultCursorTextureDir = "DefaultCursor.png";

    private Resource _defaultCursorTexture;
    private Resource _minimalisticCursorTexture;

    public static Core Instance { get; private set; } = null!;

    private double secondTimer = 0.0;

    public override void _EnterTree()
    {
        Instance = this;

        _defaultCursorTexture = ResourceLoader.Load(CursorFolder + DefaultCursorTextureDir);
        _minimalisticCursorTexture = ResourceLoader.Load(CursorFolder + MinimalisticCursorTextureDir);

        string[] args = OS.GetCmdlineArgs();
        if (args.Contains("_debug") || args.Contains("__DEBUG")) IsDebug = true;

        GetPlayerData();
        ChangeCursor((int)CurrentConfigData.CurrentCursor);
    }

    public override void _Process(double delta)
    {
        secondTimer += delta;

        if (secondTimer >= 1.0)
        {
            CurrentSaveData.PlayTime.UpdateTime();
            secondTimer = 0.0;
        }
    }

    private void GetPlayerData()
    {
        // TODO: Get Savedata and Configdata from files
        CurrentConfigData = new ConfigData();
        CurrentSaveData = new SaveData();
    }

    public void ChangeCursor(int type)
    {
        switch (type)
        {
            case 1: Input.SetCustomMouseCursor(_minimalisticCursorTexture); break;
            case 2: Input.SetCustomMouseCursor(null); break;
            case 0: default: Input.SetCustomMouseCursor(_defaultCursorTexture); break;
        }

        CurrentConfigData.CurrentCursor = (ConfigData.CursorType)type;
    }
}