using Godot;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class ConfigData : Resource
{
    [ExportGroup("Movement")]
    [Export] public bool LBMMovement { get; set; }
    [Export] public bool SmoothMovement { get; set; }
    [Export] public CursorType CurrentCursor = 0;

    public ConfigData(bool lbmMovement, bool smoothMovement)
    {
        LBMMovement = lbmMovement;
        SmoothMovement = smoothMovement;
    }

    public ConfigData()
    {
        LBMMovement = true;
        SmoothMovement = true;
    }

    public enum CursorType
    {
        Default = 0,
        Minimalistic,
        User
    }
}
