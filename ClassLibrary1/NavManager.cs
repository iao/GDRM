using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GD
{
    class NavManager
    {
        private sealed class NavManipulationException : Exception { public NavManipulationException(string message) : base(message){} }

        private static NavManager _instance = new NavManager();
        public static NavManager Instance { get { return _instance; } }

        private List<GraphNode> nodes = new List<GraphNode>();
        public List<GraphNode> Nodes { get { return nodes; } }

        private Dictionary<string, List<GraphNode>> nodes_by_name = new Dictionary<string, List<GraphNode>>();

        public void Clear()
        {
            lock (this.nodes)
            {
                this.nodes.Clear();
                this.nodes_by_name.Clear();
            }
        }

        public GraphNode GetNearestNode(Location location)
        {
            if (location == null) return null;
            GraphNode closest_node = null;
            float closest_distance = float.PositiveInfinity;

            foreach (GraphNode node in this.nodes)
            {
                if (node.Location.Scene == location.Scene)
                {
                    float distance_between = Location.DistanceBetween(node.Location, location);
                    if (distance_between < 1.0) return node;
                    if (distance_between < closest_distance)
                    {
                        closest_node = node;
                        closest_distance = distance_between;
                    }
                }
            }
            return closest_node;
        }

        public GraphNode GetNodeByName(string name)
        {
            lock (this.nodes)
            {
                List<GraphNode> nodes_with_name;
                if (this.nodes_by_name.TryGetValue(name, out nodes_with_name))
                {
                    if (nodes_with_name.Count > 0)
                    {
                        GraphNode node = nodes_with_name.ElementAt(0);
                        nodes_with_name.RemoveAt(0);
                        nodes_with_name.Add(node);
                        return node;
                    }
                }
            }
            return null;
        }

        public void AddNode(GraphNode new_node)
        {
            lock (nodes)
            {
                if (!nodes.Contains(new_node))
                {
                    nodes.Add(new_node);
                    List<GraphNode> nodes_with_same_name;
                    if (this.nodes_by_name.TryGetValue(new_node.Name, out nodes_with_same_name))
                    {
                        nodes_with_same_name.Add(new_node);
                    }
                    else
                    {
                        nodes_with_same_name = new List<GraphNode>();
                        nodes_with_same_name.Add(new_node);
                        nodes_by_name.Add(new_node.Name, nodes_with_same_name);
                    }
                }
                else
                {
                    throw new NavManipulationException("New node is already in graph");
                }
            }
        }

        public bool AddLink(GraphNode node_a, GraphNode node_b)
        {
            if (node_a == null || node_b == null)
            {
                throw new NavManipulationException("A node to join is null");
            }
            lock (nodes)
            {
                if (!nodes.Contains(node_a) || !nodes.Contains(node_b))
                {
                    throw new NavManipulationException("A node to join is not in graph");
                }
                else
                {
                    return node_a.Neighbours.Add(node_b) && node_b.Neighbours.Add(node_a);
                }
            }
        }

        public void RemoveNode(GraphNode old_node)
        {
            if (old_node == null) throw new NavManipulationException("Node to delete is null");
            lock (nodes)
            {
                if (nodes.Contains(old_node))
                {
                    foreach (GraphNode neighbour in old_node.Neighbours.ToList())
                    {
                        this.RemoveLink(old_node, neighbour);
                    }
                    nodes.Remove(old_node);
                    List<GraphNode> nodes_with_name;
                    if (this.nodes_by_name.TryGetValue(old_node.Name, out nodes_with_name))
                    {
                        nodes_with_name.Remove(old_node);
                    }
                }
                else
                {
                    throw new NavManipulationException("Node to delete isn't in graph");
                }
            }
        }

        public bool RemoveLink(GraphNode node_a, GraphNode node_b)
        {
            if (node_a == null || node_b == null) throw new NavManipulationException("Node cannot be found");
            lock (nodes)
            {
                return node_a.Neighbours.Remove(node_b) && node_b.Neighbours.Remove(node_a);
            }
        }

        public List<GraphNode> GetRoute(GraphNode start_node, GraphNode end_node)
        {
            Dictionary<GraphNode, float> distances = new Dictionary<GraphNode, float>();
            Dictionary<GraphNode, GraphNode> previous = new Dictionary<GraphNode, GraphNode>();
            foreach (GraphNode node in this.nodes)
            {
                distances.Add(node, float.PositiveInfinity);
            }
            distances[start_node] = 0;

            List<GraphNode> nodes_to_traverse = this.nodes.ToList();

            while (nodes_to_traverse.Count > 0)
            {
                GraphNode current_node = null;
                float shortest_distance = float.PositiveInfinity;
            
                foreach (GraphNode node in nodes_to_traverse)
                {
                    float distance;
                    if (distances.TryGetValue(node, out distance) && distance <= shortest_distance)
                    {
                        current_node = node;
                        shortest_distance = distance;
                    }
                }

                nodes_to_traverse.Remove(current_node);
                if (float.IsPositiveInfinity(shortest_distance)) return null;

                if (current_node == end_node)
                {
                    return this.ReconstructPath(previous, current_node);
                }

                foreach (GraphNode neighbour in current_node.Neighbours)
                {
                    float new_distance = shortest_distance + Location.DistanceBetween(neighbour.Location, current_node.Location);
                    float distance_to_neighbour;
                    if (distances.TryGetValue(neighbour, out distance_to_neighbour) && new_distance < distance_to_neighbour)
                    {
                        distances[neighbour] = new_distance;
                        previous[neighbour] = current_node;
                    }
                }
            }
            return null;
        }


        //old code. ignore
        public List<GraphNode> GetRouteOld(GraphNode start_node, GraphNode end_node)
        {
            HashSet<GraphNode> evaluated_nodes = new HashSet<GraphNode>();
            HashSet<GraphNode> nodes_to_evaluate = new HashSet<GraphNode>();
            Dictionary<GraphNode, GraphNode> route = new Dictionary<GraphNode, GraphNode>();
            Dictionary<GraphNode, float> best_known_dist = new Dictionary<GraphNode, float>();
            Dictionary<GraphNode, float> estimated_dist_to_end = new Dictionary<GraphNode, float>();
            nodes_to_evaluate.Add(start_node);

            best_known_dist[start_node] = 0.0f;
            estimated_dist_to_end[start_node] = Location.DistanceBetween(start_node.Location, end_node.Location);

            while (nodes_to_evaluate.Count > 0)
            {
                //finds the next node to evaluate based on the node closest to the target.
                GraphNode current_node = nodes_to_evaluate.ElementAt(0);
                foreach (GraphNode node in nodes_to_evaluate)
                {
                    if (Location.DistanceBetween(node.Location, end_node.Location) < Location.DistanceBetween(current_node.Location, end_node.Location))
                    {
                        current_node = node;
                    }
                }

                //If the end has been reached, unravel the route and return it.
                if (current_node == end_node)
                {
                    return ReconstructPath(route, end_node);
                }

                //remove the next node to evalaute from the 'to evaluate' set.
                nodes_to_evaluate.Remove(current_node);

                //add the next node to the 'evaluated' list.
                evaluated_nodes.Add(current_node);

                //for each of the nieghbours of the currently visitted node.
                foreach (GraphNode neighbour in current_node.Neighbours)
                {

                    //if this neighbour has already been evaluated, skip it. Otherwise:
                    if (evaluated_nodes.Contains(neighbour) == false)
                    {
                        //hopes for the best distance
                        float temp_best_known_dist = best_known_dist[current_node] + Location.DistanceBetween(neighbour.Location, end_node.Location);


                        if (nodes_to_evaluate.Contains(neighbour) == false || temp_best_known_dist < best_known_dist[neighbour])
                        {
                            route[neighbour] = current_node;
                            best_known_dist[neighbour] = temp_best_known_dist;
                            estimated_dist_to_end[neighbour] = temp_best_known_dist + Location.DistanceBetween(neighbour.Location, end_node.Location);
                            if (nodes_to_evaluate.Contains(neighbour) == false)
                            {
                                nodes_to_evaluate.Add(neighbour);
                            }
                        }

                    }
                }

            }
            return null;
        }

        private List<GraphNode> ReconstructPath(Dictionary<GraphNode, GraphNode> route_map, GraphNode current_node)
        {
            if (route_map.ContainsKey(current_node))
            {
                List<GraphNode> route = ReconstructPath(route_map, route_map[current_node]);
                route.Add(current_node);
                return route;
            }
            else
            {
                return new List<GraphNode>() { current_node };
            }
        }
    }

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

        public GraphNode(Location location) : this(location, "") {}
    }
}
