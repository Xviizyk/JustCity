using Godot;
using JustCity.Script.Level.Building;

namespace JustCity.Script.Level.Player;

public partial class InputHandler : Node
{
    [ExportGroup("Nodes")]
    [Export] public Movement PlayerMovement;
    [Export] public RoadBuilder Builder;

    [ExportGroup("Settings")]
    [Export] public float PanThreshold = 4f;

    private bool _rightDown;
    private bool _rightPanned;
    private Vector2 _rightStart;

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.Pressed && mb.CtrlPressed
                && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                Builder.ChangeLanes(mb.ButtonIndex == MouseButton.WheelUp ? 1 : -1);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                Builder.BuildPressed();
            }
            else if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
            {
                Builder.BuildReleased();
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                PlayerMovement.IsMoving = mb.Pressed;
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                if (mb.Pressed)
                {
                    _rightDown = true;
                    _rightPanned = false;
                    _rightStart = mb.Position;
                }
                else
                {
                    _rightDown = false;
                    PlayerMovement.IsMoving = false;
                    if (!_rightPanned) Builder.Back();
                }
            }

            if (mb.Pressed) PlayerMovement.Zoom(mb);
        }

        if (@event is InputEventMouseMotion motion)
        {
            if (_rightDown && !_rightPanned && motion.Position.DistanceTo(_rightStart) > PanThreshold)
            {
                _rightPanned = true;
                PlayerMovement.IsMoving = true;
            }

            if (PlayerMovement.IsMoving) PlayerMovement.MoveCamera(motion);
        }
    }
}
