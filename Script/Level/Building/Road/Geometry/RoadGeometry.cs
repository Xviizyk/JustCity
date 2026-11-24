using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JustCity.Script.Level.Building.Road.Geometry;

public static class RoadGeometry
{
    public static Vector2 Left(Vector2 d)
    {
        return new Vector2(-d.Y, d.X);
    }

    public static Curve2D Line(Vector2 a, Vector2 b)
    {
        Curve2D c = new();
        c.AddPoint(a);
        c.AddPoint(b);
        return c;
    }

    public static Curve2D Arc(Vector2 p0, Vector2 dir, Vector2 p1, float minRadius, out bool clamped)
    {
        clamped = false;
        dir = dir.Normalized();
        Vector2 normal = Left(dir);
        Vector2 chord = p1 - p0;
        float dist = chord.Length();
        float theta = Mathf.Atan2(chord.Dot(normal), chord.Dot(dir));
        if (Mathf.Abs(theta) < 0.003f) return Line(p0, p1);

        float maxTheta = Mathf.Pi - 0.15f;
        if (Mathf.Abs(theta) > maxTheta)
        {
            theta = maxTheta * Mathf.Sign(theta);
            p1 = p0 + (dir * Mathf.Cos(theta) + normal * Mathf.Sin(theta)) * dist;
            clamped = true;
        }

        float side = Mathf.Sign(theta);
        float radius = dist / (2f * Mathf.Abs(Mathf.Sin(theta)));
        float sweep = 2f * Mathf.Abs(theta);
        Vector2 center = p0 + normal * side * radius;
        float start = (p0 - center).Angle();
        if (radius < minRadius)
        {
            clamped = true;
            radius = minRadius;
            center = p0 + normal * side * radius;
            start = (p0 - center).Angle();
            sweep = Mathf.PosMod(side * ((p1 - center).Angle() - start), Mathf.Tau);
            if (sweep > Mathf.Pi * 1.5f) sweep = 0f;
        }
        if (sweep < 0.003f) return Line(p0, p1);
        return ArcCurve(center, start, radius, side * sweep);
    }

