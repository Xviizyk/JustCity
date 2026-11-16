using System.Collections.Generic;
using System.Linq;
using Godot;

namespace JustCity.Script.Level.Building;

public readonly record struct Arm(RoadSegment Seg, bool AtEnd, int Id, Vector2 Out)
{
	public int Lanes => Seg.LanesPerDirection;
	public float Width => Seg.Width;
}

public readonly record struct Link(int FromArm, int FromLane, int ToArm, int ToLane);

public sealed record LinkChange(RoadNode Node, int[] Before, bool BeforeCustom, int[] After, bool AfterCustom);

public partial class RoadNetwork
{
	private const float StraightDeg = 40f;

	public void EnsureIds()
	{
		foreach (RoadSegment s in Segments)
		{
			if (s.StartArm == 0) s.StartArm = NextId++;
			if (s.EndArm == 0) s.EndArm = NextId++;
		}
	}

	public List<Arm> ArmsOf(RoadNode n)
	{
		var res = new List<Arm>();
		foreach (RoadSegment s in Segments)
		{
			if (s.StartNodeId == n.Id) res.Add(new Arm(s, false, s.StartArm, s.StartTangent));
			if (s.EndNodeId == n.Id) res.Add(new Arm(s, true, s.EndArm, -s.EndTangent));
		}
		return res;
	}

	public List<Link> LinksOf(RoadNode n)
	{
		var res = new List<Link>();
		int[] d = n.LinkData ?? new int[0];
		for (int i = 0; i + 3 < d.Length; i += 4) res.Add(new Link(d[i], d[i + 1], d[i + 2], d[i + 3]));
		return res;
	}

	private static int[] Encode(List<Link> links)
	{
		List<Link> sorted = links.OrderBy(l => l.FromArm).ThenBy(l => l.FromLane).ThenBy(l => l.ToArm).ThenBy(l => l.ToLane).ToList();
		var a = new int[sorted.Count * 4];
		for (int i = 0; i < sorted.Count; i++)
		{
			a[i * 4] = sorted[i].FromArm;
			a[i * 4 + 1] = sorted[i].FromLane;
			a[i * 4 + 2] = sorted[i].ToArm;
			a[i * 4 + 3] = sorted[i].ToLane;
		}
		return a;
	}

	public RoadOp TryToggleLink(RoadNode n, Link link, out string reason)
	{
		reason = null;
		List<Arm> arms = ArmsOf(n);
		List<Link> links = LinksOf(n);

		if (!links.Contains(link))
		{
			if (link.FromArm == link.ToArm && arms.Count > 1) { reason = "ROAD_LINK_UTURN"; return null; }
			links.Add(link);
		}
		else
		{
			links.Remove(link);
			reason = ValidateLinks(arms, links);
			if (reason != null) return null;
		}
		return CommitLinks(n, links, true);
	}

	public RoadOp ResetLinks(RoadNode n)
	{
		if (!n.CustomLinks) return null;
		return CommitLinks(n, DefaultLinks(ArmsOf(n)), false);
	}

	private RoadOp CommitLinks(RoadNode n, List<Link> links, bool custom)
	{
		var op = new RoadOp();
		int[] after = Encode(links);
		op.Links.Add(new LinkChange(n, (int[])(n.LinkData ?? new int[0]).Clone(), n.CustomLinks, after, custom));
		n.LinkData = (int[])after.Clone();
		n.CustomLinks = custom;
		TopologyChanged?.Invoke();
		return op;
	}

	public static string ValidateLinks(List<Arm> arms, List<Link> links)
	{
		foreach (Arm a in arms)
			for (int i = 0; i < a.Lanes; i++)
				if (!links.Any(l => l.FromArm == a.Id && l.FromLane == i)) return "ROAD_LINK_NO_EXIT";

		foreach (Arm b in arms)
			for (int j = 0; j < b.Lanes; j++)
				if (!links.Any(l => l.ToArm == b.Id && l.ToLane == j)) return "ROAD_LINK_NO_ENTRY";

		if (arms.Count > 1 && FindUnreachable(arms, links, out _, out _)) return "ROAD_LINK_DISCONNECTED";
		return null;
	}

