using System.Collections.Generic;
using Godot;
using JustCity.Script.Core.Save;

namespace JustCity.Script.Level.Building;

public partial class RoadBuilder
{
	[ExportGroup("Clear")]
	[Export] public float ClearCostPerPixel = 0.25f;

	private bool _clearMode;
	private bool _clearing;
	private bool _clearDrag;
	private Vector2 _clearA;
	private Vector2 _clearPressScreen;
	private ClearPlan _clearPlan;
	private readonly List<(Vector2 a, Vector2 b)> _clearGuides = new();

	private static string Tr(string key, string fallback)
	{
		string t = TranslationServer.Translate(key);
		return t == key ? fallback : t;
	}

	private void PushHistory(RoadOp op, float cost)
	{
		_history.Add(new Entry(op, cost, default, Vector2.Zero, new List<Vector2>()));
		_redo.Clear();
	}

	private static Rect2 RectOf(Vector2 a, Vector2 b)
	{
		Vector2 mn = a.Min(b), mx = a.Max(b);
		return new Rect2(mn, mx - mn);
	}

	private void ToggleClearMode()
	{
		Cancel();
		_parMode = false;
		_clearMode = !_clearMode;
		Play(_tickPlayer, _defTick, 1.4f);
	}

	private void ToggleParallelMode()
	{
		Cancel();
		_clearMode = false;
		_parMode = !_parMode;
		Play(_tickPlayer, _defTick, 1.4f);
	}

	private void ExitModes()
	{
		_clearMode = false;
		_parMode = false;
		Cancel();
	}

	private RoadSnap ClearSnap(Vector2 raw)
	{
		_clearGuides.Clear();
		Vector2 p = raw;
		if (Input.IsKeyPressed(Key.Alt)) return new RoadSnap(p, null, null, Vector2.Zero);

		float z = Z;
		if (GridSize > 0f) p = new Vector2(Mathf.Round(p.X / GridSize), Mathf.Round(p.Y / GridSize)) * GridSize;

		float bx = 8f, by = 8f;
		RoadNode nx = null, ny = null;
		foreach (RoadNode n in Network.Nodes)
		{
			if (n.Level != Level) continue;
			float dx = Mathf.Abs(n.Position.X - raw.X) * z;
			float dy = Mathf.Abs(n.Position.Y - raw.Y) * z;
			if (dx < bx) { bx = dx; nx = n; }
			if (dy < by) { by = dy; ny = n; }
		}

		bool sx = false, sy = false;
		if (nx != null) { p.X = nx.Position.X; sx = true; _snapKeyNow = $"cx{nx.Id}"; }
		if (ny != null) { p.Y = ny.Position.Y; sy = true; _snapKeyNow += $"cy{ny.Id}"; }

		if (_clearing)
		{
			Vector2 a = _clearA;
			if (!sx && Mathf.Abs(a.X - raw.X) * z < 8f) { p.X = a.X; sx = true; }
			if (!sy && Mathf.Abs(a.Y - raw.Y) * z < 8f) { p.Y = a.Y; sy = true; }
			if (!sx && !sy)
			{
				float w = raw.X - a.X, h = raw.Y - a.Y;
				if (Mathf.Abs(Mathf.Abs(w) - Mathf.Abs(h)) * z < 8f)
				{
					float m = (Mathf.Abs(w) + Mathf.Abs(h)) * 0.5f;
					p = a + new Vector2(Mathf.Sign(w) * m, Mathf.Sign(h) * m);
					_snapKeyNow = "square";
				}
			}
		}

		if (nx != null) _clearGuides.Add((nx.Position, p));
		if (ny != null) _clearGuides.Add((ny.Position, p));
		return new RoadSnap(p, null, null, Vector2.Zero);
	}

	private void ClearPressed()
	{
		if (!_clearing)
		{
			_clearing = true;
			_clearA = _hover.Position;
			_clearDrag = true;
			_clearPressScreen = GetViewport().GetMousePosition();
			_clearPlan = null;
			Play(_tickPlayer, _defTick, 1.25f);
		}
		else CommitClear();
	}

