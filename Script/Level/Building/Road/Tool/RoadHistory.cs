using System.Collections.Generic;
using Godot;
using JustCity.Script.Level.Building.Road.Graph;

namespace JustCity.Script.Level.Building.Road.Tool;

public class RoadHistory
{
    public class Entry
    {
        public RoadEdit Edit;
        public float Cost;
        public RoadSnap Start;
        public Vector2 Dir;
        public List<Vector2> Candidates;
    }

    private readonly List<Entry> done = [];
    private readonly List<Entry> undone = [];

    public void Add(RoadEdit edit, float cost, RoadSnap start, Vector2 dir, List<Vector2> candidates)
    {
        done.Add(new Entry { Edit = edit, Cost = cost, Start = start, Dir = dir, Candidates = candidates });
        undone.Clear();
    }

    public Entry Undo()
    {
        if (done.Count == 0) return null;
        Entry e = done[^1];
        done.RemoveAt(done.Count - 1);
        undone.Add(e);
        return e;
    }

    public Entry NextRedo()
    {
        return undone.Count == 0 ? null : undone[^1];
    }

    public Entry Redo()
    {
        if (undone.Count == 0) return null;
        Entry e = undone[^1];
        undone.RemoveAt(undone.Count - 1);
        done.Add(e);
        return e;
    }
}
