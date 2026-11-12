using Godot;

namespace Core.Save;

[GlobalClass]
public partial class ConfigData : Resource
{
    [ExportGroup("Movement")]
    [Export] public bool LBMMovement { get; set; }
    [Export] public bool SmoothMovement { get; set; }

    public ConfigData(bool lbmMovement, bool smoothMovement)
    {
        LBMMovement = lbmMovement;
        SmoothMovement = smoothMovement;
    }

    public ConfigData()
    {
        LBMMovement = false;
        SmoothMovement = true;
    }
}