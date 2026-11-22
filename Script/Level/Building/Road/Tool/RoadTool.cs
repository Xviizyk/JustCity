using Godot;

namespace JustCity.Script.Level.Building.Road.Tool;

public abstract class RoadTool(RoadBuilder builder)
{
    protected RoadBuilder Builder = builder;

    public virtual void Update(Vector2 mouse, float delta, bool moved) { }

    public virtual void Pressed() { }

    public virtual void Released() { }

    public virtual bool Cancel()
    {
        return false;
    }
}
