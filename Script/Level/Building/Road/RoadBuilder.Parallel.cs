using System.Collections.Generic;
using Godot;
using JustCity.Script.Core.Save;

namespace JustCity.Script.Level.Building;

public partial class RoadBuilder
{
	[ExportGroup("Parallel")]
	[Export] public float ParallelStep = 8f;
	[Export] public float ParallelMargin = 4f;

	private bool _parMode;
	private RoadSegment _parRef;
	private RoadSegment _parHover;
	private float _parSideSign = 1f;
	private float _parGap;
	private Curve2D _parCurve;
	private bool _parValid;
	private string _parReason;
	private Vector2 _parAt;
	private List<Vector2> _parCuts = new();

	private RoadSegment SegmentAt(Vector2 raw, float tolPx)
	{
		RoadSegment best = null;
		float bd = tolPx / Z;
		foreach (RoadSegment s in Network.Segments)
		{
			if (s.Level != Level) continue;
			float d = RoadPoly.Nearest(Network.PolyOf(s), raw, out _, out _) - s.Width * 0.5f;
			if (d < bd) { bd = d; best = s; }
		}
		return best;
	}

	private static Vector2[] OffsetPoly(Vector2[] src, float d)
	{
		var p = new List<Vector2>();
		foreach (Vector2 v in src)
			if (p.Count == 0 || p[^1].DistanceTo(v) > 1e-3f) p.Add(v);
		if (p.Count < 2) return p.ToArray();

		var res = new Vector2[p.Count];
		for (int i = 0; i < p.Count; i++)
		{
			Vector2 e0 = i > 0 ? (p[i] - p[i - 1]).Normalized() : (p[1] - p[0]).Normalized();
			Vector2 e1 = i < p.Count - 1 ? (p[i + 1] - p[i]).Normalized() : e0;
			Vector2 n0 = RoadGeometry.Left(e0), n1 = RoadGeometry.Left(e1);
			Vector2 nm = n0 + n1;
			nm = nm.Length() < 1e-4f ? n0 : nm.Normalized();
			float c = Mathf.Max(nm.Dot(n0), 0.35f);
			res[i] = p[i] + nm * d / c;
		}
		return res;
	}

	private void ParallelPressed()
	{
		if (_parRef == null)
		{
			if (_parHover != null)
			{
				_parRef = _parHover;
				Play(_tickPlayer, _defTick, 1.25f);
			}
			return;
		}
		if (_parCurve == null || !_parValid) { Deny(); return; }

		SaveData sd = Core.Core.Instance.CurrentSaveData;
		float cost = CostOf(_parCurve);
		if (sd.Balance < cost) { Deny(); return; }

		RoadOp op = Network.Build(_parCurve, LanesPerDirection, LaneWidth, Level);
		if (op == null) { Deny(); return; }
		sd.Balance -= cost;
		PushHistory(op, cost);
		_renderer.Grow(op.Built);
		Play(_fxPlayer, _defBuild, 1f);
	}

	private void UpdateParallel()
	{
		Vector2 raw = GetLocalMousePosition();
		_parCurve = null;
		_parValid = false;
		_parReason = null;
		_parCuts.Clear();

		if (_parRef != null && !Network.Segments.Contains(_parRef)) _parRef = null;
		if (_parRef == null) { _parHover = SegmentAt(raw, 12f); return; }

		Vector2[] pts = Network.PolyOf(_parRef);
		RoadPoly.Nearest(pts, raw, out float off, out Vector2 foot);
		Vector2 n = RoadGeometry.Left(RoadPoly.TangentAt(pts, off));
		float sd = (raw - foot).Dot(n);

		float sign = sd >= 0f ? 1f : -1f;
		if (Mathf.Abs(sd) < 4f / Z) sign = _parSideSign;
		_parSideSign = sign;

		float minGap = _parRef.Width * 0.5f + LanesPerDirection * LaneWidth + ParallelMargin;
		float step = ParallelStep;
		while (step * Z < 6f) step *= 2f;
		float want = Mathf.Abs(sd);
		float gap = want <= minGap ? minGap : minGap + Mathf.Round((want - minGap) / step) * step;
		_parGap = gap;

		Vector2[] offPts = OffsetPoly(pts, sign * gap);
		if (offPts.Length < 2) return;
		_parCurve = RoadPoly.ToCurve(offPts);

		Vector2[] np = RoadPoly.Of(_parCurve);
		float minR = RoadGeometry.MinRadius(np, out Vector2 sharpAt);
		_parAt = np[^1];

		if (RoadPoly.Length(np) < MinLength) { _parReason = "ROAD_ERR_SHORT"; _parAt = np[np.Length / 2]; }
		else if (minR < MinRadius * 0.9f) { _parReason = "ROAD_ERR_SHARP"; _parAt = sharpAt; }
		else if (Core.Core.Instance.CurrentSaveData.Balance < CostOf(_parCurve)) _parReason = "ROAD_ERR_MONEY";

		_parValid = _parReason == null;
		_parCuts = Network.PreviewCuts(_parCurve, Level);
	}

	private void DrawParallel(float px)
	{
		RoadSegment hi = _parRef ?? _parHover;
		if (hi != null)
			DrawPolyline(Network.PolyOf(hi), Accent with { A = 0.28f }, hi.Width, true);

		if (_parRef != null && _parCurve != null)
		{
			Color col = Accent.Lerp(Bad, _badT);
			DrawPolyline(RoadPoly.Of(_parCurve), col with { A = 0.45f }, LanesPerDirection * LaneWidth * 2f, true);

			foreach (Vector2 c in _parCuts)
			{
				DrawCircle(c, 5f * px, Colors.White);
				DrawCircle(c, 3f * px, Accent);
			}
			if (_parReason != null)
			{
				float s = 5f * px;
				DrawLine(_parAt + new Vector2(-s, -s), _parAt + new Vector2(s, s), Bad, 1.5f * px, true);
				DrawLine(_parAt + new Vector2(-s, s), _parAt + new Vector2(s, -s), Bad, 1.5f * px, true);
			}
		}

		Vector2 m = GetLocalMousePosition();
		DrawCircle(m, 3f * px, Ink with { A = 0.4f });

		Vector2 label = m + new Vector2(18f, 22f) * px;
		if (_parRef == null)
			Label(label, Tr("ROAD_PAR_PICK", "Параллель: выберите дорогу"), Ink, px);
		else if (_parCurve != null)
		{
			Label(label, $"${CostOf(_parCurve):0}  ·  {_parGap:0}  ·  {LanesPerDirection}x2", Ink, px);
			if (_parReason != null)
				Label(label + new Vector2(0f, 20f * px), Tr(_parReason, _parReason), Bad, px);
		}

		string hint = Tr("ROAD_PAR_HINT", "Клик по дороге - основа  ·  клик - построить  ·  Esc - другая дорога / выход  ·  [ ] - полосы");
		Label(ScreenToLocal(new Vector2(16f, GetViewport().GetVisibleRect().Size.Y - 28f)), hint, Ink with { A = 0.45f }, px);
	}
}
