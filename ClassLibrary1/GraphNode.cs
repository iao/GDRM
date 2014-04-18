using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GD
{
    class GraphNode
    {
        public HashSet<GraphNode> Neighbours { get; private set; }
        public Location Location { get; set; }
        public string Name { get; set; }

        public GraphNode(Location location, string name)
        {
            this.Neighbours = new HashSet<GraphNode>();
            this.Location = location;
            this.Name = name;
        }

        public GraphNode(Location location) : this(location, null){}
    }
}
