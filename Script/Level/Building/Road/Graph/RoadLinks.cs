using System.Collections.Generic;
using Godot;

namespace JustCity.Script.Level.Building.Road.Graph;

public class RoadLinks(RoadGraph graph)
{
    private const float StraightDeg = 40f;
    private readonly RoadGraph graph = graph;

    public void EnsureIds()
    {
        foreach (RoadEdge e in graph.Segments)
        {
            if (e.StartArm == 0) e.StartArm = graph.NextId++;
            if (e.EndArm == 0) e.EndArm = graph.NextId++;
        }
    }

    public List<Arm> ArmsOf(RoadJunction n)
    {
        List<Arm> arms = [];
        foreach (RoadEdge e in graph.Segments)
        {
            if (e.StartNodeId == n.Id) arms.Add(new Arm(e, false));
            if (e.EndNodeId == n.Id) arms.Add(new Arm(e, true));
        }
        return arms;
    }

    public static List<Link> LinksOf(RoadJunction n)
    {
        List<Link> links = [];
        int[] d = n.LinkData;
        for (int i = 0; d != null && i + 3 < d.Length; i += 4) links.Add(new Link(d[i], d[i + 1], d[i + 2], d[i + 3]));
        return links;
    }

    public RoadEdit TryToggle(RoadJunction n, Link link, out string reason)
    {
        reason = null;
        List<Arm> arms = ArmsOf(n);
        List<Link> links = LinksOf(n);
        if (!links.Contains(link))
        {
            if (link.FromArm == link.ToArm && arms.Count > 1)
            {
                reason = "ROAD_LINK_UTURN";
                return null;
            }
            links.Add(link);
        }
        else
        {
            links.Remove(link);
            reason = Validate(arms, links);
            if (reason != null) return null;
        }
        return Commit(n, links, true);
    }

    public RoadEdit Reset(RoadJunction n)
    {
        return n.CustomLinks ? Commit(n, DefaultLinks(ArmsOf(n)), false) : null;
    }

    private RoadEdit Commit(RoadJunction n, List<Link> links, bool custom)
    {
        RoadEdit edit = new();
        int[] after = Encode(links);
        edit.Links.Add(new RoadEdit.LinkChange { Node = n, Before = n.LinkData == null ? [] : (int[])n.LinkData.Clone(), BeforeCustom = n.CustomLinks, After = after, AfterCustom = custom });
        n.LinkData = (int[])after.Clone();
        n.CustomLinks = custom;
        graph.RoadChanged();
        return edit;
    }

    public static string Validate(List<Arm> arms, List<Link> links)
    {
        foreach (Arm a in arms) for (int l = 0; l < a.In; l++) if (CanReachOther(arms, a, true) && !Has(links, a.Id, l, true)) return "ROAD_LINK_NO_EXIT";
        foreach (Arm a in arms) for (int l = 0; l < a.Exit; l++) if (CanReachOther(arms, a, false) && !Has(links, a.Id, l, false)) return "ROAD_LINK_NO_ENTRY";
        return FindUnreachable(arms, links, out _, out _) ? "ROAD_LINK_DISCONNECTED" : null;
    }

    private static bool Has(List<Link> links, int armId, int lane, bool leaving)
    {
        foreach (Link k in links) if (leaving ? k.FromArm == armId && k.FromLane == lane : k.ToArm == armId && k.ToLane == lane) return true;
        return false;
    }

    private static bool CanReachOther(List<Arm> arms, Arm a, bool leaving)
    {
        if (arms.Count == 1) return leaving ? a.Exit > 0 : a.In > 0;
        foreach (Arm o in arms) if (o.Id != a.Id && (leaving ? o.Exit > 0 : o.In > 0)) return true;
        return false;
    }