	private static bool FindUnreachable(List<Arm> arms, List<Link> links, out Arm src, out Arm dst)
	{
		src = default;
		dst = default;
		int n = arms.Count;
		var idx = new System.Collections.Generic.Dictionary<int, int>();
		for (int i = 0; i < n; i++) idx[arms[i].Id] = i;

		var reach = new bool[n, n];
		foreach (Link l in links)
			if (l.FromArm != l.ToArm && idx.TryGetValue(l.FromArm, out int f) && idx.TryGetValue(l.ToArm, out int t))
				reach[f, t] = true;

		for (int k = 0; k < n; k++)
			for (int i = 0; i < n; i++)
				for (int j = 0; j < n; j++)
					if (reach[i, k] && reach[k, j]) reach[i, j] = true;

		for (int i = 0; i < n; i++)
			for (int j = 0; j < n; j++)
				if (i != j && !reach[i, j]) { src = arms[i]; dst = arms[j]; return true; }
		return false;
	}

	private static float TurnAngle(Arm a, Arm b) => (-a.Out).AngleTo(b.Out);

	private static void AddUnique(List<Link> l, Link x)
	{
		if (!l.Contains(x)) l.Add(x);
	}

	private static List<Link> DefaultLinks(List<Arm> arms)
	{
		var links = new List<Link>();
		if (arms.Count == 0) return links;
		if (arms.Count == 1)
		{
			Arm only = arms[0];
			for (int k = 0; k < only.Lanes; k++) links.Add(new Link(only.Id, k, only.Id, k));
			return links;
		}

		float straightRad = Mathf.DegToRad(StraightDeg);
		foreach (Arm a in arms)
		{
			int straightId = -1;
			float best = straightRad;
			foreach (Arm b in arms)
			{
				if (b.Id == a.Id) continue;
				float t = Mathf.Abs(TurnAngle(a, b));
				if (t < best) { best = t; straightId = b.Id; }
			}

			int n = a.Lanes;
			for (int i = 0; i < n; i++)
			{
				foreach (Arm b in arms)
				{
					if (b.Id == a.Id) continue;
					float th = TurnAngle(a, b);
					bool straight = b.Id == straightId;
					bool right = th > 0f;
					bool take;
					if (n == 1 || straight) take = true;
					else if (right) take = i == n - 1 || (straightId < 0 && i * 2 >= n);
					else take = i == 0 || (straightId < 0 && i * 2 < n);
					if (!take) continue;

					int j = straight ? Mathf.Clamp(i, 0, b.Lanes - 1) : right ? b.Lanes - 1 : 0;
					AddUnique(links, new Link(a.Id, i, b.Id, j));
				}
			}
		}
		FixCoverage(arms, links);
		return links;
	}

	private static Arm ClosestTarget(List<Arm> arms, Arm a)
	{
		Arm best = default;
		float bd = float.MaxValue;
		foreach (Arm b in arms)
		{
			if (b.Id == a.Id) continue;
			float d = Mathf.Abs(TurnAngle(a, b));
			if (d < bd) { bd = d; best = b; }
		}
		return best;
	}

	private static Arm ClosestSource(List<Arm> arms, Arm b)
	{
		Arm best = default;
		float bd = float.MaxValue;
		foreach (Arm a in arms)
		{
			if (a.Id == b.Id) continue;
			float d = Mathf.Abs(TurnAngle(a, b));
			if (d < bd) { bd = d; best = a; }
		}
		return best;
	}

	private static Link LinkFrom(Arm a, int i, Arm b)
	{
		float th = TurnAngle(a, b);
		int j = Mathf.Abs(th) < Mathf.DegToRad(StraightDeg) ? Mathf.Clamp(i, 0, b.Lanes - 1) : th > 0f ? b.Lanes - 1 : 0;
		return new Link(a.Id, i, b.Id, j);
	}

