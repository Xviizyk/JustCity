using Godot;
using JustCity.Script.Core.Save;

namespace JustCity.Script.Level.Building;

public partial class RoadBuilder : Node2D
{
    [ExportGroup("State")]
    [Export] public RoadNetwork Network;
    [Export] public bool Active = true;

    [ExportGroup("Road")]
    [Export] public int LanesPerDirection = 1;
    [Export] public float LaneWidth = 12f;
    [Export] public int Level = 0;

    [ExportGroup("Snapping")]
    [Export] public float GridSize = 0f;
    [Export] public float SnapRadius = 20f;
    [Export] public float DragCommitDistance = 24f;

    [ExportGroup("Rules")]
    [Export] public float MinRadius = 30f;
    [Export] public float MinLength = 40f;
    [Export] public float CostPerPixel = 0.5f;
    [Export] public float BridgeCostMultiplier = 2f;

    [ExportGroup("Debug")]
    [Export] public bool ShowDebug = false;

    private RoadRenderer _renderer;
    private bool _placing;
    private RoadSnap _start;
    private RoadSnap _hover;
    private Curve2D _preview;
    private bool _valid;
    private bool _dragStarted;

    public override void _EnterTree()
    {
        SaveData sd = Core.Core.Instance.CurrentSaveData;
        sd.RoadNetwork ??= new RoadNetwork();
        Network = sd.RoadNetwork;

        _renderer = new RoadRenderer
        {
            ShowBehindParent = true,
            ShowDebug = ShowDebug
        };
        AddChild(_renderer);
        _renderer.Network = Network;
    }

    public override void _ExitTree()
    {
        Core.Core.Instance.CurrentSaveData.RoadNetwork = Network;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Active) return;

        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.Escape) Cancel();
            else if (k.Keycode == Key.L)
            {
                Cancel();
                Level = Level == 0 ? 1 : 0;
            }
            else if (k.Keycode == Key.F3)
            {
                ShowDebug = !ShowDebug;
                _renderer.ShowDebug = ShowDebug;
                _renderer.QueueRedraw();
            }
        }
    }

    public void BuildPressed()
    {
        if (!Active) return;
        if (!_placing)
        {
            Begin();
            _dragStarted = true;
        }
        else
        {
            Commit();
            _dragStarted = false;
        }
    }

    public void BuildReleased()
    {
        if (!Active) return;
        if (_placing && _dragStarted && _hover.Position.DistanceTo(_start.Position) > DragCommitDistance)
        {
            Commit();
            Cancel();
        }
        _dragStarted = false;
    }

    private void Begin()
    {
        _start = _hover;
        _placing = true;
    }

    public void Cancel()
    {
        _placing = false;
        _dragStarted = false;
        _preview = null;
        QueueRedraw();
    }

    private void Commit()
    {
        if (!_valid || _preview == null) return;

        Network.AddRoad(_preview, LanesPerDirection, LaneWidth, Level);

        Vector2 end = _preview.GetPointPosition(_preview.PointCount - 1);
        _start = new RoadSnap(end, null, null, RoadGeometry.EndTangent(_preview));
        _preview = null;
        _valid = false;
    }

    public override void _Process(double delta)
    {
        if (!Active || Network == null) return;

        _hover = HoverSnap();

        if (_placing)
        {
            if (_hover.Position.DistanceTo(_start.Position) < 1f)
            {
                _preview = null;
                _valid = false;
            }
            else
            {
                Vector2 dir = _start.Direction != Vector2.Zero ? _start.Direction : (_hover.Position - _start.Position).Normalized();
                ArcInfo arc = RoadGeometry.Arc(_start.Position, dir, _hover.Position);
                _preview = arc.Curve;
                _valid = arc.Radius >= MinRadius && Mathf.Abs(arc.Sweep) <= Mathf.Pi && arc.Curve.GetBakedLength() >= MinLength;
            }
        }

        QueueRedraw();
    }

    private RoadSnap HoverSnap()
    {
        Vector2 raw = GetLocalMousePosition();
        RoadSnap snap = Network.Snap(raw, SnapRadius, Level);
        if (snap.IsFree && GridSize > 0f)
        {
            snap = snap with
            {
                Position = new Vector2(
                    Mathf.Round(raw.X / GridSize) * GridSize,
                    Mathf.Round(raw.Y / GridSize) * GridSize
                )
            };
        }
        return snap;
    }

    public override void _Draw()
    {
        if (!Active || Network == null) return;

        Color ring = _hover.IsFree ? new Color(0f, 0f, 0f, 0.35f) : new Color(0.2f, 0.55f, 0.8f, 0.9f);
        DrawArc(_hover.Position, 10f, 0f, Mathf.Tau, 24, ring, 2f, true);

        if (Level > 0) DrawChip(GetLocalMousePosition() + new Vector2(16f, -30f), $"Уровень {Level}");
        if (!_placing || _preview == null) return;

        Color fill = _valid ? new Color(0.3f, 0.6f, 0.75f, 0.55f) : new Color(0.85f, 0.3f, 0.3f, 0.55f);
        DrawPolyline(RoadPoly.Of(_preview), fill, LanesPerDirection * LaneWidth * 2f, true);

        Vector2 dir = _start.Direction != Vector2.Zero ? _start.Direction : (_hover.Position - _start.Position).Normalized();
        Vector2 nrm = dir.Orthogonal();
        Color guide = new(0f, 0f, 0f, 0.3f);
        DrawDashedLine(_start.Position - dir * 2000f, _start.Position + dir * 2000f, guide, 2f, 10f);
        DrawDashedLine(_start.Position - nrm * 2000f, _start.Position + nrm * 2000f, guide, 2f, 10f);

        float len = _preview.GetBakedLength();
        float cost = len * CostPerPixel * (Level > 0 ? BridgeCostMultiplier : 1f) * LanesPerDirection;
        Vector2 labelPos = GetLocalMousePosition() + new Vector2(16f, 16f);
        DrawChip(labelPos, $"${cost:0}");
        DrawChip(labelPos + new Vector2(64f, 0f), $"{len:0} px");
    }

    private void DrawChip(Vector2 pos, string text)
    {
        Font font = ThemeDB.FallbackFont;
        const int fs = 14;
        Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
        var rect = new Rect2(pos, size + new Vector2(12f, 8f));
        DrawRect(rect, new Color(1f, 1f, 1f, 0.9f));
        DrawRect(rect, new Color(0f, 0f, 0f, 0.35f), false, 1f);
        DrawString(font, pos + new Vector2(6f, 4f + font.GetAscent(fs)), text, HorizontalAlignment.Left, -1, fs, Colors.Black);
    }
}