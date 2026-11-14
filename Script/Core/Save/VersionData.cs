using Godot;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class Version : Resource
{
    [ExportGroup("Version data")]
    [Export] public int Minor = 0;
    [Export] public int Major = 0;
    [Export] public int Patch = 0;
    [Export] public string Type = "a";

    public Version(int minor, int major, int patch, string type = "")
    {
        Minor = minor;
        Major = major;
        Patch = patch;
        Type = type;
    }

    public Version()
    {
        Minor = 0;
        Major = 0;
        Patch = 0;
        Type = "unknown";
    }
}