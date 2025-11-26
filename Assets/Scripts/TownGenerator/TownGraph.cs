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

// Custom comparer for Vector3 keys in Dictionary, to handle floating point inaccuracies
public class Vector3EqualityComparer : IEqualityComparer<Vector3>
{
    private const float Tolerance = 0.01f; // Adjust as needed

    public bool Equals(Vector3 v1, Vector3 v2)
    {
        return Mathf.Abs(v1.x - v2.x) < Tolerance &&
               Mathf.Abs(v1.y - v2.y) < Tolerance &&
               Mathf.Abs(v1.z - v2.z) < Tolerance;
    }

    public int GetHashCode(Vector3 obj)
    {
        // Simple hash code, might have collisions but good enough for small tolerances
        return (Mathf.RoundToInt(obj.x / Tolerance) * 31 +
                Mathf.RoundToInt(obj.y / Tolerance) * 17 +
                Mathf.RoundToInt(obj.z / Tolerance) * 13);
    }
}
