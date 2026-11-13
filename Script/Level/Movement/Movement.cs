using System;
using Core.Save;
using Godot;

namespace Level.Movement;

public partial class Movement : Node
{
    [ExportGroup("Control")]
    [Export] public float MovingSmoothSpeed = 12.0f;
    [Export] public float ZoomingSmoothSpeed = 12.0f;
    [Export] public float ZoomPower = 0.85f;
    [Export] public int ZoomLevels = 20;
    [Export] public Camera2D Camera;

    private bool _leftMain = true;
    private bool _smoothMovement = true;
    private bool _isDragging = false;

    private float _currentZoomLevel = 1.0f;
    private float _larpZoomLevel = 1.0f;

    private Vector2 _targetPosition;

    public override void _Ready()
    {
        ConfigData cd = Core.Core.Instance.CurrentConfigData;
        SaveData sd = Core.Core.Instance.CurrentSaveData;
        _smoothMovement = cd.SmoothMovement;
        _leftMain = cd.LBMMovement;
        Camera.Position = sd.Position;
        Camera.Zoom = sd.Zoom;
        _targetPosition = sd.Position;
    }

    public override void _Process(double delta)
    {
        SmoothMove(delta);
        SmoothZoom(delta);

        Core.Core.Instance.CurrentSaveData.Position = Camera.Position;
        Core.Core.Instance.CurrentSaveData.Zoom = Camera.Zoom;
    }

    public override void _Input(InputEvent @event)
    {
        if (_leftMain ? @event.IsActionPressed("Left") : @event.IsActionPressed("Right"))
        {
            _isDragging = true;
        }

        if (_leftMain ? @event.IsActionReleased("Left") : @event.IsActionReleased("Right"))
        {
            _isDragging = false;
        }

        if (_isDragging && @event is InputEventMouseMotion motion)
        {
            Vector2 move = motion.Relative / Camera.Zoom;

            if (_smoothMovement) _targetPosition -= move;
            else
            {
                Camera.Position -= move;
                _targetPosition = Camera.Position;
            }
        }

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _larpZoomLevel = Math.Clamp(_larpZoomLevel + 1, 1, ZoomLevels);
            if (mb.ButtonIndex == MouseButton.WheelDown) _larpZoomLevel = Math.Clamp(_larpZoomLevel - 1, 1, ZoomLevels);
        }
    }

    private void SmoothMove(double delta)
    {
        if (!_smoothMovement || !_isDragging && Camera.Position.IsEqualApprox(_targetPosition)) return;
        float l = 1.0f - Mathf.Exp(-MovingSmoothSpeed * (float)delta);
        Camera.Position = Camera.Position.Lerp(_targetPosition, l);
    }

    private void SmoothZoom(double delta)
    {
        if (!_smoothMovement)
        {
            _currentZoomLevel = _larpZoomLevel;
        }
        else
        {
            float l = 1.0f - Mathf.Exp(-ZoomingSmoothSpeed * (float)delta);
            _currentZoomLevel = Mathf.Lerp(_currentZoomLevel, _larpZoomLevel, l);
        }

        float z = Mathf.Pow(ZoomPower, ZoomLevels - _currentZoomLevel);
        Camera.Zoom = new Vector2(z, z);
    }
}