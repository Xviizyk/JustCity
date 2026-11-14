using System;
using Godot;
using JustCity.Script.Core.Save;

namespace JustCity.Script.Level.Player;

public partial class Movement : Node
{
    [ExportGroup("Nodes")]
    [Export] public Camera2D Camera;

    [ExportGroup("Settings")]
    [Export] public float MovingSmoothSpeed = 12.0f;
    [Export] public float ZoomingSmoothSpeed = 12.0f;
    [Export] public float ZoomPower = 0.85f;
    [Export] public int ZoomLevels = 20;

    [ExportGroup("Flags")]
    [Export] public bool LBMIsMain = true;
    [Export] public bool IsMoving = false;
    [Export] public bool CanMove = true;
    [Export] public bool CanZoom = true;

    private bool _smoothMovement = true;

    private float _currentZoomLevel = 1.0f;
    private float _larpZoomLevel = 1.0f;

    private Vector2 _targetPosition;

    public override void _Ready()
    {
        ConfigData cd = Core.Core.Instance.CurrentConfigData;
        SaveData sd = Core.Core.Instance.CurrentSaveData;
        _smoothMovement = cd.SmoothMovement;
        LBMIsMain = cd.LBMMovement;
        Camera.Position = sd.Position;
        Camera.Zoom = sd.Zoom;
        _targetPosition = sd.Position;
    }

    public override void _Process(double delta)
    {
        if (CanMove) SmoothMove(delta);
        if (CanZoom) SmoothZoom(delta);

        Core.Core.Instance.CurrentSaveData.Position = Camera.Position;
        Core.Core.Instance.CurrentSaveData.Zoom = Camera.Zoom;
    }

    public void MoveCamera(InputEventMouseMotion motion)
    {
        Vector2 move = motion.Relative / Camera.Zoom;

        if (_smoothMovement) _targetPosition -= move;
        else
        {
            Camera.Position -= move;
            _targetPosition = Camera.Position;
        }
    }

    public void Zoom(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.WheelUp) _larpZoomLevel = Math.Clamp(_larpZoomLevel + 1, 1, ZoomLevels);
        if (mb.ButtonIndex == MouseButton.WheelDown) _larpZoomLevel = Math.Clamp(_larpZoomLevel - 1, 1, ZoomLevels);
    }

    private void SmoothMove(double delta)
    {
        if (!_smoothMovement || !IsMoving && Camera.Position.IsEqualApprox(_targetPosition)) return;
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