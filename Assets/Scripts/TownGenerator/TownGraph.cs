using UnityEngine;
using System.Collections.Generic;

// Represents a node (vertex) in our road graph
public class GraphNode
{
    public Vector3 position;
    public List<GraphEdge> edges = new List<GraphEdge>(); // Edges connected to this node

    public GraphNode(Vector3 pos)
    {
        position = pos;
    }

    // For debugging
    public override string ToString()
    {
        return $"Node({position.x:F1}, {position.z:F1})";
    }
}

// Represents an edge (segment) in our road graph
public class GraphEdge
{
    public GraphNode startNode;
    public GraphNode endNode;
    public float length;

    public GraphEdge(GraphNode start, GraphNode end)
    {
        startNode = start;
        endNode = end;
        length = Vector3.Distance(start.position, end.position);
    }

    // For debugging
    public override string ToString()
    {
        return $"Edge({startNode.position.x:F1}, {startNode.position.z:F1} to {endNode.position.x:F1}, {endNode.position.z:F1})";
    }
}


