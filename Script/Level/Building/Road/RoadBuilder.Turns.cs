using System.Collections.Generic;
using Godot;

namespace JustCity.Script.Level.Building;

public partial class RoadBuilder
{
	private bool _turnMode;
	private RoadNode _turnNode;
	private RoadNode _turnHoverNode;
	private int _turnFromArm = -1;
	private int _turnFromLane = -1;
	private string _turnMsg;
	private float _turnMsgT;
	private int _hoverMarker = -1;
	private readonly List<TurnMarker> _markers = new();
	private List<Link> _turnLinks = new();

	private sealed record TurnMarker(Arm Arm, int Lane, bool Incoming, Vector2 Pos, Vector2 Dir);

	private void ToggleTurnMode()
	{
		Cancel();
		_clearMode = false;
		_parMode = false;
		_turnMode = !_turnMode;
		_turnNode = null;
		_turnFromArm = -1;
		Play(_tickPlayer, _defTick, 1.4f);
	}

	private bool TurnEscape()
	{
		if (!_turnMode) return false;
		if (_turnFromArm >= 0) { _turnFromArm = -1; return true; }
		if (_turnNode != null) { _turnNode = null; return true; }
		return false;
	}

	private void TurnReset()
	{
		if (_turnNode == null) return;
		RoadOp op = Network.ResetLinks(_turnNode);
		if (op == null) return;
		PushHistory(op, 0f);
		_turnFromArm = -1;
		Play(_fxPlayer, _defBuild, 0.9f);
	}

	private TurnMarker FindMarker(int armId, int lane, bool incoming)
	{
		foreach (TurnMarker m in _markers)
			if (m.Arm.Id == armId && m.Lane == lane && m.Incoming == incoming) return m;
		return null;
	}

	private void RefreshMarkers()
	{
		_markers.Clear();
		List<Arm> arms = Network.ArmsOf(_turnNode);
		float r = 14f;
		foreach (Arm a in arms) r = Mathf.Max(r, a.Width * 0.5f + 14f);

		foreach (Arm a in arms)
		{
			Vector2 u = a.Out;
			Vector2 side = RoadGeometry.Left(u);
			for (int k = 0; k < a.Lanes; k++)
			{
				float off = (k + 0.5f) * a.Seg.LaneWidth;
				_markers.Add(new TurnMarker(a, k, true, _turnNode.Position + u * r - side * off, -u));
				_markers.Add(new TurnMarker(a, k, false, _turnNode.Position + u * r + side * off, u));
			}
		}
		_turnLinks = Network.LinksOf(_turnNode);
	}

	private void UpdateTurns()
	{
		float dt = (float)GetProcessDeltaTime();
		_turnMsgT = Mathf.Max(0f, _turnMsgT - dt);
		_hoverMarker = -1;
		_turnHoverNode = null;

		Vector2 raw = GetLocalMousePosition();
		float z = Z;

		if (_turnNode != null && !Network.Nodes.Contains(_turnNode))
		{
			_turnNode = null;
			_turnFromArm = -1;
		}

		if (_turnNode != null)
		{
			RefreshMarkers();
			float bd = 14f;
			for (int i = 0; i < _markers.Count; i++)
			{
				float d = _markers[i].Pos.DistanceTo(raw) * z;
				if (d < bd) { bd = d; _hoverMarker = i; }
			}
			if (_turnFromArm >= 0 && FindMarker(_turnFromArm, _turnFromLane, true) == null)
				_turnFromArm = -1;
		}

		if (_hoverMarker < 0)
		{
			RoadNode best = null;
			float bd = 18f;
			foreach (RoadNode n in Network.Nodes)
			{
				if (n.Level != Level) continue;
				float d = n.Position.DistanceTo(raw) * z;
				if (d < bd) { bd = d; best = n; }
			}
			if (best != null && Network.ArmsOf(best).Count >= 2) _turnHoverNode = best;
		}
	}

	private void TurnPressed()
	{
		if (_hoverMarker >= 0)
		{
			TurnMarker m = _markers[_hoverMarker];
			if (m.Incoming)
			{
				bool same = m.Arm.Id == _turnFromArm && m.Lane == _turnFromLane;
				_turnFromArm = same ? -1 : m.Arm.Id;
				_turnFromLane = same ? -1 : m.Lane;
				Play(_tickPlayer, _defTick, 1.2f);
			}
			else if (_turnFromArm < 0)
			{
				_turnMsg = "ROAD_LINK_PICK";
				_turnMsgT = 1.6f;
				Deny();
			}
			else
			{
				var link = new Link(_turnFromArm, _turnFromLane, m.Arm.Id, m.Lane);
				RoadOp op = Network.TryToggleLink(_turnNode, link, out string reason);
				if (op == null)
				{
					_turnMsg = reason;
					_turnMsgT = 2.2f;
					Deny();
				}
				else
				{
					PushHistory(op, 0f);
					Play(_tickPlayer, _defTick, 1.5f);
				}
			}
			return;
		}

		if (_turnHoverNode != null)
		{
			_turnNode = _turnHoverNode;
			_turnFromArm = -1;
			Play(_tickPlayer, _defTick, 1.3f);
			return;
		}

		if (!TurnEscape()) return;
	}

