using System.Collections.Generic;
using Godot;

namespace JustCity.Script.Level.Building;

public static class RoadPoly
{
	public static Vector2[] Of(Curve2D curve) => curve.Tessellate(5, 2f);

	public static float Length(Vector2[] p)
	{
		float l = 0f;
		for (int i = 1; i < p.Length; i++) l += p[i - 1].DistanceTo(p[i]);
		return l;
	}

	public static float Nearest(Vector2[] p, Vector2 q, out float offset, out Vector2 point)
	{
		float best = float.MaxValue;
		offset = 0f;
		point = p[0];
		float acc = 0f;
		for (int i = 1; i < p.Length; i++)
		{
			Vector2 a = p[i - 1], b = p[i];
			Vector2 ab = b - a;
			float len = ab.Length();
			float t = len < 1e-4f ? 0f : Mathf.Clamp((q - a).Dot(ab) / (len * len), 0f, 1f);
			Vector2 c = a + ab * t;
			float d = c.DistanceTo(q);
			if (d < best)
			{
				best = d;
				offset = acc + len * t;
				point = c;
			}
			acc += len;
		}
		return best;
	}

	public static Vector2 PointAt(Vector2[] p, float offset)
	{
		float acc = 0f;
		for (int i = 1; i < p.Length; i++)
		{
			float len = p[i - 1].DistanceTo(p[i]);
			if (acc + len >= offset)
			{
				float t = len < 1e-4f ? 0f : (offset - acc) / len;
				return p[i - 1].Lerp(p[i], Mathf.Clamp(t, 0f, 1f));
			}
			acc += len;
		}
		return p[^1];
	}

	public static Vector2 TangentAt(Vector2[] p, float offset)
	{
		float acc = 0f;
		for (int i = 1; i < p.Length; i++)
		{
			float len = p[i - 1].DistanceTo(p[i]);
			if (acc + len >= offset || i == p.Length - 1)
				return len < 1e-4f ? Vector2.Right : (p[i] - p[i - 1]) / len;
			acc += len;
		}
		return Vector2.Right;
	}

	public static Vector2[] Sub(Vector2[] p, float from, float to)
	{
		var res = new List<Vector2> { PointAt(p, from) };
		float acc = 0f;
		for (int i = 1; i < p.Length; i++)
		{
			acc += p[i - 1].DistanceTo(p[i]);
			if (acc > from + 0.01f && acc < to - 0.01f) res.Add(p[i]);
		}
		res.Add(PointAt(p, to));
		return res.ToArray();
	}

	public static Curve2D ToCurve(Vector2[] pts)
	{
		var c = new Curve2D();
		foreach (Vector2 v in pts) c.AddPoint(v);
		return c;
	}

	public static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t, out Vector2 point)
	{
		t = 0f;
		point = default;
		Vector2 r = b - a, s = d - c;
		float den = r.Cross(s);
		if (Mathf.Abs(den) < 1e-6f) return false;
		Vector2 ca = c - a;
		t = ca.Cross(s) / den;
		float u = ca.Cross(r) / den;
		if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
		point = a + r * t;
		return true;
	}
}
