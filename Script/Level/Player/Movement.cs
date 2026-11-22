using System;
using Godot;
using JustCity.Script.Core;
using JustCity.Script.Core.Save;

namespace JustCity.Script.Level.Player;

public partial class Movement : Node
{
    [ExportGroup("Nodes")]
    [Export] public Camera2D Camera;
    [Export] public InputHandler InputHandlerer;

    [ExportGroup("Settings")]
    [Export] public float MovingSmoothSpeed = 12.0f;
    [Export] public float ZoomingSmoothSpeed = 12.0f;
    [Export] public float KeyboardSpeed = 700.0f;
    [Export] public float ZoomPower = 0.85f;
    [Export] public int ZoomLevels = 20;
    [Export] public string Direction;

    [ExportGroup("Inertia")]
    [Export] public bool UseInertia = true;
    [Export] public float InertiaDamping = 5.0f;
    [Export] public float InertiaMinScreenSpeed = 8.0f;
    [Export] public float InertiaMaxScreenSpeed = 4000.0f;
    [Export] public float InertiaReleaseWindowMs = 80.0f;

    [ExportGroup("Bounds")]
    [Export] public Rect2 WorldBounds = new();
    [Export] public float BoundsSpring = 8.0f;

    [ExportGroup("Flags")]
    [Export] public bool LBMIsMain = true;
    [Export] public bool IsMoving = false;
    [Export] public bool CanMove = true;
    [Export] public bool CanZoom = true;

    private bool _smoothMovement = true;
    private bool _wasMoving;

    private float _currentZoomLevel = 1.0f;
    private float _larpZoomLevel = 1.0f;

    private Vector2 _targetPosition;
    private Vector2 _dragVel;
    private Vector2 _inertia;

    private ulong _lastMoveMs;

    public override void _Ready()
    {
        ConfigData cd = this.CurrentConfigData();
        SaveData sd = this.CurrentSaveData();
        _smoothMovement = cd.SmoothMovement;
        LBMIsMain = cd.LBMMovement;
        Camera.Position = sd.Position;
        Camera.Zoom = sd.Zoom;
        _targetPosition = sd.Position;

        float z = Mathf.Max(sd.Zoom.X, 0.001f);
        float lvl = ZoomLevels - Mathf.Log(z) / Mathf.Log(ZoomPower);
        _currentZoomLevel = _larpZoomLevel = Mathf.Clamp(lvl, 1f, ZoomLevels);
    }

    public override void _Process(double delta)
    {
        if (CanMove)
        {
            TrackDrag();
            KeyboardMove(delta);
            ApplyInertia(delta);
            SmoothMove(delta);
            SpringToBounds(delta);
        }
        if (CanZoom) SmoothZoom(delta);

        this.CurrentSaveData().Position = Camera.Position;
        this.CurrentSaveData().Zoom = Camera.Zoom;
    }

    public void MoveCamera(InputEventMouseMotion motion)
    {
        Vector2 move = motion.Relative / Camera.Zoom;
        Camera.Position -= move;
        _targetPosition = Camera.Position;

        ulong now = Time.GetTicksMsec();
        float dt = Mathf.Clamp((now - _lastMoveMs) / 1000f, 0.001f, 0.05f);
        _lastMoveMs = now;
        _dragVel = _dragVel.Lerp(-move / dt, 0.4f);
        _inertia = Vector2.Zero;
    }

    public void Zoom(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.WheelUp) _larpZoomLevel = Math.Clamp(_larpZoomLevel + 1, 1, ZoomLevels);
        if (mb.ButtonIndex == MouseButton.WheelDown) _larpZoomLevel = Math.Clamp(_larpZoomLevel - 1, 1, ZoomLevels);
    }

    private void TrackDrag()
    {
        if (IsMoving && !_wasMoving)
        {
            _inertia = Vector2.Zero;
            _dragVel = Vector2.Zero;
            _lastMoveMs = Time.GetTicksMsec();
        }
        else if (!IsMoving && _wasMoving && UseInertia && _smoothMovement)
        {
            bool fresh = Time.GetTicksMsec() - _lastMoveMs <= (ulong)InertiaReleaseWindowMs;
            _inertia = fresh ? _dragVel.LimitLength(InertiaMaxScreenSpeed / Camera.Zoom.X) : Vector2.Zero;
        }
        _wasMoving = IsMoving;
    }

    private void ApplyInertia(double dt)
    {
        if (_inertia == Vector2.Zero) return;
        if (IsMoving) { _inertia = Vector2.Zero; return; }

        Camera.Position += _inertia * (float)dt;
        _targetPosition = Camera.Position;
        _inertia *= Mathf.Exp(-InertiaDamping * (float)dt);

        if (_inertia.Length() * Camera.Zoom.X < InertiaMinScreenSpeed) _inertia = Vector2.Zero;
    }

    private void KeyboardMove(double delta)
    {
        Vector2 dir = Vector2.Zero;
        if (Direction == "None") return;
        if (Direction == "Up") dir.Y -= 1f;
        else if (Direction == "Down") dir.Y += 1f;
        if (Direction == "Left") dir.X -= 1f;
        else if (Direction == "Right") dir.X += 1f;

        _inertia = Vector2.Zero;
        float speed = KeyboardSpeed * (InputHandlerer.IsShifting ? 2.5f : 1f);
        Vector2 move = dir.Normalized() * speed * (float)delta / Camera.Zoom.X;
        if (_smoothMovement) _targetPosition += move;
        else
        {
            Camera.Position += move;
            _targetPosition = Camera.Position;
        }
    }

    private void SmoothMove(double delta)
    {
        if (!_smoothMovement || IsMoving || Camera.Position.IsEqualApprox(_targetPosition)) return;
        float l = 1.0f - Mathf.Exp(-MovingSmoothSpeed * (float)delta);
        Camera.Position = Camera.Position.Lerp(_targetPosition, l);
    }

    private void SpringToBounds(double dt)
    {
        if (WorldBounds.Size == Vector2.Zero || IsMoving) return;

        Vector2 p = Camera.Position;
        Vector2 c = new(
            Mathf.Clamp(p.X, WorldBounds.Position.X, WorldBounds.End.X),
            Mathf.Clamp(p.Y, WorldBounds.Position.Y, WorldBounds.End.Y)
        );
        Vector2 t = new(
            Mathf.Clamp(_targetPosition.X, WorldBounds.Position.X, WorldBounds.End.X),
            Mathf.Clamp(_targetPosition.Y, WorldBounds.Position.Y, WorldBounds.End.Y)
        );
        _targetPosition = t;

        if (c == p) return;
        _inertia *= 0.85f;
        Camera.Position = p.Lerp(c, 1f - Mathf.Exp(-BoundsSpring * (float)dt));
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
        if (Mathf.IsEqualApprox(Camera.Zoom.X, z)) return;

        Viewport vp = GetViewport();
        Vector2 before = Camera.Position + (vp.GetMousePosition() - vp.GetVisibleRect().Size / 2f) / Camera.Zoom;
        Camera.Zoom = new Vector2(z, z);
        Vector2 after = Camera.Position + (vp.GetMousePosition() - vp.GetVisibleRect().Size / 2f) / Camera.Zoom;
        Vector2 shift = before - after;
        Camera.Position += shift;
        _targetPosition += shift;
    }
}