	private static string TurnText(string key) => key switch
	{
		"ROAD_LINK_NO_EXIT" => Tr(key, "У полосы должен остаться выезд"),
		"ROAD_LINK_NO_ENTRY" => Tr(key, "В полосу должен остаться въезд"),
		"ROAD_LINK_DISCONNECTED" => Tr(key, "Дороги перестанут быть связаны"),
		"ROAD_LINK_UTURN" => Tr(key, "Разворот на перекрёстке запрещён"),
		"ROAD_LINK_PICK" => Tr(key, "Сначала выберите входящую полосу"),
		_ => Tr(key, key)
	};

	private void DrawLink(TurnMarker a, TurnMarker b, Color c, float width)
	{
		Vector2 p0 = a.Pos, p3 = b.Pos;
		float k = Mathf.Max(10f, p0.DistanceTo(p3) * 0.45f);
		Vector2 p1 = p0 + a.Dir * k, p2 = p3 - b.Dir * k;
		var pts = new Vector2[17];
		for (int i = 0; i <= 16; i++) pts[i] = p0.BezierInterpolate(p1, p2, p3, i / 16f);
		DrawPolyline(pts, c, width, true);
	}

	private void DrawTurns(float px)
	{
		if (_turnHoverNode != null && _turnHoverNode != _turnNode)
			DrawArc(_turnHoverNode.Position, 14f * px, 0f, Mathf.Tau, 28, Accent with { A = 0.8f }, 1.5f * px, true);

		if (_turnNode != null)
		{
			float ring = 14f;
			foreach (TurnMarker m in _markers) ring = Mathf.Max(ring, m.Pos.DistanceTo(_turnNode.Position) + 6f);
			DrawArc(_turnNode.Position, ring, 0f, Mathf.Tau, 48, Accent with { A = 0.25f }, 1f * px, true);

			bool picked = _turnFromArm >= 0;
			foreach (Link l in _turnLinks)
			{
				TurnMarker a = FindMarker(l.FromArm, l.FromLane, true);
				TurnMarker b = FindMarker(l.ToArm, l.ToLane, false);
				if (a == null || b == null) continue;
				bool sel = l.FromArm == _turnFromArm && l.FromLane == _turnFromLane;
				if (sel) DrawLink(a, b, Accent with { A = 0.95f }, 2.5f * px);
				else DrawLink(a, b, Ink with { A = picked ? 0.15f : 0.4f }, 2f * px);
			}

			for (int i = 0; i < _markers.Count; i++)
			{
				TurnMarker m = _markers[i];
				bool hov = i == _hoverMarker;
				if (m.Incoming)
				{
					bool selected = m.Arm.Id == _turnFromArm && m.Lane == _turnFromLane;
					Color c = selected || hov ? Accent : Ink with { A = 0.7f };
					Vector2 d = m.Dir, s = RoadGeometry.Left(d);
					DrawColoredPolygon(new[] { m.Pos + d * 5f * px, m.Pos - d * 3f * px + s * 4f * px, m.Pos - d * 3f * px - s * 4f * px }, c);
				}
				else
				{
					bool linked = picked && _turnLinks.Contains(new Link(_turnFromArm, _turnFromLane, m.Arm.Id, m.Lane));
					bool sameArm = picked && m.Arm.Id == _turnFromArm;
					Color c = hov ? Accent : Ink with { A = sameArm ? 0.2f : 0.7f };
					if (linked) DrawCircle(m.Pos, 4.5f * px, Accent);
					else DrawArc(m.Pos, 4f * px, 0f, Mathf.Tau, 16, c, 1.5f * px, true);
				}
			}
		}

		Vector2 mouse = GetLocalMousePosition();
		DrawCircle(mouse, 3f * px, Ink with { A = 0.4f });
		Vector2 label = mouse + new Vector2(18f, 22f) * px;

		if (_turnMsgT > 0f && _turnMsg != null)
			Label(label, TurnText(_turnMsg), Bad, px);
		else if (_turnNode == null)
			Label(label, _turnHoverNode != null ? Tr("ROAD_TURN_CLICK", "Клик - настроить повороты") : Tr("ROAD_TURN_MODE", "Повороты: выберите перекрёсток"), Ink, px);
		else if (_hoverMarker >= 0)
		{
			TurnMarker m = _markers[_hoverMarker];
			if (m.Incoming) Label(label, Tr("ROAD_TURN_PICK_LANE", "Клик - выбрать полосу"), Ink, px);
			else if (_turnFromArm >= 0)
			{
				bool linked = _turnLinks.Contains(new Link(_turnFromArm, _turnFromLane, m.Arm.Id, m.Lane));
				Label(label, linked ? Tr("ROAD_TURN_REMOVE", "Клик - убрать поворот") : Tr("ROAD_TURN_ADD", "Клик - добавить поворот"), Ink, px);
			}
		}

		string hint = Tr("ROAD_TURN_HINT", "Входящая полоса, затем выходящая - добавить/убрать поворот  ·  R - по умолчанию  ·  Esc - назад  ·  Ctrl+Z - отмена");
		Label(ScreenToLocal(new Vector2(16f, GetViewport().GetVisibleRect().Size.Y - 28f)), hint, Ink with { A = 0.45f }, px);
	}
}
