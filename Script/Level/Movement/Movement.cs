using Core.Save;
using Godot;

namespace Level.Movement;

public partial class Movement : Node
{
    [ExportGroup("Mouse control")]
    [Export] public float SmoothSpeed = 12.0f;
    [Export] public Camera2D Camera;

    private bool _leftMain = true;
    private bool _smoothMovement = true;
    private bool _isDragging = false;

    private Vector2 _targetPosition;

    public override void _Ready()
    {
        ConfigData cd = Core.Core.Instance.CurrentConfigData;
        _smoothMovement = cd.SmoothMovement;
        _leftMain = cd.LBMMovement;
    }

    public override void _Process(double delta)
    {
        SmoothMove(delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (_leftMain ? @event.IsActionPressed("Left") : @event.IsActionPressed("Right"))
        {
            _isDragging = true;
        }

        if (_isDragging && @event is InputEventMouseMotion motion)
        {
            Vector2 move = motion.Relative;

            if (_smoothMovement) _targetPosition += move;
            else
            {
                Camera.Position += move;
                _targetPosition = Camera.Position;
            }
        }
    }

    private void SmoothMove(double delta)
    {
        if (!_smoothMovement || !_isDragging && Camera.Position.IsEqualApprox(_targetPosition)) return;
        float l = 1.0f - Mathf.Exp(-SmoothSpeed * (float)delta);
        Camera.Position = _targetPosition.Lerp(_targetPosition, l);
    }
}