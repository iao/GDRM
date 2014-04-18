using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GD
{
    class VisLink
    {
        public GraphNode NodeA { get; private set; }
        public GraphNode NodeB { get; private set; }

        public VisLink(GraphNode node_a, GraphNode node_b)
        {
            this.NodeA = node_a;
            this.NodeB = node_b;
        }

        public override bool Equals(System.Object obj)
        {
            if(obj == null) return false;
            if(obj is VisLink){
                VisLink link = (VisLink) obj;
                if (((link.NodeA.Equals(this.NodeA)) && (link.NodeB.Equals(this.NodeB))) || ((link.NodeA.Equals(this.NodeB)) && (link.NodeB.Equals(this.NodeA)))){
                    return true;
                }
            }
            return false;
        }

        public override int GetHashCode()
        {
            return NodeA.GetHashCode() ^ NodeB.GetHashCode();
        }
    }
}
