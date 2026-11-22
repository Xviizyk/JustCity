using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Geometry;

namespace JustCity.Script.Level.Building.Road.Graph;

public class ClearPlan
{
    public List<Item> Items = [];
    public List<Vector2> Cuts = [];
    public float Cost;
    public float Length;

    public class Item
    {
        public RoadEdge Edge;
        public Vector2[] Pts;
        public List<float> Offsets = [];
        public List<bool> Inside = [];
    }

    public static ClearPlan Make(RoadGraph graph, Rect2 rect, int level, float costPerPixel, float bridgeMultiplier)
    {
        ClearPlan plan = new();
        foreach (RoadEdge edge in graph.Segments)
        {
            if (edge.Level != level || !rect.Intersects(graph.BoundsOf(edge))) continue;

            Vector2[] pts = graph.Poly(edge);
            float total = RoadGeometry.Length(pts);
            List<float> breaks = [0f, total];
            float walked = 0f;
            for (int i = 1; i < pts.Length; i++)
            {
                float len = pts[i - 1].DistanceTo(pts[i]);
                if (len > 1e-4f && Clip(pts[i - 1], pts[i], rect, out float t0, out float t1))
                {
                    if (t0 > 1e-4f) breaks.Add(walked + len * t0);
                    if (t1 < 1f - 1e-4f) breaks.Add(walked + len * t1);
                }
                walked += len;
            }
            breaks.Sort();

            Item item = new() { Edge = edge, Pts = pts };
            float inside = 0f;
            item.Offsets.Add(0f);
            for (int k = 0; k < breaks.Count - 1; k++)
            {
                if (breaks[k + 1] - breaks[k] < 0.05f) continue;
                Vector2 mid = RoadGeometry.PointAt(pts, (breaks[k] + breaks[k + 1]) * 0.5f);
                bool isInside = mid.X > rect.Position.X && mid.X < rect.End.X && mid.Y > rect.Position.Y && mid.Y < rect.End.Y;
                if (item.Inside.Count > 0 && item.Inside[^1] == isInside) item.Offsets[^1] = breaks[k + 1];
                else
                {
                    item.Inside.Add(isInside);
                    item.Offsets.Add(breaks[k + 1]);
                }
            }
            if (!item.Inside.Contains(true)) continue;
            item.Offsets[^1] = total;

            for (int i = 0; i < item.Inside.Count; i++)
            {
                if (!item.Inside[i]) continue;
                inside += item.Offsets[i + 1] - item.Offsets[i];
                if (item.Offsets[i] > 0.5f) plan.Cuts.Add(RoadGeometry.PointAt(pts, item.Offsets[i]));
                if (item.Offsets[i + 1] < total - 0.5f) plan.Cuts.Add(RoadGeometry.PointAt(pts, item.Offsets[i + 1]));
            }
            plan.Items.Add(item);
            plan.Cost += inside * costPerPixel * edge.LanesPerDirection * (edge.Level > 0 ? bridgeMultiplier : 1f);
            plan.Length += inside;
        }
        return plan;
    }

    private static bool Clip(Vector2 a, Vector2 b, Rect2 rect, out float t0, out float t1)
    {
        t0 = 0f;
        t1 = 1f;
        Vector2 d = b - a;
        return ClipSide(-d.X, a.X - rect.Position.X, ref t0, ref t1) && ClipSide(d.X, rect.End.X - a.X, ref t0, ref t1) && ClipSide(-d.Y, a.Y - rect.Position.Y, ref t0, ref t1) && ClipSide(d.Y, rect.End.Y - a.Y, ref t0, ref t1);
    }

    private static bool ClipSide(float p, float q, ref float t0, ref float t1)
    {
        if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
        float t = q / p;
        if (p < 0f)
        {
            if (t > t1) return false;
            if (t > t0) t0 = t;
        }
        else
        {
            if (t < t0) return false;
            if (t < t1) t1 = t;
        }
        return true;
    }
}
