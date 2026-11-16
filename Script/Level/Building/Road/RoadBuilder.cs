using System.Collections.Generic;
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

	[ExportGroup("Snapping (экранные px)")]
	[Export] public float GridSize = 0f;
	[Export] public float SnapRadius = 20f;
	[Export] public float SnapPx = 10f;
	[Export] public float DeadZonePx = 10f;
	[Export] public float LengthStep = 8f;
	[Export] public float AngleStepDeg = 15f;
	[Export] public float RhythmStep = 96f;
	[Export] public float AutoDirConeDeg = 35f;

	[ExportGroup("Rules")]
	[Export] public float MinRadius = 30f;
	[Export] public float MinLength = 40f;
	[Export] public float MinJunctionAngleDeg = 25f;
	[Export] public float CostPerPixel = 0.5f;
	[Export] public float BridgeCostMultiplier = 2f;
	[Export] public int MaxLanes = 4;

	[ExportGroup("Feel")]
	[Export] public float PreviewSmoothing = 28f;
	[Export] public float ClickBuffer = 0.08f;

	[ExportGroup("Style")]
	[Export] public Color Accent = new(0.20f, 0.45f, 0.95f);
	[Export] public Color Ink = new(0.17f, 0.18f, 0.20f);
	[Export] public Color Bad = new(0.90f, 0.35f, 0.30f);

	[ExportGroup("Sound")]
	[Export] public bool Sound = true;
	[Export] public float SoundVolumeDb = -14f;
	[Export] public AudioStream SnapSound;
	[Export] public AudioStream BuildSound;
	[Export] public AudioStream DenySound;

	[ExportGroup("Debug")]
	[Export] public bool ShowDebug = false;

	private enum GKind { None, Ray, Angle, AlignX, AlignY, Perp, Rhythm }

	private record struct Cand(Vector2 Pos, RoadSegment Seg, GKind Kind, Vector2 GA, Vector2 GB, string Key);
	private record struct Preview(Curve2D Curve, bool Valid, bool Clamped, string Reason, float Turn, Vector2 At);
	private sealed record Entry(RoadOp Op, float Cost, RoadSnap Start, Vector2 Dir, List<Vector2> Cands);

	private RoadRenderer _renderer;
	private AudioStreamPlayer _tickPlayer;
	private AudioStreamPlayer _fxPlayer;

	private bool _placing;
	private RoadSnap _start;
	private RoadSnap _hover;
	private Vector2 _dir;
	private Vector2 _autoDir;
	private List<Vector2> _dirCandidates = new();
	private int _chainCount;

	private Preview _pv;
	private Curve2D _shown;
	private List<Vector2> _cuts = new();
	private Vector2 _smooth;
	private float _badT;

	private GKind _gKind;
	private Vector2 _gA, _gB;
	private string _snapKey = "";
	private string _snapKeyNow = "";
	private float _tickCool;
	private Vector2 _ringPos;
	private float _ringR = 9f;
	private float _buffer;
	private float _shake;

	private readonly List<Entry> _history = new();
	private readonly List<Entry> _redo = new();

	private float Z => Mathf.Max(0.01f, GetGlobalTransformWithCanvas().Scale.X);
	private Vector2 ScreenToLocal(Vector2 s) => GetGlobalTransformWithCanvas().AffineInverse() * s;

	private AudioStreamWav _defTick, _defBuild, _defDeny;

	public override void _EnterTree()
	{
		SaveData sd = Core.Core.Instance.CurrentSaveData;
		sd.RoadNetwork ??= new RoadNetwork();
		Network = sd.RoadNetwork;

		_renderer = new RoadRenderer { ShowBehindParent = true, ShowDebug = ShowDebug };
		AddChild(_renderer);
		_renderer.Network = Network;

		_tickPlayer = new AudioStreamPlayer();
		_fxPlayer = new AudioStreamPlayer();
		AddChild(_tickPlayer);
		AddChild(_fxPlayer);
		_defTick = MakeTone(1800f, 0.030f, 0.30f);
		_defBuild = MakeTone(520f, 0.090f, 0.40f);
		_defDeny = MakeTone(170f, 0.120f, 0.40f);
	}

	public override void _ExitTree()
	{
		Core.Core.Instance.CurrentSaveData.RoadNetwork = Network;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Active) return;
		if (e is not InputEventKey { Pressed: true } k) return;

		bool ctrl = k.CtrlPressed || k.MetaPressed;
		if (ctrl && (k.Keycode == Key.Z || k.Keycode == Key.Y))
		{
			if (k.Echo) return;
			if (k.Keycode == Key.Y || k.ShiftPressed) RedoLast();
			else UndoLast();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (k.Echo) return;

		switch (k.Keycode)
		{
			case Key.Escape:
				if (!_placing && !_clearing && _parRef == null && (_clearMode || _parMode)) ExitModes();
				else Cancel();
				break;
			case Key.X: ToggleClearMode(); break;
			case Key.P: ToggleParallelMode(); break;
			case Key.Backspace:
				if (_placing && _chainCount > 0) UndoLast();
				break;
			case Key.Bracketleft: ChangeLanes(-1); break;
			case Key.Bracketright: ChangeLanes(1); break;
			case Key.L:
				Cancel();
				Level = Level == 0 ? 1 : 0;
				break;
			case Key.F3:
				ShowDebug = !ShowDebug;
				_renderer.ShowDebug = ShowDebug;
				_renderer.QueueRedraw();
				break;
		}
	}

	public void BuildPressed()
	{
		if (!Active) return;
		if (_clearMode) { ClearPressed(); return; }
		if (_parMode) { ParallelPressed(); return; }
		if (!_placing)
		{
			_start = _hover;
			_dirCandidates = Network.StartDirections(_start);
			_autoDir = Vector2.Zero;
			_dir = _start.Direction;
			_placing = true;
			_chainCount = 0;
			_smooth = _start.Position;
			_shown = null;
			_pv = default;
			Play(_tickPlayer, _defTick, 1.25f);
		}
		else if (!TryCommit())
		{
			if (_pv.Curve == null) return;
			_buffer = ClickBuffer;
		}
	}

	public void BuildReleased()
	{
		if (!Active) return;
		if (_clearMode) ClearReleased();
	}

	public void Back() => Cancel();

	public void Cancel()
	{
		_placing = false;
		_clearing = false;
		_clearPlan = null;
		_parRef = null;
		_pv = default;
		_shown = null;
		_cuts.Clear();
		_buffer = 0f;
		_chainCount = 0;
		_gKind = GKind.None;
		QueueRedraw();
	}

	public void ChangeLanes(int delta)
	{
		LanesPerDirection = Mathf.Clamp(LanesPerDirection + delta, 1, MaxLanes);
		Play(_tickPlayer, _defTick, 0.9f + 0.1f * LanesPerDirection);
	}

	private float CostOf(Curve2D curve) =>
		curve.GetBakedLength() * CostPerPixel * (Level > 0 ? BridgeCostMultiplier : 1f) * LanesPerDirection;

	private bool Straight => Input.IsKeyPressed(Key.Shift);
	private bool IsArc => _dir != Vector2.Zero && !Straight;

	private bool TryCommit()
	{
		if (!_pv.Valid || _pv.Curve == null) return false;

		SaveData sd = Core.Core.Instance.CurrentSaveData;
		float cost = CostOf(_pv.Curve);
		if (sd.Balance < cost) return false;

		RoadOp op = Network.Build(_pv.Curve, LanesPerDirection, LaneWidth, Level);
		if (op == null) return false;
		sd.Balance -= cost;

		_history.Add(new Entry(op, cost, _start, _dir, _dirCandidates));
		_redo.Clear();
		_renderer.Grow(op.Built);
		Play(_fxPlayer, _defBuild, 1f);

		if (!_hover.IsFree) { Cancel(); return true; }

		Curve2D curve = _pv.Curve;
		Vector2 end = curve.GetPointPosition(curve.PointCount - 1);
		Vector2 endDir = RoadGeometry.EndTangent(curve);
		_start = new RoadSnap(end, null, null, endDir);
		_dir = endDir;
		_dirCandidates = new List<Vector2>();
		_chainCount++;
		_pv = default;
		_shown = null;
		_cuts.Clear();
		_smooth = end;
		return true;
	}

	private void UndoLast()
	{
		if (_history.Count == 0) return;
		Entry e = _history[^1];
		_history.RemoveAt(_history.Count - 1);

		Network.Undo(e.Op);
		Core.Core.Instance.CurrentSaveData.Balance += e.Cost;
		_redo.Add(e);
		Play(_fxPlayer, _defTick, 0.7f);

		if (_placing && _chainCount > 0)
		{
			_chainCount--;
			_start = e.Start;
			_dir = e.Dir;
			_dirCandidates = e.Cands;
			_autoDir = Vector2.Zero;
			_pv = default;
			_shown = null;
			_cuts.Clear();
			_smooth = _start.Position;
		}
		else Cancel();
	}

	private void RedoLast()
	{
		if (_redo.Count == 0) return;
		Entry e = _redo[^1];
		SaveData sd = Core.Core.Instance.CurrentSaveData;
		if (sd.Balance < e.Cost) { Deny(); return; }
		_redo.RemoveAt(_redo.Count - 1);

		Cancel();
		Network.Redo(e.Op);
		sd.Balance -= e.Cost;
		_history.Add(e);
		_renderer.Grow(e.Op.Built);
		Play(_fxPlayer, _defBuild, 1.1f);
	}

	private void Deny()
	{
		_shake = 0.25f;
		Play(_fxPlayer, _defDeny, 1f);
	}

	public override void _Process(double delta)
	{
		if (!Active || Network == null) return;
		float dt = (float)delta;

		_hover = HoverSnap();
		if (_snapKeyNow != _snapKey)
		{
			if (_snapKeyNow != "" && _tickCool <= 0f)
			{
				Play(_tickPlayer, _defTick, (float)GD.RandRange(0.95, 1.05));
				_tickCool = 0.04f;
				if (_hover.Node != null) _ringR = 15f;
			}
			_snapKey = _snapKeyNow;
		}
		_tickCool = Mathf.Max(0f, _tickCool - dt);

		if (_clearMode) UpdateClear();
		else if (_parMode) UpdateParallel();

		if (_placing)
		{
			UpdatePreview();
			_smooth = _smooth.Lerp(_hover.Position, 1f - Mathf.Exp(-PreviewSmoothing * dt));
			if (_pv.Curve != null) UpdateShown();
		}

		if (_buffer > 0f)
		{
			_buffer -= dt;
			if (_pv.Valid && TryCommit()) _buffer = 0f;
			else if (_buffer <= 0f) Deny();
		}
		_shake = Mathf.Max(0f, _shake - dt);

		float follow = _hover.IsFree ? 1f : 1f - Mathf.Exp(-30f * dt);
		_ringPos = _ringPos.Lerp(_hover.Position, follow);
		_ringR = Mathf.Lerp(_ringR, 9f, 1f - Mathf.Exp(-22f * dt));

		bool bad = (_placing && _pv.Curve != null && !_pv.Valid) || (_parMode && _parRef != null && _parCurve != null && !_parValid);
		_badT = Mathf.MoveToward(_badT, bad ? 1f : 0f, dt * 8f);

		QueueRedraw();
	}

	private void UpdateDir(Vector2 raw)
	{
		if (_start.Direction != Vector2.Zero) { _dir = _start.Direction; return; }

		Vector2 to = raw - _start.Position;
		if (to.Length() < 6f / Z) return;

		Vector2 best = Vector2.Zero;
		float bestA = Mathf.DegToRad(AutoDirConeDeg);
		foreach (Vector2 d in _dirCandidates)
		{
			float a = Mathf.Abs(d.AngleTo(to));
			if (d == _autoDir) a -= Mathf.DegToRad(8f);
			if (a < bestA) { bestA = a; best = d; }
		}
		_autoDir = best;
		_dir = best;
	}

	private RoadSnap HoverSnap()
	{
		Vector2 raw = GetLocalMousePosition();
		_gKind = GKind.None;
		_snapKeyNow = "";
		if (_clearMode) return ClearSnap(raw);
		if (_placing) UpdateDir(raw);
		if (Input.IsKeyPressed(Key.Alt)) return new RoadSnap(raw, null, null, Vector2.Zero);

		float z = Z;
		float reach = (_hover.Node != null ? SnapRadius * 1.4f : SnapRadius) / z;
		RoadSnap snap = Network.Snap(raw, reach, Level);
		if (snap.Node != null)
		{
			_snapKeyNow = $"n{snap.Node.Id}";
			return snap;
		}

		Cand best = default;
		float bestScore = float.MaxValue;
		bool has = false;

		void Consider(Vector2 pos, RoadSegment seg, GKind kind, Vector2 ga, Vector2 gb, string key, float strength, float maxPx)
		{
			float dpx = raw.DistanceTo(pos) * z;
			if (dpx > maxPx) return;
			float score = dpx / strength;
			if (score < bestScore)
			{
				bestScore = score;
				best = new Cand(pos, seg, kind, ga, gb, key);
				has = true;
			}
		}

		Vector2 p0 = _start.Position;

		if (snap.Segment != null)
		{
			RoadSegment seg = snap.Segment;
			Vector2[] sp = Network.PolyOf(seg);
			Consider(snap.Position, seg, GKind.None, default, default, $"s{seg.GetHashCode()}", 1f, SnapRadius);

			float total = RoadPoly.Length(sp);
			RoadPoly.Nearest(sp, snap.Position, out float off, out _);
			float k = Mathf.Round(off / RhythmStep);
			float ro = k * RhythmStep;
			if (ro > 12f && ro < total - 12f)
			{
				Vector2 rp = RoadPoly.PointAt(sp, ro);
				Consider(rp, seg, GKind.Rhythm, rp, RoadPoly.TangentAt(sp, ro), $"h{seg.GetHashCode()}:{k}", 1.3f, 9f);
			}

			if (_placing)
			{
				RoadPoly.Nearest(sp, p0, out _, out Vector2 foot);
				if (foot.DistanceTo(p0) * z > 14f)
					Consider(foot, seg, GKind.Perp, p0, foot, $"p{seg.GetHashCode()}", 1.4f, 14f);
			}
		}

		if (_placing)
		{
			Vector2 v = raw - p0;
			float len = v.Length();
			if (len > 4f / z)
			{
				if (IsArc)
				{
					Vector2 n = RoadGeometry.Left(_dir);
					float theta = Mathf.Atan2(v.Dot(n), v.Dot(_dir));
					float st = Mathf.DegToRad(AngleStepDeg) / 2f;
					float idx = Mathf.Round(theta / st);
					float ts = idx * st;
					Vector2 pa = p0 + (_dir * Mathf.Cos(ts) + n * Mathf.Sin(ts)) * len;
					Consider(pa, null, GKind.Angle, p0, pa, $"t{(int)idx}", 1f, SnapPx);
				}
				else
				{
					float ang = v.Angle();
					float step = LengthStep;
					while (step * z < 6f) step *= 2f;

					void Ray(Vector2 d, float strength)
					{
						float diff = Mathf.Abs(Mathf.Wrap(ang - d.Angle(), -Mathf.Pi, Mathf.Pi));
						if (diff > Mathf.DegToRad(20f)) return;
						float t = v.Dot(d);
						if (t <= 0f) return;
						Vector2 pp = p0 + d * t;
						float t2 = Mathf.Max(step, Mathf.Round(t / step) * step);
						Vector2 p2 = p0 + d * t2;
						if (p2.DistanceTo(pp) * z < 5f) pp = p2;
						string key = $"r{Mathf.RoundToInt(Mathf.RadToDeg(d.Angle()) * 10f)}";
						Consider(pp, null, GKind.Ray, p0, d, key, strength, SnapPx);
						foreach (RoadSegment s in Network.Segments)
						{
							if (s.Level != Level) continue;
							if (Network.RayHit(p0, d, s, out Vector2 h))
								Consider(h, s, GKind.Ray, p0, d, key, 1.6f, SnapPx + 2f);
						}
					}

					for (float a = 0f; a < Mathf.Tau - 0.01f; a += Mathf.DegToRad(AngleStepDeg))
						Ray(Vector2.FromAngle(a), 1f);
					foreach (Vector2 d in _dirCandidates) Ray(d, 1.3f);
				}
			}
		}

		foreach (RoadNode n in Network.Nodes)
		{
			if (n.Level != Level) continue;
			if (n.Position.DistanceTo(raw) * z < 14f) continue;
			if (_placing && n.Position.DistanceTo(p0) < 1f) continue;
			Vector2 ax = new(n.Position.X, raw.Y);
			Vector2 ay = new(raw.X, n.Position.Y);
			Consider(ax, null, GKind.AlignX, n.Position, ax, $"x{n.Id}", 0.8f, 8f);
			Consider(ay, null, GKind.AlignY, n.Position, ay, $"y{n.Id}", 0.8f, 8f);
		}

		if (has)
		{
			_gKind = best.Kind;
			_gA = best.GA;
			_gB = best.GB;
			_snapKeyNow = best.Key;
			return new RoadSnap(best.Pos, null, best.Seg, Vector2.Zero);
		}

		if (!_placing && GridSize > 0f)
			return new RoadSnap(new Vector2(Mathf.Round(raw.X / GridSize), Mathf.Round(raw.Y / GridSize)) * GridSize, null, null, Vector2.Zero);
		return new RoadSnap(raw, null, null, Vector2.Zero);
	}

	private (Curve2D curve, bool clamped) BuildCurve(Vector2 p1, RoadSnap target)
	{
		Vector2 p0 = _start.Position;
		if (!IsArc) return (RoadGeometry.Line(p0, p1).Curve, false);
		if (target.Node != null && target.Direction != Vector2.Zero)
			return (RoadGeometry.Hermite(p0, _dir, p1, -target.Direction), false);
		ArcInfo a = RoadGeometry.ArcClamped(p0, _dir, p1, MinRadius);
		return (a.Curve, a.Clamped);
	}

	private void UpdatePreview()
	{
		_pv = default;
		_cuts.Clear();
		Vector2 p0 = _start.Position, p1 = _hover.Position;
		if (p0.DistanceTo(p1) < DeadZonePx / Z) { _shown = null; return; }

		(Curve2D curve, bool clamped) = BuildCurve(p1, _hover);
		Vector2[] pts = RoadPoly.Of(curve);
		Vector2 endDir = RoadGeometry.EndTangent(curve);

		string reason = null;
		Vector2 at = p1;
		float minR = RoadGeometry.MinRadius(pts, out Vector2 sharpAt);

		if (RoadPoly.Length(pts) < MinLength) { reason = "ROAD_ERR_SHORT"; at = pts[pts.Length / 2]; }
		else if (minR < MinRadius * 0.9f) { reason = "ROAD_ERR_SHARP"; at = sharpAt; }
		else if (_hover.Segment != null && TooSharpJunction(_hover.Segment, _hover.Position, endDir)) reason = "ROAD_ERR_ANGLE";
		else if (Core.Core.Instance.CurrentSaveData.Balance < CostOf(curve)) reason = "ROAD_ERR_MONEY";

		float turn = IsArc ? Mathf.Abs(_dir.AngleTo(endDir)) : 0f;
		_pv = new Preview(curve, reason == null, clamped, reason, turn, at);
		_cuts = Network.PreviewCuts(curve, Level);
	}

	private void UpdateShown()
	{
		if (_smooth.DistanceTo(_hover.Position) * Z < 0.5f) { _shown = _pv.Curve; return; }
		_shown = BuildCurve(_smooth, _hover).curve;
	}

	private bool TooSharpJunction(RoadSegment seg, Vector2 pos, Vector2 arriveDir)
	{
		Vector2[] pts = Network.PolyOf(seg);
		RoadPoly.Nearest(pts, pos, out float off, out _);
		float a = Mathf.Abs(arriveDir.AngleTo(RoadPoly.TangentAt(pts, off)));
		a = Mathf.Min(a, Mathf.Pi - a);
		return a < Mathf.DegToRad(MinJunctionAngleDeg);
	}

	public override void _Draw()
	{
		if (!Active || Network == null) return;
		float px = 1f / Z;
		if (_clearMode) { DrawClear(px); return; }
		if (_parMode) { DrawParallel(px); return; }

		if (_hover.Segment != null)
			DrawPolyline(Network.PolyOf(_hover.Segment), Accent with { A = 0.22f }, _hover.Segment.Width, true);

		if (!_placing && !_hover.IsFree)
		{
			List<Vector2> dirs = Network.StartDirections(_hover);
			int main = dirs.Count / 3;
			for (int i = 0; i < dirs.Count; i++)
				Arrow(_hover.Position, dirs[i], Accent with { A = i < main ? 0.85f : 0.3f }, px);
		}

		DrawGuide(px);

		if (_placing)
		{
			DrawCircle(_start.Position, 3f * px, Accent);
			if (IsArc)
				DrawLine(_start.Position, _start.Position + _dir * 16f * px, Accent with { A = 0.6f }, 1.5f * px, true);
		}

		if (_placing && _pv.Curve != null)
		{
			Color col = Accent.Lerp(Bad, _badT);
			float alpha = _pv.Clamped ? 0.28f : 0.45f;
			DrawPolyline(RoadPoly.Of(_shown ?? _pv.Curve), col with { A = alpha }, LanesPerDirection * LaneWidth * 2f, true);

			foreach (Vector2 c in _cuts)
			{
				DrawCircle(c, 5f * px, Colors.White);
				DrawCircle(c, 3f * px, Accent);
			}

			if (_pv.Clamped)
			{
				Vector2 real = _pv.Curve.GetPointPosition(_pv.Curve.PointCount - 1);
				DrawDashedLine(real, _hover.Position, Ink with { A = 0.3f }, 1f * px, 5f * px);
				DrawCircle(real, 3f * px, col);
			}

			if (_pv.Reason != null)
			{
				float s = 5f * px;
				DrawLine(_pv.At + new Vector2(-s, -s), _pv.At + new Vector2(s, s), Bad, 1.5f * px, true);
				DrawLine(_pv.At + new Vector2(-s, s), _pv.At + new Vector2(s, -s), Bad, 1.5f * px, true);
			}
		}

		DrawCursor(px);
		DrawTexts(px);
	}

	private void Arrow(Vector2 pos, Vector2 d, Color c, float px)
	{
		Vector2 a = pos + d * 14f * px, b = pos + d * 34f * px;
		Vector2 l = RoadGeometry.Left(d);
		DrawLine(a, b, c, 1.5f * px, true);
		DrawLine(b, b - d * 6f * px + l * 4f * px, c, 1.5f * px, true);
		DrawLine(b, b - d * 6f * px - l * 4f * px, c, 1.5f * px, true);
	}

	private void DrawGuide(float px)
	{
		Color g = Accent with { A = 0.5f };
		switch (_gKind)
		{
			case GKind.Ray:
				DrawDashedLine(_gA, _hover.Position + _gB * 30f * px, Ink with { A = 0.25f }, 1f * px, 6f * px);
				break;
			case GKind.AlignX:
			case GKind.AlignY:
				DrawDashedLine(_gA, _gB, g, 1f * px, 5f * px);
				DrawCircle(_gA, 2.5f * px, g);
				break;
			case GKind.Rhythm:
			{
				Vector2 nrm = RoadGeometry.Left(_gB);
				DrawLine(_gA - nrm * 8f * px, _gA + nrm * 8f * px, g, 1.5f * px, true);
				break;
			}
			case GKind.Perp:
			{
				Vector2 foot = _gB;
				Vector2 din = (foot - _gA).Normalized();
				Vector2 t = RoadGeometry.Left(din);
				float s = 8f * px;
				DrawPolyline(new[] { foot - din * s, foot - din * s + t * s, foot + t * s }, g, 1.5f * px, true);
				break;
			}
		}
	}

	private void DrawCursor(float px)
	{
		bool deny = _placing && _pv.Curve != null && !_pv.Valid;
		Color rc = deny ? Bad : _hover.IsFree ? Ink with { A = 0.4f } : Accent;
		Vector2 rp = _ringPos;

		if (_hover.Node != null)
			DrawArc(rp, _ringR * px, 0f, Mathf.Tau, 28, rc, 1.5f * px, true);
		else if (_hover.Segment != null)
		{
			DrawArc(rp, 7f * px, 0f, Mathf.Tau, 24, rc, 1.5f * px, true);
			DrawCircle(rp, 2.5f * px, rc);
		}
		else DrawCircle(rp, 3f * px, rc);

		if (deny)
		{
			float s = 6f * px;
			DrawLine(rp + new Vector2(-s, s), rp + new Vector2(s, -s), Bad, 1.5f * px, true);
		}
	}

	private void DrawTexts(float px)
	{
		Vector2 mouse = GetLocalMousePosition();
		float sx = _shake > 0f ? Mathf.Sin(_shake * 60f) * 4f * (_shake / 0.25f) : 0f;
		Vector2 label = mouse + new Vector2(18f + sx, 22f) * px;

		if (Level > 0) Label(mouse + new Vector2(18f, -26f) * px, $"L{Level}", Ink with { A = 0.6f }, px);

		if (_placing && _pv.Curve != null)
		{
			string info = $"${CostOf(_pv.Curve):0}  ·  {_pv.Curve.GetBakedLength():0}";
			if (_pv.Turn > 0.01f) info += $"  ·  {Mathf.RadToDeg(_pv.Turn):0}°";
			info += $"  ·  {LanesPerDirection}x2";
			Label(label, info, Ink, px);
			if (_pv.Reason != null)
				Label(label + new Vector2(0f, 20f * px), TranslationServer.Translate(_pv.Reason), Bad, px);
		}
		else if (!_placing && LanesPerDirection > 1 && _hover.IsFree)
		{
			Label(label, $"{LanesPerDirection}x2", Ink with { A = 0.45f }, px);
		}

		if (_placing)
		{
			string hint = TranslationServer.Translate("ROAD_HINT");
			if (hint == "ROAD_HINT") hint = "Shift - прямая  ·  Alt - без снапа  ·  Backspace - шаг назад  ·  Ctrl+Z - отмена  ·  Ctrl+колесо - полосы";
			Vector2 pos = ScreenToLocal(new Vector2(16f, GetViewport().GetVisibleRect().Size.Y - 28f));
			Label(pos, hint, Ink with { A = 0.45f }, px);
		}
	}

	private void Label(Vector2 pos, string text, Color c, float px)
	{
		Font font = ThemeDB.FallbackFont;
		const int fs = 13;
		DrawSetTransform(pos, 0f, new Vector2(px, px));
		DrawString(font, new Vector2(0f, font.GetAscent(fs)), text, HorizontalAlignment.Left, -1, fs, c);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private void Play(AudioStreamPlayer player, AudioStreamWav fallback, float pitch)
	{
		if (!Sound || player == null) return;
		AudioStream custom = ReferenceEquals(player, _tickPlayer) ? SnapSound
			: ReferenceEquals(fallback, _defDeny) ? DenySound : BuildSound;
		player.Stream = custom ?? fallback;
		player.PitchScale = pitch;
		player.VolumeDb = SoundVolumeDb;
		player.Play();
	}

	private static AudioStreamWav MakeTone(float freq, float dur, float vol)
	{
		const int rate = 22050;
		int n = (int)(rate * dur);
		var data = new byte[n * 2];
		for (int i = 0; i < n; i++)
		{
			float t = i / (float)rate;
			float env = Mathf.Exp(-t * (6f / dur));
			short v = (short)(Mathf.Sin(Mathf.Tau * freq * t) * env * vol * short.MaxValue);
			data[i * 2] = (byte)(v & 0xFF);
			data[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
		}
		return new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = rate,
			Stereo = false,
			Data = data
		};
	}
}
