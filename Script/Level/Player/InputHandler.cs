using Godot;
using JustCity.Script.Level.Building;

namespace JustCity.Script.Level.Player;

public partial class InputHandler : Node
{
    [ExportGroup("Nodes")]
    [Export] public Movement PlayerMovement;
    [Export] public RoadBuilder Builder;

    public override void _Input(InputEvent @event)
    {
        string mainAction = PlayerMovement.LBMIsMain ? "Left" : "Right";
        string buildAction = PlayerMovement.LBMIsMain ? "Right" : "Left";

        if (@event.IsActionPressed(mainAction)) PlayerMovement.IsMoving = true;
        if (@event.IsActionReleased(mainAction)) PlayerMovement.IsMoving = false;

        if (@event.IsActionPressed(buildAction)) Builder.BuildPressed();
        if (@event.IsActionReleased(buildAction)) Builder.BuildReleased();

        if (PlayerMovement.IsMoving && @event is InputEventMouseMotion motion) PlayerMovement.MoveCamera(motion);
        if (@event is InputEventMouseButton mb && mb.Pressed) PlayerMovement.Zoom(mb);
    }
}