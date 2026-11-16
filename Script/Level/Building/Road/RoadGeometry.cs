using Godot;

namespace JustCity.Script.Level.Building;

public readonly record struct ArcInfo(Curve2D Curve, float Radius, float Sweep, bool Clamped = false);

public static class RoadGeometry
{
	public static Vector2 Left(Vector2 d) => new(-d.Y, d.X);

	public static ArcInfo Line(Vector2 a, Vector2 b)
	{
		var c = new Curve2D();
		c.AddPoint(a);
		c.AddPoint(b);
		return new ArcInfo(c, float.PositiveInfinity, 0f);
	}

	public static ArcInfo ArcClamped(Vector2 p0, Vector2 dir, Vector2 p1, float minR,
		float maxSweep = Mathf.Pi * 5f / 6f)
	{
		dir = dir.Normalized();
		Vector2 n = Left(dir), d = p1 - p0;
		float dist = d.Length();
		float theta = Mathf.Atan2(d.Dot(n), d.Dot(dir));
		if (Mathf.Abs(theta) < 0.003f) return Line(p0, p1);

		bool clamped = false;
		float maxTheta = maxSweep / 2f;
		if (Mathf.Abs(theta) > maxTheta)
		{
			theta = maxTheta * Mathf.Sign(theta);
			p1 = p0 + (dir * Mathf.Cos(theta) + n * Mathf.Sin(theta)) * dist;
			clamped = true;
		}

		float side = Mathf.Sign(theta);
		float r = dist / (2f * Mathf.Abs(Mathf.Sin(theta)));
		if (r < minR) { r = minR; clamped = true; }

		Vector2 c = p0 + n * side * r;
		float a0 = (p0 - c).Angle();
		float rel = Mathf.PosMod(side * ((p1 - c).Angle() - a0), Mathf.Tau);
		if (rel > Mathf.Pi * 1.5f) rel = 0f;
		float sweep = side * Mathf.Min(rel, maxSweep);
		if (Mathf.Abs(sweep) < 0.003f) return Line(p0, p1);

		return BuildArc(c, a0, r, sweep, clamped);
	}

	private static ArcInfo BuildArc(Vector2 c, float a0, float radius, float sweep, bool clamped)
	{
		var curve = new Curve2D();
		int pieces = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweep) / (Mathf.Pi / 2f)));
		float step = sweep / pieces;
		float k = 4f / 3f * Mathf.Tan(Mathf.Abs(step) / 4f) * radius;
		float sgn = Mathf.Sign(step);
		for (int i = 0; i <= pieces; i++)
		{
			float a = a0 + step * i;
			Vector2 pos = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
			Vector2 t = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * sgn;
			curve.AddPoint(pos, -t * k, t * k);
		}
		return new ArcInfo(curve, radius, sweep, clamped);
	}

	public static Curve2D Hermite(Vector2 p0, Vector2 t0, Vector2 p1, Vector2 t1)
	{
		float k = p0.DistanceTo(p1) / 3f;
		var c = new Curve2D();
		c.AddPoint(p0, Vector2.Zero, t0 * k);
		c.AddPoint(p1, -t1 * k, Vector2.Zero);
		return c;
	}

	public static float MinRadius(Vector2[] pts) => MinRadius(pts, out _);

	public static float MinRadius(Vector2[] pts, out Vector2 at)
	{
		float min = float.PositiveInfinity;
		at = pts.Length > 0 ? pts[pts.Length / 2] : Vector2.Zero;
		for (int i = 1; i < pts.Length - 1; i++)
		{
			Vector2 a = pts[i - 1], b = pts[i], c = pts[i + 1];
			float cr = Mathf.Abs((b - a).Cross(c - b));
			if (cr < 1e-3f) continue;
			float r = (b - a).Length() * (c - b).Length() * (c - a).Length() / (2f * cr);
			if (r < min) { min = r; at = b; }
		}
		return min;
	}

	public static Vector2 EndTangent(Curve2D c)
	{
		float len = c.GetBakedLength();
		if (len < 1f) return Vector2.Right;
		return (c.SampleBaked(len) - c.SampleBaked(len - 1f)).Normalized();
	}
}
