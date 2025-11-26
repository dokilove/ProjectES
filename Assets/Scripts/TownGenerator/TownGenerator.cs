using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TownGenerator : MonoBehaviour
{
    [Header("Road Settings")]
    public List<Road> roads = new List<Road>();
    public float roadWidth = 5f;
    public Material roadMaterial;

    [Header("Building Settings")]
    public GameObject[] buildingPrefabs;
    public float buildingOffset = 10f;
    public float buildingSpacing = 20f;
    public Vector2 buildingRandomOffset = new Vector2(0, 5f);
    public Vector2 buildingRandomSpacing = new Vector2(0, 10f);

    [Header("Graph Data (Debug)")]
    public List<GraphNode> graphNodes = new List<GraphNode>();
    public List<GraphEdge> graphEdges = new List<GraphEdge>();

    [Header("Debug")]
    public List<Vector3> intersectionPoints = new List<Vector3>();
    public List<List<Vector3>> cityBlocks = new List<List<Vector3>>();


    private const string BUILDING_CONTAINER_NAME = "[Generated Buildings]";

    #region Road Generation
    public void GenerateRoad()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        if (roads.Count == 0)
        {
            ClearRoads(); // Use the clear method if no roads exist
            return;
        }

        List<CombineInstance> combineInstances = new List<CombineInstance>();

        foreach (var road in roads)
        {
            if (road.nodes.Count < 2) continue;

            Mesh roadMesh = CreateRoadMesh(road);
            CombineInstance combineInstance = new CombineInstance
            {
                mesh = roadMesh,
                transform = Matrix4x4.identity // Vertices are already in local space
            };
            combineInstances.Add(combineInstance);
        }

        Mesh finalMesh = new Mesh();
        finalMesh.name = "CombinedRoadMesh";
        finalMesh.CombineMeshes(combineInstances.ToArray(), true, false); // Merge submeshes, don't use matrices

        meshFilter.mesh = finalMesh;
        meshCollider.sharedMesh = finalMesh;

        if (roadMaterial != null)
        {
            meshRenderer.material = roadMaterial;
        }
    }

    public void ClearRoads()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.mesh = null;
        }
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
        }
    }

    private Mesh CreateRoadMesh(Road road)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        float currentLength = 0f;

        for (int i = 0; i < road.nodes.Count - 1; i++)
        {
            // Transform world points to local points for the mesh
            Vector3 p1 = transform.InverseTransformPoint(road.nodes[i]);
            Vector3 p2 = transform.InverseTransformPoint(road.nodes[i + 1]);
            float segmentLength = Vector3.Distance(p1, p2);

            Vector3 forward = (p2 - p1).normalized;
            Vector3 side = Vector3.Cross(forward, Vector3.up).normalized;

            vertices.Add(p1 + side * roadWidth * 0.5f); // Left of current node
            vertices.Add(p1 - side * roadWidth * 0.5f); // Right of current node
            vertices.Add(p2 + side * roadWidth * 0.5f); // Left of next node
            vertices.Add(p2 - side * roadWidth * 0.5f); // Right of next node

            uvs.Add(new Vector2(0, currentLength));
            uvs.Add(new Vector2(1, currentLength));
            uvs.Add(new Vector2(0, currentLength + segmentLength));
            uvs.Add(new Vector2(1, currentLength + segmentLength));

            currentLength += segmentLength;

            int vertIndex = i * 4;
            triangles.Add(vertIndex + 0);
            triangles.Add(vertIndex + 2);
            triangles.Add(vertIndex + 1);

            triangles.Add(vertIndex + 1);
            triangles.Add(vertIndex + 2);
            triangles.Add(vertIndex + 3);
        }

        Mesh mesh = new Mesh();
        mesh.name = "RoadMesh_" + road.name;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
    #endregion

    #region Building Generation
    public void GenerateBuildings()
    {
        ClearBuildings();

        if (buildingPrefabs == null || buildingPrefabs.Length == 0)
        {
            Debug.LogWarning("Building prefabs array is empty. Cannot generate buildings.");
            return;
        }

        GameObject buildingContainer = new GameObject(BUILDING_CONTAINER_NAME);
        buildingContainer.transform.SetParent(transform);

        foreach (var road in roads)
        {
            if (road.nodes.Count < 2) continue;

            float roadLength = 0;
            for (int i = 0; i < road.nodes.Count - 1; i++)
            {
                roadLength += Vector3.Distance(road.nodes[i], road.nodes[i + 1]);
            }

            float currentDistance = 0;
            while (currentDistance < roadLength)
            {
                currentDistance += buildingSpacing + Random.Range(buildingRandomSpacing.x, buildingRandomSpacing.y);
                if (currentDistance >= roadLength) break;

                GetPathPositionAndDirection(road, currentDistance, out Vector3 position, out Vector3 direction);

                PlaceBuilding(position, direction, 1, buildingContainer.transform);  // Right side
                PlaceBuilding(position, direction, -1, buildingContainer.transform); // Left side
            }
        }
    }

    public void ClearBuildings()
    {
        Transform existingContainer = transform.Find(BUILDING_CONTAINER_NAME);
        if (existingContainer != null)
        {
            DestroyImmediate(existingContainer.gameObject);
        }
    }

    private void GetPathPositionAndDirection(Road road, float distance, out Vector3 position, out Vector3 direction)
    {
        position = road.nodes[0];
        direction = (road.nodes.Count > 1) ? (road.nodes[1] - road.nodes[0]).normalized : Vector3.forward;

        float traveled = 0;
        for (int i = 0; i < road.nodes.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(road.nodes[i], road.nodes[i + 1]);
            if (traveled + segmentLength >= distance)
            {
                float distIntoSegment = distance - traveled;
                direction = (road.nodes[i + 1] - road.nodes[i]).normalized;
                position = road.nodes[i] + direction * distIntoSegment;
                return;
            }
            traveled += segmentLength;
        }
    }

    private void PlaceBuilding(Vector3 position, Vector3 direction, int side, Transform parent)
    {
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized * side;
        float offset = buildingOffset + Random.Range(buildingRandomOffset.x, buildingRandomOffset.y);
        Vector3 buildingPosition = position + perpendicular * offset;

        if (Physics.Raycast(buildingPosition + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f))
        {
            buildingPosition.y = hit.point.y;
        }
        else
        {
            buildingPosition.y = transform.position.y;
        }

        Quaternion buildingRotation = Quaternion.LookRotation(-perpendicular);

        GameObject prefab = buildingPrefabs[Random.Range(0, buildingPrefabs.Length)];
        if (prefab == null) return;

        GameObject newBuilding = Instantiate(prefab, buildingPosition, buildingRotation);
        newBuilding.transform.SetParent(parent);
    }
    #endregion

    #region Block Generation
    public void FindIntersections()
    {
        intersectionPoints = new List<Vector3>();
        List<(Vector3, Vector3)> allSegments = new List<(Vector3, Vector3)>();

        // 1. Collect all segments
        foreach (var road in roads)
        {
            for (int i = 0; i < road.nodes.Count - 1; i++)
            {
                allSegments.Add((road.nodes[i], road.nodes[i + 1]));
            }
        }

        // 2. Check for intersections
        for (int i = 0; i < allSegments.Count; i++)
        {
            for (int j = i + 1; j < allSegments.Count; j++)
            {
                Vector2 p1 = new Vector2(allSegments[i].Item1.x, allSegments[i].Item1.z);
                Vector2 p2 = new Vector2(allSegments[i].Item2.x, allSegments[i].Item2.z);
                Vector2 p3 = new Vector2(allSegments[j].Item1.x, allSegments[j].Item1.z);
                Vector2 p4 = new Vector2(allSegments[j].Item2.x, allSegments[j].Item2.z);

                if (LineSegementsIntersect(p1, p2, p3, p4, out Vector2 intersection2D))
                {
                    float intersectionY = (allSegments[i].Item1.y + allSegments[i].Item2.y + allSegments[j].Item1.y + allSegments[j].Item2.y) / 4f;
                    intersectionPoints.Add(new Vector3(intersection2D.x, intersectionY, intersection2D.y));
                }
            }
        }
    }

    public void BuildGraph()
    {
        graphNodes.Clear();
        graphEdges.Clear();

        // Ensure intersections are found first
        FindIntersections();

        // Use a dictionary to store unique nodes and map Vector3 positions to GraphNode objects
        Dictionary<Vector3, GraphNode> uniqueNodes = new Dictionary<Vector3, GraphNode>(new Vector3EqualityComparer());

        // Add all original road nodes
        foreach (var road in roads)
        {
            foreach (var nodePos in road.nodes)
            {
                if (!uniqueNodes.ContainsKey(nodePos))
                {
                    GraphNode newNode = new GraphNode(nodePos);
                    uniqueNodes.Add(nodePos, newNode);
                    graphNodes.Add(newNode);
                }
            }
        }

        // Add all intersection points
        foreach (var intersectionPos in intersectionPoints)
        {
            if (!uniqueNodes.ContainsKey(intersectionPos))
            {
                GraphNode newNode = new GraphNode(intersectionPos);
                uniqueNodes.Add(intersectionPos, newNode);
                graphNodes.Add(newNode);
            }
        }

        // Now, create edges
        foreach (var road in roads)
        {
            for (int i = 0; i < road.nodes.Count - 1; i++)
            {
                Vector3 segmentStart = road.nodes[i];
                Vector3 segmentEnd = road.nodes[i + 1];

                List<GraphNode> nodesOnSegment = new List<GraphNode>();
                nodesOnSegment.Add(uniqueNodes[segmentStart]);
                nodesOnSegment.Add(uniqueNodes[segmentEnd]);

                // Find all intersection points that lie on this segment
                foreach (var intersectionPos in intersectionPoints)
                {
                    // Check if the intersection point is on the current segment
                    // Use 2D projection for this check
                    Vector2 segStart2D = new Vector2(segmentStart.x, segmentStart.z);
                    Vector2 segEnd2D = new Vector2(segmentEnd.x, segmentEnd.z);
                    Vector2 intersect2D = new Vector2(intersectionPos.x, intersectionPos.z);

                    // Check if point is collinear and within the segment bounds
                    float crossProduct = (intersect2D.y - segStart2D.y) * (segEnd2D.x - segStart2D.x) -
                                         (intersect2D.x - segStart2D.x) * (segEnd2D.y - segStart2D.y);

                    if (Mathf.Abs(crossProduct) < 0.01f) // Collinear (within tolerance)
                    {
                        float dotProduct = (intersect2D.x - segStart2D.x) * (segEnd2D.x - segStart2D.x) +
                                           (intersect2D.y - segStart2D.y) * (segEnd2D.y - segStart2D.y);
                        float squaredLength = (segEnd2D.x - segStart2D.x) * (segEnd2D.x - segStart2D.x) +
                                              (segEnd2D.y - segStart2D.y) * (segEnd2D.y - segStart2D.y);

                        if (dotProduct >= 0 && dotProduct <= squaredLength) // Within segment bounds
                        {
                            // Ensure it's not one of the segment's endpoints (already added)
                            if (!uniqueNodes[segmentStart].Equals(uniqueNodes[intersectionPos]) &&
                                !uniqueNodes[segmentEnd].Equals(uniqueNodes[intersectionPos]))
                            {
                                nodesOnSegment.Add(uniqueNodes[intersectionPos]);
                            }
                        }
                    }
                }

                // Sort nodes along the segment
                nodesOnSegment.Sort((n1, n2) =>
                {
                    float dist1 = Vector3.Distance(segmentStart, n1.position);
                    float dist2 = Vector3.Distance(segmentStart, n2.position);
                    return dist1.CompareTo(dist2);
                });

                // Create edges between sorted nodes
                for (int k = 0; k < nodesOnSegment.Count - 1; k++)
                {
                    GraphNode nodeA = nodesOnSegment[k];
                    GraphNode nodeB = nodesOnSegment[k + 1];

                    // Avoid duplicate edges (e.g., if two roads share a segment)
                    bool edgeExists = false;
                    foreach (var existingEdge in nodeA.edges)
                    {
                        if ((existingEdge.startNode == nodeA && existingEdge.endNode == nodeB) ||
                            (existingEdge.startNode == nodeB && existingEdge.endNode == nodeA))
                        {
                            edgeExists = true;
                            break;
                        }
                    }

                    if (!edgeExists)
                    {
                        GraphEdge newEdge = new GraphEdge(nodeA, nodeB);
                        graphEdges.Add(newEdge);
                        nodeA.edges.Add(newEdge);
                        nodeB.edges.Add(newEdge); // Add to both ends
                    }
                }
            }
        }
    }

    private bool LineSegementsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);

        if (Mathf.Approximately(d, 0.0f))
        {
            return false;
        }

        float t = ((p1.x - p3.x) * (p4.y - p3.y) - (p1.y - p3.y) * (p4.x - p3.x)) / d;
        float u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / d;

        if (t >= 0.0f && t <= 1.0f && u >= 0.0f && u <= 1.0f)
        {
            intersection.x = p1.x + t * (p2.x - p1.x);
            intersection.y = p1.y + t * (p2.y - p1.y);
            return true;
        }

        return false;
    }
    #endregion

    #region Block Finding
    public void FindCityBlocks()
    {
        cityBlocks.Clear();
        if (graphNodes.Count == 0 || graphEdges.Count == 0)
        {
            Debug.LogWarning("Graph is empty. Build graph first.");
            return;
        }

        // Keep track of traversed edges to avoid duplicate cycles
        Dictionary<GraphEdge, bool> traversedEdges = new Dictionary<GraphEdge, bool>();
        foreach (var edge in graphEdges)
        {
            traversedEdges[edge] = false;
        }

        foreach (var startEdge in graphEdges)
        {
            // Try to find a cycle starting from this edge
            if (!traversedEdges[startEdge])
            {
                List<Vector3> currentCycle = new List<Vector3>();
                GraphNode currentNode = startEdge.startNode;
                GraphEdge currentEdge = startEdge;
                GraphNode previousNode = startEdge.endNode; // To determine incoming direction

                int maxIterations = 1000; // Safety break
                for (int i = 0; i < maxIterations; i++)
                {
                    currentCycle.Add(currentNode.position);
                    traversedEdges[currentEdge] = true; // Mark as traversed

                    // Find the next edge using the "right-hand rule"
                    GraphNode nextNode = (currentEdge.startNode == currentNode) ? currentEdge.endNode : currentEdge.startNode;
                    
                    // Sort edges around nextNode to find the "most clockwise" one
                    List<GraphEdge> outgoingEdges = nextNode.edges.Where(e => e != currentEdge).ToList();
                    if (outgoingEdges.Count == 0) break; // Dead end

                    // Calculate incoming vector
                    Vector2 incomingVec = new Vector2(currentNode.position.x - nextNode.position.x, currentNode.position.z - nextNode.position.z).normalized;

                    GraphEdge nextBestEdge = null;
                    float minAngle = 361f; // Greater than 360

                    foreach (var outgoingEdge in outgoingEdges)
                    {
                        Vector3 otherNodePos = (outgoingEdge.startNode == nextNode) ? outgoingEdge.endNode.position : outgoingEdge.startNode.position;
                        Vector2 outgoingVec = new Vector2(otherNodePos.x - nextNode.position.x, otherNodePos.z - nextNode.position.z).normalized;

                        // Calculate angle from incoming to outgoing (clockwise)
                        float angle = Vector2.SignedAngle(incomingVec, outgoingVec);
                        if (angle < 0) angle += 360; // Normalize to 0-360

                        // We want the smallest positive angle (most clockwise turn)
                        if (angle < minAngle)
                        {
                            minAngle = angle;
                            nextBestEdge = outgoingEdge;
                        }
                    }

                    if (nextBestEdge == null) break; // Should not happen if graph is connected

                    // Move to the next edge
                    previousNode = currentNode;
                    currentNode = nextNode;
                    currentEdge = nextBestEdge;

                    // Check if we completed a cycle
                    if (currentNode == startEdge.startNode && currentEdge == startEdge)
                    {
                        // Found a cycle!
                        cityBlocks.Add(currentCycle);
                        break;
                    }
                }
            }
        }
    }
    #endregion
}