    private static bool FindUnreachable(List<Arm> arms, List<Link> links, out Arm source, out Arm target)
    {
        source = null;
        target = null;
        int n = arms.Count;
        Dictionary<int, int> index = new();
        for (int i = 0; i < n; i++) index[arms[i].Id] = i;

        bool[,] reach = new bool[n, n];
        foreach (Link k in links) if (k.FromArm != k.ToArm && index.TryGetValue(k.FromArm, out int f) && index.TryGetValue(k.ToArm, out int t)) reach[f, t] = true;
        for (int k = 0; k < n; k++) for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) if (reach[i, k] && reach[k, j]) reach[i, j] = true;

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j || reach[i, j] || arms[i].In == 0 || arms[j].Exit == 0) continue;
                source = arms[i];
                target = arms[j];
                return true;
            }
        }
        return false;
    }

    private static int Compare(Link a, Link b)
    {
        if (a.FromArm != b.FromArm) return a.FromArm.CompareTo(b.FromArm);
        if (a.FromLane != b.FromLane) return a.FromLane.CompareTo(b.FromLane);
        if (a.ToArm != b.ToArm) return a.ToArm.CompareTo(b.ToArm);
        return a.ToLane.CompareTo(b.ToLane);
    }

    private static int[] Encode(List<Link> links)
    {
        List<Link> sorted = [.. links];
        sorted.Sort(Compare);
        int[] data = new int[sorted.Count * 4];
        for (int i = 0; i < sorted.Count; i++)
        {
            data[i * 4] = sorted[i].FromArm;
            data[i * 4 + 1] = sorted[i].FromLane;
            data[i * 4 + 2] = sorted[i].ToArm;
            data[i * 4 + 3] = sorted[i].ToLane;
        }
        return data;
    }

    private static Arm ArmById(List<Arm> arms, int id)
    {
        foreach (Arm a in arms) if (a.Id == id) return a;
        return null;
    }

    private static bool Same(int[] a, int[] b)
    {
        b ??= [];
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static void AddUnique(List<Link> links, Link link)
    {
        if (!links.Contains(link)) links.Add(link);
    }

    private static float Turn(Arm a, Arm b)
    {
        return (-a.Out).AngleTo(b.Out);
    }

    private static int MapLane(float turn, int lane, int count)
    {
        if (Mathf.Abs(turn) < Mathf.DegToRad(StraightDeg)) return Mathf.Clamp(lane, 0, count - 1);
        return turn > 0f ? count - 1 : 0;
    }

    private static List<Link> SelfLinks(Arm a)
    {
        List<Link> links = [];
        for (int l = 0; l < Mathf.Min(a.In, a.Exit); l++) links.Add(new Link(a.Id, l, a.Id, l));
        return links;
    }

    private static List<Link> DefaultLinks(List<Arm> arms)
    {
        if (arms.Count == 1) return SelfLinks(arms[0]);

        List<Link> links = [];
        if (arms.Count == 0) return links;
        foreach (Arm a in arms)
        {
            int straightId = -1;
            float best = Mathf.DegToRad(StraightDeg);
            foreach (Arm o in arms)
            {
                if (o.Id != a.Id && o.Exit > 0 && Mathf.Abs(Turn(a, o)) < best)
                {
                    best = Mathf.Abs(Turn(a, o));
                    straightId = o.Id;
                }
            }

            for (int lane = 0; lane < a.In; lane++)
            {
                foreach (Arm o in arms)
                {
                    if (o.Id == a.Id || o.Exit == 0) continue;
                    bool straight = o.Id == straightId, right = Turn(a, o) > 0f;
                    bool take = a.In == 1 || straight || (right ? lane == a.In - 1 || (straightId < 0 && lane * 2 >= a.In) : lane == 0 || (straightId < 0 && lane * 2 < a.In));
                    if (take) AddUnique(links, new Link(a.Id, lane, o.Id, straight ? Mathf.Clamp(lane, 0, o.Exit - 1) : right ? o.Exit - 1 : 0));
                }
            }
        }
        FixCoverage(arms, links);
        return links;
    }

    private static Arm Closest(List<Arm> arms, Arm a, bool leaving)
    {
        Arm best = null;
        float bestAngle = float.MaxValue;
        foreach (Arm o in arms)
        {
            if (o.Id == a.Id || (leaving ? o.Exit == 0 : o.In == 0)) continue;
            float angle = Mathf.Abs(leaving ? Turn(a, o) : Turn(o, a));
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = o;
            }
        }
        return best;
    }

    private static void FixCoverage(List<Arm> arms, List<Link> links)
    {
        if (arms.Count == 1)
        {
            links.Clear();
            links.AddRange(SelfLinks(arms[0]));
            return;
        }
        foreach (Arm a in arms)
        {
            for (int lane = 0; lane < a.In; lane++)
            {
                Arm o = Closest(arms, a, true);
                if (o != null && !Has(links, a.Id, lane, true)) AddUnique(links, new Link(a.Id, lane, o.Id, MapLane(Turn(a, o), lane, o.Exit)));
            }
        }
        foreach (Arm a in arms)
        {
            for (int lane = 0; lane < a.Exit; lane++)
            {
                Arm o = Closest(arms, a, false);
                if (o != null && !Has(links, a.Id, lane, false)) AddUnique(links, new Link(o.Id, MapLane(Turn(o, a), lane, o.In), a.Id, lane));
            }
        }

        for (int i = 0; i < arms.Count * arms.Count + 4 && FindUnreachable(arms, links, out Arm source, out Arm target); i++)
        {
            float turn = Turn(source, target);
            int lane = Mathf.Abs(turn) < Mathf.DegToRad(StraightDeg) ? source.In / 2 : turn > 0f ? source.In - 1 : 0;
            AddUnique(links, new Link(source.Id, lane, target.Id, MapLane(turn, lane, target.Exit)));
        }
    }

    public void Sanitize()
    {
        Dictionary<int, List<Arm>> byNode = new();
        foreach (RoadEdge e in graph.Segments)
        {
            if (!byNode.ContainsKey(e.StartNodeId)) byNode[e.StartNodeId] = [];
            if (!byNode.ContainsKey(e.EndNodeId)) byNode[e.EndNodeId] = [];
            byNode[e.StartNodeId].Add(new Arm(e, false));
            byNode[e.EndNodeId].Add(new Arm(e, true));
        }

        foreach (RoadJunction n in graph.Nodes)
        {
            List<Arm> arms = [];
            if (byNode.TryGetValue(n.Id, out List<Arm> value)) arms = value;
            List<Link> links;
            if (!n.CustomLinks || arms.Count < 2)
            {
                n.CustomLinks = false;
                links = DefaultLinks(arms);
            }
            else
            {
                links = [];
                foreach (Link k in LinksOf(n))
                {
                    Arm source = ArmById(arms, k.FromArm), target = ArmById(arms, k.ToArm);
                    if (source != null && target != null && source != target && k.FromLane >= 0 && k.FromLane < source.In && k.ToLane >= 0 && k.ToLane < target.Exit) AddUnique(links, k);
                }
                FixCoverage(arms, links);
            }

            int[] data = Encode(links);
            if (!Same(data, n.LinkData)) n.LinkData = data;
        }
    }
}
