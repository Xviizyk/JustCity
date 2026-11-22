using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.Geometry;

public static class RoadCorners
{
	private static int ByAngle(Arm a, Arm b)
	{
		return a.Out.Angle().CompareTo(b.Out.Angle());
	}

	public static void Build(Vector2 node, List<Arm> arms, float radius, List<Vector2> tris, List<Vector2[]> rims)
	{
		if (arms.Count < 2) return;
		arms.Sort(ByAngle);
		for (int i = 0; i < arms.Count; i++)
		{
			Arm a = arms[i], b = arms[(i + 1) % arms.Count];
			Vector2 edgeA = a.Origin + RoadGeometry.Left(a.Out) * a.Half, edgeB = b.Origin - RoadGeometry.Left(b.Out) * b.Half;
			float den = a.Out.Cross(b.Out);
			if (Mathf.Abs(den) < 0.02f) continue;

			float ta = (edgeB - edgeA).Cross(b.Out) / den, tb = (edgeB - edgeA).Cross(a.Out) / den;
			Vector2 corner = edgeA + a.Out * ta;
			if (ta > 0f && tb > 0f) Fillet(a, b, corner, ta, tb, den, radius, tris, rims);
			else if (ta < 0f && tb < 0f)
			{
				float limit = 2f * Mathf.Max(a.Half, b.Half);
				List<Vector2> rim = [edgeA, edgeB];
				if (-ta <= limit && -tb <= limit) rim.Insert(1, corner);
				Fan(tris, node, rim);
				rims?.Add([.. rim]);
			}
		}
	}

	private static void Fillet(Arm a, Arm b, Vector2 corner, float ta, float tb, float sin, float radius, List<Vector2> tris, List<Vector2[]> rims)
	{
		float phi = Mathf.Atan2(sin, a.Out.Dot(b.Out));
		float tan = Mathf.Tan(phi / 2f);
		float touch = Mathf.Min(Mathf.Min(radius / tan, 2f * radius), Mathf.Min(a.Reach - ta, b.Reach - tb) * 0.8f);
		if (touch < 1.5f) return;

		float r = touch * tan;
		Vector2 bisector = (a.Out + b.Out).Normalized();
		Vector2 center = corner + bisector * (r / Mathf.Sin(phi / 2f));
		Vector2 touchA = corner + a.Out * touch, touchB = corner + b.Out * touch;
		float start = (touchA - center).Angle();
		float sweep = Mathf.Wrap((touchB - center).Angle() - start, -Mathf.Pi, Mathf.Pi);
		int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / 0.3f));

		List<Vector2> rim = [touchA];
		for (int k = 1; k < steps; k++) rim.Add(center + Vector2.FromAngle(start + sweep * k / steps) * r);
		rim.Add(touchB);
		Fan(tris, corner - bisector * 1.5f, rim);
		rims?.Add([.. rim]);
	}

	private static void Fan(List<Vector2> tris, Vector2 apex, List<Vector2> rim)
	{
		for (int i = 0; i + 1 < rim.Count; i++)
		{
			float cross = (rim[i] - apex).Cross(rim[i + 1] - apex);
			if (!float.IsFinite(cross) || Mathf.Abs(cross) < 2f) continue;
			tris.Add(apex);
			tris.Add(rim[i]);
			tris.Add(rim[i + 1]);
		}
	}

	public static void Draw(CanvasItem canvas, List<Vector2> tris, Color color, Vector2 offset)
	{
		if (tris.Count < 3) return;
		Vector2[] t = new Vector2[3];
		Color[] colors = [color, color, color];
		Vector2[] uvs = [Vector2.Zero, Vector2.Zero, Vector2.Zero];
		for (int i = 0; i + 2 < tris.Count; i += 3)
		{
			t[0] = tris[i] + offset;
			t[1] = tris[i + 1] + offset;
			t[2] = tris[i + 2] + offset;
			canvas.DrawPrimitive(t, colors, uvs);
		}
	}
}