	private void ClearReleased()
	{
		if (_clearing && _clearDrag
			&& GetViewport().GetMousePosition().DistanceTo(_clearPressScreen) > 12f)
			CommitClear();
		_clearDrag = false;
	}

	private void UpdateClear()
	{
		if (!_clearing) { _clearPlan = null; return; }
		Rect2 r = RectOf(_clearA, _hover.Position);
		_clearPlan = (r.Size.X < 2f / Z && r.Size.Y < 2f / Z)
			? null
			: Network.PlanClear(r, Level, ClearCostPerPixel, BridgeCostMultiplier);
	}

	private void CommitClear()
	{
		Rect2 r = RectOf(_clearA, _hover.Position);
		ClearPlan plan = Network.PlanClear(r, Level, ClearCostPerPixel, BridgeCostMultiplier);
		SaveData sd = Core.Core.Instance.CurrentSaveData;

		if (plan.Items.Count == 0 || sd.Balance < plan.Cost) { Deny(); return; }

		RoadOp op = Network.Clear(plan);
		if (op == null) { Deny(); return; }
		sd.Balance -= plan.Cost;
		PushHistory(op, plan.Cost);
		Play(_fxPlayer, _defBuild, 0.7f);

		_clearing = false;
		_clearDrag = false;
		_clearPlan = null;
	}

	private void DrawClear(float px)
	{
		Color red = Bad;

		foreach (var (a, b) in _clearGuides)
		{
			DrawDashedLine(a, b, Accent with { A = 0.5f }, 1f * px, 5f * px);
			DrawCircle(a, 2.5f * px, Accent with { A = 0.5f });
		}

		if (_clearing)
		{
			Rect2 r = RectOf(_clearA, _hover.Position);
			DrawRect(r, red with { A = 0.07f }, true);
			DrawRect(r, red with { A = 0.7f }, false, 1.5f * px);

			if (_clearPlan != null)
			{
				foreach (SegClear it in _clearPlan.Items)
					foreach (var (a, b, inside) in it.Parts)
						if (inside)
							DrawPolyline(RoadPoly.Sub(it.Pts, a, b), red with { A = 0.45f }, it.Seg.Width, true);

				foreach (Vector2 c in _clearPlan.Cuts)
				{
					DrawCircle(c, 5f * px, Colors.White);
					DrawCircle(c, 3f * px, red);
				}
			}
		}

		Vector2 m = GetLocalMousePosition();
		float s = 6f * px;
		DrawLine(_hover.Position + new Vector2(-s, 0f), _hover.Position + new Vector2(s, 0f), red, 1.5f * px, true);
		DrawLine(_hover.Position + new Vector2(0f, -s), _hover.Position + new Vector2(0f, s), red, 1.5f * px, true);

		Vector2 label = m + new Vector2(18f, 22f) * px;
		if (!_clearing) Label(label, Tr("ROAD_MODE_CLEAR", "Очистка"), Ink, px);
		else if (_clearPlan != null && _clearPlan.Items.Count > 0)
		{
			Label(label, $"${_clearPlan.Cost:0}  ·  {_clearPlan.Length:0}", Ink, px);
			if (Core.Core.Instance.CurrentSaveData.Balance < _clearPlan.Cost)
				Label(label + new Vector2(0f, 20f * px), Tr("ROAD_ERR_MONEY", "Не хватает денег"), Bad, px);
		}
		else if (_clearPlan != null)
			Label(label, Tr("ROAD_CLEAR_EMPTY", "Здесь нечего чистить"), Bad, px);

		string hint = Tr("ROAD_CLEAR_HINT", "Два клика или протяжка - очистить область  ·  Alt - без снапа  ·  Esc - выход  ·  Ctrl+Z - отмена");
		Label(ScreenToLocal(new Vector2(16f, GetViewport().GetVisibleRect().Size.Y - 28f)), hint, Ink with { A = 0.45f }, px);
	}
}
