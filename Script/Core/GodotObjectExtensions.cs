using Godot;
using JustCity.Script.Core.Save;

namespace JustCity.Script.Core;

public static class GodotObjectExtensions
{
    public static bool IsDebug(this GodotObject _) => Core.Instance.IsDebug;

    public static SaveData CurrentSaveData(this GodotObject _) => Core.Instance?.CurrentSaveData;

    public static ConfigData CurrentConfigData(this GodotObject _) => Core.Instance?.CurrentConfigData;
}