    public static Curve2D ArcCurve(Vector2 center, float start, float radius, float sweep)
    {
        Curve2D curve = new();
        int pieces = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(sweep) / (Mathf.Pi / 2f)));
        float step = sweep / pieces;
        float sign = Mathf.Sign(sweep);
        float handle = 4f / 3f * Mathf.Tan(Mathf.Abs(step) / 4f) * radius;
        for (int i = 0; i <= pieces; i++)
        {
            float a = start + step * i;
            Vector2 tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * sign;
            curve.AddPoint(center + Vector2.FromAngle(a) * radius, -tangent * handle, tangent * handle);
        }
        return curve;
    }

    public static Curve2D Circular(Vector2 a, Vector2 b, Vector2 heading)
    {
        Vector2 center = (a + b) / 2f;
        float start = (a - center).Angle();
        float side = 1f;
        if (heading != Vector2.Zero && new Vector2(-Mathf.Sin(start), Mathf.Cos(start)).Dot(heading) < 0f) side = -1f;
        return ArcCurve(center, start, a.DistanceTo(b) / 2f, side * Mathf.Pi);
    }

    public static Curve2D Hermite(Vector2 p0, Vector2 t0, Vector2 p1, Vector2 t1)
    {
        float handle = p0.DistanceTo(p1) / 3f;
        Curve2D curve = new();
        curve.AddPoint(p0, Vector2.Zero, t0 * handle);
        curve.AddPoint(p1, -t1 * handle, Vector2.Zero);
        return curve;
    }

    public static Curve2D Reverse(Curve2D c)
    {
        Curve2D r = new();
        for (int i = c.PointCount - 1; i >= 0; i--) r.AddPoint(c.GetPointPosition(i), c.GetPointOut(i), c.GetPointIn(i));
        return r;
    }

    public static float MinRadius(Vector2[] pts, out Vector2 at)
    {
        float min = float.PositiveInfinity;
        at = pts[pts.Length / 2];
        for (int i = 1; i < pts.Length - 1; i++)
        {
            float cross = Mathf.Abs((pts[i] - pts[i - 1]).Cross(pts[i + 1] - pts[i]));
            if (cross < 1e-3f) continue;
            float radius = pts[i - 1].DistanceTo(pts[i]) * pts[i].DistanceTo(pts[i + 1]) * pts[i - 1].DistanceTo(pts[i + 1]) / (2f * cross);
            if (radius < min)
            {
                min = radius;
                at = pts[i];
            }
        }
        return min;
    }

    public static Vector2 StartTangent(Curve2D c)
    {
        if (c.GetBakedLength() < 1f) return Vector2.Right;
        return (c.SampleBaked(1f) - c.SampleBaked(0f)).Normalized();
    }

    public static Vector2 EndTangent(Curve2D c)
    {
        float len = c.GetBakedLength();
        if (len < 1f) return Vector2.Right;
        return (c.SampleBaked(len) - c.SampleBaked(len - 1f)).Normalized();
    }

    public static Vector2[] Poly(Curve2D c)
    {
        int n = c.PointCount;
        if (n == 0) return [];
        Vector2 o = c.GetPointPosition(0);
        Curve2D local = new();
        for (int i = 0; i < n; i++) local.AddPoint(c.GetPointPosition(i) - o, c.GetPointIn(i), c.GetPointOut(i));
        Vector2[] raw = local.Tessellate(5, 2f);
        List<Vector2> pts = [];
        foreach (Vector2 p in raw) if (pts.Count == 0 || pts[^1].DistanceSquaredTo(p) > 1e-4f) pts.Add(p + o);
        return pts.Count < 2 ? [.. raw.Select(p => p + o)] : [.. pts];
    }

    public static float Length(Vector2[] pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Length; i++) len += pts[i - 1].DistanceTo(pts[i]);
        return len;
    }

    public static Rect2 Bounds(Vector2[] pts)
    {
        Vector2 min = pts[0], max = pts[0];
        foreach (Vector2 p in pts)
        {
            min = min.Min(p);
            max = max.Max(p);
        }
        return new Rect2(min, max - min).Grow(1f);
    }

    public static float Nearest(Vector2[] pts, Vector2 q, out float offset, out Vector2 point)
    {
        float best = float.MaxValue, walked = 0f;
        offset = 0f;
        point = pts[0];
        for (int i = 1; i < pts.Length; i++)
        {
            Vector2 ab = pts[i] - pts[i - 1];
            float len = ab.Length();
            float t = len < 1e-4f ? 0f : Mathf.Clamp((q - pts[i - 1]).Dot(ab) / (len * len), 0f, 1f);
            Vector2 closest = pts[i - 1] + ab * t;
            if (closest.DistanceTo(q) < best)
            {
                best = closest.DistanceTo(q);
                offset = walked + len * t;
                point = closest;
            }
            walked += len;
        }
        return best;
    }

    public static Vector2 PointAt(Vector2[] pts, float offset)
    {
        float walked = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            float len = pts[i - 1].DistanceTo(pts[i]);
            if (walked + len >= offset) return pts[i - 1].Lerp(pts[i], len < 1e-4f ? 0f : Mathf.Clamp((offset - walked) / len, 0f, 1f));
            walked += len;
        }
        return pts[^1];
    }

    public static Vector2 TangentAt(Vector2[] pts, float offset)
    {
        float walked = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            float len = pts[i - 1].DistanceTo(pts[i]);
            if (walked + len >= offset || i == pts.Length - 1) return len < 1e-4f ? Vector2.Right : (pts[i] - pts[i - 1]) / len;
            walked += len;
        }
        return Vector2.Right;
    }

    public static Vector2[] Sub(Vector2[] pts, float begin, float end)
    {
        List<Vector2> res = [PointAt(pts, begin)];
        float walked = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            walked += pts[i - 1].DistanceTo(pts[i]);
            if (walked > begin + 0.01f && walked < end - 0.01f) res.Add(pts[i]);
        }
        res.Add(PointAt(pts, end));
        return [.. res];
    }

    public static Curve2D ToCurve(Vector2[] pts)
    {
        Curve2D c = new();
        foreach (Vector2 p in pts) c.AddPoint(p);
        return c;
    }

    public static Vector2[] Offset(Vector2[] src, float dist)
    {
        List<Vector2> p = [];
        foreach (Vector2 v in src) if (p.Count == 0 || p[^1].DistanceTo(v) > 1e-3f) p.Add(v);
        if (p.Count < 2) return [.. p];

        Vector2[] res = new Vector2[p.Count];
        for (int i = 0; i < p.Count; i++)
        {
            Vector2 before = (i > 0 ? p[i] - p[i - 1] : p[1] - p[0]).Normalized();
            Vector2 after = i < p.Count - 1 ? (p[i + 1] - p[i]).Normalized() : before;
            Vector2 mid = Left(before) + Left(after);
            mid = mid.Length() < 1e-4f ? Left(before) : mid.Normalized();
            res[i] = p[i] + mid * dist / Mathf.Max(mid.Dot(Left(before)), 0.35f);
        }
        return res;
    }

    public static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t, out Vector2 point)
    {
        t = 0f;
        point = Vector2.Zero;
        Vector2 r = b - a, s = d - c;
        float den = r.Cross(s);
        if (Mathf.Abs(den) < 1e-6f) return false;

        t = (c - a).Cross(s) / den;
        float u = (c - a).Cross(r) / den;
        if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
        point = a + r * t;
        return true;
    }
}
