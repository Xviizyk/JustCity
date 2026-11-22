#nullable enable

using System.Linq;
using System.Text;
using Godot;
using JustCity.Script.Core.Save;
using JustCity.Script.Core.Utils;

namespace JustCity.Script.Core;

public partial class Core : Node
{
    [ExportGroup("Save System")]
    [Export] public ConfigData CurrentConfigData = null!;
    [Export] public SaveData CurrentSaveData = null!;
    [Export] public bool IsDebug;
    [Export] public string SaveDir = "saves/save.svmew";
    [Export] public string ConfigDir = "config/config.cfgmew";

    [ExportGroup("Cursor")]
    [Export] public string CursorFolder = "res://Resources/Textures/Cursor/";
    [Export] public string MinimalisticCursorTextureDir = "MinimalisticCursor.png";
    [Export] public string DefaultCursorTextureDir = "DefaultCursor.png";

    private Resource _defaultCursorTexture = null!;
    private Resource _minimalisticCursorTexture = null!;

    public static Core Instance { get; private set; } = null!;

    private double secondTimer = 0.0;

    public override void _EnterTree()
    {
        Instance = this;

        _defaultCursorTexture = ResourceLoader.Load(CursorFolder + DefaultCursorTextureDir);
        _minimalisticCursorTexture = ResourceLoader.Load(CursorFolder + MinimalisticCursorTextureDir);

        string[] args = OS.GetCmdlineArgs();
        if (args.Contains("_debug") || args.Contains("__DEBUG")) IsDebug = true;

        if (IsDebug) GD.Print("Game is in debug mode");

        GetPlayerData();
        ChangeCursor((int)CurrentConfigData.CurrentCursor);
    }

    public override void _ExitTree()
    {
        CurrentSaveData.Version = Version.FromEngine();
        SavePlayerData();
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
        byte[]? cfgBytes = FileUtils.Load(ConfigDir);
        byte[]? saveBytes = FileUtils.Load(SaveDir);

        CurrentConfigData = new ConfigData();
        CurrentSaveData = new SaveData();

        if (cfgBytes != null) ReflectSave.FillFromJson(CurrentConfigData, Encoding.UTF8.GetString(cfgBytes));
        if (saveBytes != null) ReflectSave.FillFromJson(CurrentSaveData, Encoding.UTF8.GetString(saveBytes));
    }

    private void SavePlayerData()
    {
        FileUtils.Save(ConfigDir, Encoding.UTF8.GetBytes(ReflectSave.ToJson(CurrentConfigData)));
        FileUtils.Save(SaveDir, Encoding.UTF8.GetBytes(ReflectSave.ToJson(CurrentSaveData)));
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
