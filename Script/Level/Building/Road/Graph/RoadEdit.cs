using System.Collections.Generic;

namespace JustCity.Script.Level.Building.Road.Graph;

public class RoadEdit
{
    public List<RoadJunction> Nodes = [];
    public List<RoadJunction> RemovedNodes = [];
    public List<RoadEdge> Added = [];
    public List<RoadEdge> Removed = [];
    public List<RoadEdge> Built = [];
    public List<LinkChange> Links = [];

    public class LinkChange
    {
        public RoadJunction Node;
        public int[] Before;
        public int[] After;
        public bool BeforeCustom;
        public bool AfterCustom;
    }
}
