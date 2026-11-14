using Godot;

namespace JustCity.Script.Level.Building;

public readonly record struct ArcInfo(Curve2D Curve, float Radius, float Sweep);

public static class RoadGeometry
{
    public static ArcInfo Arc(Vector2 p0, Vector2 dir, Vector2 p1)
    {
        dir = dir.Normalized();
        var curve = new Curve2D();
        Vector2 d = p1 - p0;
        Vector2 n = new(-dir.Y, dir.X);
        float dn = d.Dot(n);

        if (Mathf.Abs(dn) < 0.01f)
        {
            curve.AddPoint(p0);
            curve.AddPoint(p1);
            return new ArcInfo(curve, float.PositiveInfinity, 0f);
        }

        float r = d.LengthSquared() / (2f * dn);
        float radius = Mathf.Abs(r);
        Vector2 c = p0 + n * r;

        float a0 = (p0 - c).Angle();
        float a1 = (p1 - c).Angle();
        float sweep = a1 - a0;
        if (r > 0f) { while (sweep < 0f) sweep += Mathf.Tau; }
        else { while (sweep > 0f) sweep -= Mathf.Tau; }

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
        return new ArcInfo(curve, radius, sweep);
    }

    public static Vector2 EndTangent(Curve2D c)
    {
        float len = c.GetBakedLength();
        if (len < 1f) return Vector2.Right;
        return (c.SampleBaked(len) - c.SampleBaked(len - 1f)).Normalized();
    }
}
