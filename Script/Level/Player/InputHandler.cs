using Godot;
using JustCity.Script.Level.Building.Road.Tool;

namespace JustCity.Script.Level.Player;

public partial class InputHandler : Node
{
    private static readonly StringName ActionBuild = "lmb";
    private static readonly StringName ActionCameraPan = "mmb";
    private static readonly StringName ActionCancelOrPan = "rmb";
    private static readonly StringName ActionLanesIncrease = "lanes_increase";
    private static readonly StringName ActionLanesDecrease = "lanes_decrease";
    private static readonly StringName ActionUndo = "undo";
    private static readonly StringName ActionRedo = "redo";
    private static readonly StringName ActionEscape = "road_escape";
    private static readonly StringName ActionToggleClear = "road_toggle_clear";
    private static readonly StringName ActionToggleParallel = "road_toggle_parallel";
    private static readonly StringName ActionStepBack = "road_step_back";
    private static readonly StringName ActionToggleLevel = "road_level_change";
    private static readonly StringName ActionToggleDebug = "debug";
    private static readonly StringName ActionShift = "shift";
    private static readonly StringName ActionAlt = "alt";
    private static readonly StringName ActionTool1 = "tool_1";
    private static readonly StringName ActionTool2 = "tool_2";
    private static readonly StringName ActionTool3 = "tool_3";
    private static readonly StringName ActionTool4 = "tool_4";
    private static readonly StringName ActionTool5 = "tool_5";
    private static readonly StringName ActionTool6 = "tool_6";
    private static readonly StringName ActionMoveUp = "move_up";
    private static readonly StringName ActionMoveDown = "move_down";
    private static readonly StringName ActionMoveLeft = "move_left";
    private static readonly StringName ActionMoveRight = "move_right";

    [ExportGroup("Nodes")]
    [Export] public Movement PlayerMovement;
    [Export] public RoadBuilder Builder;

    [ExportGroup("Settings")]
    [Export] public float PanThreshold = 4f;

    [ExportGroup("Keys")]
    [Export] public bool IsShifting = false;
    [Export] public bool IsAlting = false;

    private bool _rightDown;
    private bool _rightPanned;
    private Vector2 _rightStart;

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed(ActionLanesIncrease))
        {
            Builder.ChangeLanes(1);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event.IsActionPressed(ActionLanesDecrease))
        {
            Builder.ChangeLanes(-1);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event.IsActionPressed(ActionBuild)) Builder.BuildPressed();
        else if (@event.IsActionReleased(ActionBuild))
        {
            Builder.BuildReleased();
        }
        else if (@event.IsActionPressed(ActionCameraPan)) PlayerMovement.IsMoving = true;
        else if (@event.IsActionReleased(ActionCameraPan)) PlayerMovement.IsMoving = false;
        else if (@event.IsActionPressed(ActionCancelOrPan))
        {
            _rightDown = true;
            _rightPanned = false;
            _rightStart = (@event as InputEventMouse)?.Position ?? GetViewport().GetMousePosition();
        }
        else if (@event.IsActionReleased(ActionCancelOrPan))
        {
            _rightDown = false;
            PlayerMovement.IsMoving = false;
            if (!_rightPanned) Builder.Back();
        }
        else if (@event.IsActionPressed(ActionShift)) IsShifting = true;
        else if (@event.IsActionPressed(ActionAlt)) IsAlting = true;
        else if (@event.IsActionReleased(ActionShift)) IsShifting = false;
        else if (@event.IsActionReleased(ActionAlt)) IsAlting = false;

        if (@event is InputEventMouseButton { Pressed: true } mb) PlayerMovement.Zoom(mb);
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(ActionRedo, exactMatch: true)) { Builder.Redo(); MarkHandled(); return; }
        if (@event.IsActionPressed(ActionUndo, exactMatch: true)) { Builder.Undo(); MarkHandled(); return; }
        if (@event.IsActionPressed(ActionEscape)) { Builder.Escape(); return; }
        if (@event.IsActionPressed(ActionToggleClear)) { Builder.ToggleClear(); return; }
        if (@event.IsActionPressed(ActionToggleParallel)) { Builder.ToggleParallel(); return; }
        if (@event.IsActionPressed(ActionStepBack)) { Builder.StepBack(); return; }
        if (@event.IsActionPressed(ActionToggleLevel)) { Builder.ToggleLevel(); return; }
        if (@event.IsActionPressed(ActionToggleDebug)) { Builder.ToggleDebug(); return; }
        if (Builder.Active && Builder.Mode == BuildMode.Build)
        {
            if (@event.IsActionPressed(ActionTool1)) { Builder.Type = BuildType.Straight; return; }
            if (@event.IsActionPressed(ActionTool2)) { Builder.Type = BuildType.Circular; return; }
            if (@event.IsActionPressed(ActionTool3)) { Builder.Type = BuildType.Grid; return; }
            if (@event.IsActionPressed(ActionTool4)) { Builder.Type = BuildType.GridSmooth; return; }
            if (@event.IsActionPressed(ActionTool5)) { Builder.Type = BuildType.Smooth; return; }
            if (@event.IsActionPressed(ActionTool6)) { Builder.Type = BuildType.Smart; return; }
        }
        PlayerMovement.Direction = "None";
        if (@event.IsAction(ActionMoveUp)) { PlayerMovement.Direction = "Up"; return; }
        if (@event.IsAction(ActionMoveDown)) { PlayerMovement.Direction = "Down"; return; }
        if (@event.IsAction(ActionMoveLeft)) { PlayerMovement.Direction = "Left"; return; }
        if (@event.IsAction(ActionMoveRight)) { PlayerMovement.Direction = "Right"; return; }
    }

    private void MarkHandled() => GetViewport().SetInputAsHandled();
}