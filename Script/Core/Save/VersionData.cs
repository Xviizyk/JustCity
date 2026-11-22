using Godot;

namespace JustCity.Script.Core.Save;

[GlobalClass]
public partial class Version : Resource
{
    [ExportGroup("Version data")]
    [Export] public int Major = 0;
    [Export] public int Minor = 0;
    [Export] public int Patch = 0;
    [Export] public string Type = "unknown";

    public Version(int major, int minor, int patch, string type = "")
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Type = type;
    }

    public Version() {}

    public static Version FromEngine()
    {
        var info = Engine.GetVersionInfo();

        return new Version(
            info["major"].AsInt32(),
            info["minor"].AsInt32(),
            info["patch"].AsInt32(),
            info["status"].AsString()
        );
    }
}
