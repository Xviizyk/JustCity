using Godot;

namespace JustCity.Script.Core.Save;

public static partial class Save
{
    public static void SaveField(StringName field, Variant value)
    {
        Core core = Core.Instance;
        if (core.IsDebug) GD.Print($"[SAVE | SetField | INFO] {field} was successfully set to {value}");
        Core.Instance.CurrentSaveData.Set(field, value);
    }

    public static Variant GetField(StringName field)
    {
        Core core = Core.Instance;
        Variant value = core.Get(field);
        if (core.IsDebug) GD.Print($"[SAVE | GetField | INFO] Got {field}: {value}");
        return value;
    }
}