	private static Link LinkTo(Arm a, Arm b, int j)
	{
		float th = TurnAngle(a, b);
		int i = Mathf.Abs(th) < Mathf.DegToRad(StraightDeg) ? Mathf.Clamp(j, 0, a.Lanes - 1) : th > 0f ? a.Lanes - 1 : 0;
		return new Link(a.Id, i, b.Id, j);
	}

	private static Link TurnLink(Arm a, Arm b)
	{
		float th = TurnAngle(a, b);
		if (Mathf.Abs(th) < Mathf.DegToRad(StraightDeg))
		{
			int i = a.Lanes / 2;
			return new Link(a.Id, i, b.Id, Mathf.Clamp(i, 0, b.Lanes - 1));
		}
		return th > 0f ? new Link(a.Id, a.Lanes - 1, b.Id, b.Lanes - 1) : new Link(a.Id, 0, b.Id, 0);
	}

	private static void FixCoverage(List<Arm> arms, List<Link> links)
	{
		if (arms.Count == 0) return;
		if (arms.Count == 1)
		{
			Arm only = arms[0];
			links.Clear();
			for (int k = 0; k < only.Lanes; k++) links.Add(new Link(only.Id, k, only.Id, k));
			return;
		}

		foreach (Arm a in arms)
		{
			for (int i = 0; i < a.Lanes; i++)
			{
				if (links.Any(l => l.FromArm == a.Id && l.FromLane == i)) continue;
				AddUnique(links, LinkFrom(a, i, ClosestTarget(arms, a)));
			}
		}
		foreach (Arm b in arms)
		{
			for (int j = 0; j < b.Lanes; j++)
			{
				if (links.Any(l => l.ToArm == b.Id && l.ToLane == j)) continue;
				AddUnique(links, LinkTo(ClosestSource(arms, b), b, j));
			}
		}
		for (int guard = 0; guard < arms.Count * arms.Count + 4; guard++)
		{
			if (!FindUnreachable(arms, links, out Arm src, out Arm dst)) break;
			AddUnique(links, TurnLink(src, dst));
		}
	}

	public void SanitizeLinks()
	{
		var map = new System.Collections.Generic.Dictionary<int, List<Arm>>();
		foreach (RoadSegment s in Segments)
		{
			if (!map.TryGetValue(s.StartNodeId, out List<Arm> ls)) map[s.StartNodeId] = ls = new List<Arm>();
			ls.Add(new Arm(s, false, s.StartArm, s.StartTangent));
			if (!map.TryGetValue(s.EndNodeId, out List<Arm> le)) map[s.EndNodeId] = le = new List<Arm>();
			le.Add(new Arm(s, true, s.EndArm, -s.EndTangent));
		}
		foreach (RoadNode n in Nodes)
			SanitizeNode(n, map.TryGetValue(n.Id, out List<Arm> arms) ? arms : new List<Arm>());
	}

	private void SanitizeNode(RoadNode n, List<Arm> arms)
	{
		List<Link> links;
		if (!n.CustomLinks || arms.Count < 2)
		{
			n.CustomLinks = false;
			links = DefaultLinks(arms);
		}
		else
		{
			var lanes = new System.Collections.Generic.Dictionary<int, int>();
			foreach (Arm a in arms) lanes[a.Id] = a.Lanes;

			links = new List<Link>();
			foreach (Link l in LinksOf(n))
			{
				if (!lanes.TryGetValue(l.FromArm, out int fl) || !lanes.TryGetValue(l.ToArm, out int tl)) continue;
				if (l.FromLane < 0 || l.FromLane >= fl || l.ToLane < 0 || l.ToLane >= tl) continue;
				if (l.FromArm == l.ToArm) continue;
				AddUnique(links, l);
			}
			FixCoverage(arms, links);
		}

		int[] data = Encode(links);
		if (!(n.LinkData ?? new int[0]).SequenceEqual(data)) n.LinkData = data;
	}
}
