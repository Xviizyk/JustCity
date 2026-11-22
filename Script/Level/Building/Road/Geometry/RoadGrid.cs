using System.Collections.Generic;
using Godot;

namespace JustCity.Script.Level.Building.Road.Geometry;

public static class RoadGrid
{
    public static List<Vector2> Path(Vector2 a, Vector2 heading, Vector2 b, float leg)
    {
        Vector2[] dirs = new Vector2[8];
        for (int i = 0; i < 8; i++) dirs[i] = Vector2.FromAngle(i * Mathf.Pi / 4f);

        Vector2[] firsts = dirs;
        if (heading != Vector2.Zero) firsts = [heading.Normalized()];
        List<Vector2> best = TwoLegs(a, firsts, dirs, b, heading != Vector2.Zero, out float bestLength);

        if (best == null && heading != Vector2.Zero)
            for (int k = 1; k <= 4; k++)
            {
                Vector2 mid = a + heading.Normalized() * leg * k;
                List<Vector2> turn = [];
                foreach (Vector2 d in dirs) if (d.Dot(heading.Normalized()) > -0.2f) turn.Add(d);
                List<Vector2> rest = TwoLegs(mid, [.. turn], dirs, b, false, out float restLength);
                if (rest != null && leg * k + restLength < bestLength)
                {
                    bestLength = leg * k + restLength;
                    best = [a];
                    best.AddRange(rest);
                }
            }
        best ??= [a, b];
        return best;
    }

    private static List<Vector2> TwoLegs(Vector2 a, Vector2[] firsts, Vector2[] dirs, Vector2 b, bool keepStart, out float length)
    {
        List<Vector2> best = null;
        length = float.MaxValue;
        Vector2 delta = b - a;
        foreach (Vector2 d1 in firsts)
        {
            foreach (Vector2 d2 in dirs)
            {
                float det = d1.Cross(d2);
                if (Mathf.Abs(det) < 0.05f) continue;
                float s = delta.Cross(d2) / det, t = d1.Cross(delta) / det;
                if (s < -0.5f || t < -0.5f || s + t >= length) continue;
                if (keepStart && s <= 0.5f && t > 0.5f) continue;

                best = [a];
                if (s > 0.5f && t > 0.5f) best.Add(a + d1 * s);
                best.Add(b);
                length = s + t;
            }
        }
        return best;
    }

    public static Vector2[] Round(List<Vector2> pts, float radius)
    {
        List<Vector2> res = [pts[0]];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            Vector2 u = (pts[i] - pts[i - 1]).Normalized(), v = (pts[i + 1] - pts[i]).Normalized();
            float turn = Mathf.Abs(u.AngleTo(v));
            if (turn < 0.02f)
            {
                res.Add(pts[i]);
                continue;
            }

            float tan = Mathf.Tan(turn / 2f);
            float r = Mathf.Min(radius, Mathf.Min(pts[i].DistanceTo(pts[i - 1]), pts[i].DistanceTo(pts[i + 1])) * 0.5f / tan);
            Vector2 entry = pts[i] - u * r * tan;
            float side = Mathf.Sign(u.Cross(v));
            Vector2 center = entry + RoadGeometry.Left(u) * side * r;
            float start = (entry - center).Angle();
            int steps = Mathf.Max(2, Mathf.CeilToInt(turn / 0.12f));
            for (int k = 0; k <= steps; k++) res.Add(center + Vector2.FromAngle(start + side * turn * k / steps) * r);
        }
        res.Add(pts[^1]);
        return [.. res];
    }
}
