using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TownGenerator))]
public class TownGeneratorEditor : Editor
{
    private int selectedRoadIndex = -1;

    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields (roadWidth, roadMaterial, etc.)
        DrawDefaultInspector();

        EditorGUILayout.Space();

        TownGenerator generator = (TownGenerator)target;

        // --- Road List UI ---
        EditorGUILayout.LabelField("Roads", EditorStyles.boldLabel);
        for (int i = 0; i < generator.roads.Count; i++)
        {
            GUI.backgroundColor = (i == selectedRoadIndex) ? Color.cyan : Color.white;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            generator.roads[i].name = EditorGUILayout.TextField(generator.roads[i].name, GUILayout.MinWidth(100));
            EditorGUILayout.LabelField($"({generator.roads[i].nodes.Count} nodes)", GUILayout.Width(80));

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                selectedRoadIndex = i;
                SceneView.RepaintAll(); // Repaint to show new selection
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                Undo.RecordObject(generator, "Delete Road");
                generator.roads.RemoveAt(i);
                if (selectedRoadIndex == i) selectedRoadIndex = -1;
                else if (selectedRoadIndex > i) selectedRoadIndex--;
                EditorUtility.SetDirty(generator);
                SceneView.RepaintAll();
                i--; // Adjust index after removal
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Add New Road"))
        {
            Undo.RecordObject(generator, "Add New Road");
            Road newRoad = new Road() { name = "Road " + (generator.roads.Count + 1) };
            generator.roads.Add(newRoad);
            selectedRoadIndex = generator.roads.Count - 1;
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }
        // --- End Road List UI ---

        EditorGUILayout.Space(10);

        // --- Generation Buttons ---
        EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate All Roads"))
        {
            generator.GenerateRoad();
        }

        if (GUILayout.Button("Clear Roads"))
        {
            generator.ClearRoads();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Buildings"))
        {
            generator.GenerateBuildings();
        }

        if (GUILayout.Button("Clear Buildings"))
        {
            generator.ClearBuildings();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
        if (GUILayout.Button("Find Intersections"))
        {
            generator.FindIntersections();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Build Graph"))
        {
            generator.BuildGraph();
            SceneView.RepaintAll();
        }
        // --- End Generation Buttons ---


        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Select a road to edit it in the Scene View.\nShift + Left Click: Add Node\nCtrl + Left Click: Remove Node", MessageType.Info);
    }

    private void OnSceneGUI()
    {
        TownGenerator generator = (TownGenerator)target;
        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        // Draw all roads, with special drawing for the selected one
        for (int i = 0; i < generator.roads.Count; i++)
        {
            if (i == selectedRoadIndex)
            {
                DrawRoadHandles(generator.roads[i], generator);
            }
            else
            {
                DrawRoadAsSimpleLines(generator.roads[i]);
            }
        }

        DrawIntersections(generator);
        DrawGraph(generator); // Draw the graph

        // Handle input only if a road is selected
        if (selectedRoadIndex != -1 && selectedRoadIndex < generator.roads.Count)
        {
            HandleSceneInput(generator.roads[selectedRoadIndex], generator, e);
        }
    }

    private void DrawRoadHandles(Road road, TownGenerator generator)
    {
        for (int i = 0; i < road.nodes.Count; i++)
        {
            Handles.color = Color.red;
            
            EditorGUI.BeginChangeCheck();
            float handleSize = HandleUtility.GetHandleSize(road.nodes[i]) * 0.1f;
            var fmh_144_68_638997718916906200 = Quaternion.identity; Vector3 newPos = Handles.FreeMoveHandle(road.nodes[i], handleSize, Vector3.zero, Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, "Move Road Node");
                road.nodes[i] = newPos;
                EditorUtility.SetDirty(generator);
            }

            if (i > 0)
            {
                Handles.color = Color.white;
                Handles.DrawLine(road.nodes[i - 1], road.nodes[i]);
            }
        }
    }

    private void DrawRoadAsSimpleLines(Road road)
    {
        Handles.color = Color.gray;
        for (int i = 0; i < road.nodes.Count - 1; i++)
        {
            Handles.DrawLine(road.nodes[i], road.nodes[i + 1]);
        }
    }

    private void DrawIntersections(TownGenerator generator)
    {
        if (generator.intersectionPoints == null) return;
        Handles.color = Color.blue;
        foreach (var point in generator.intersectionPoints)
        {
            float handleSize = HandleUtility.GetHandleSize(point) * 0.2f;
            Handles.SphereHandleCap(0, point, Quaternion.identity, handleSize, EventType.Repaint);
        }
    }

    private void DrawGraph(TownGenerator generator)
    {
        if (generator.graphNodes == null || generator.graphEdges == null) return;

        // Draw Graph Nodes (Yellow)
        Handles.color = Color.yellow;
        foreach (var node in generator.graphNodes)
        {
            float handleSize = HandleUtility.GetHandleSize(node.position) * 0.15f;
            Handles.SphereHandleCap(0, node.position, Quaternion.identity, handleSize, EventType.Repaint);
        }

        // Draw Graph Edges (Green)
        Handles.color = Color.green;
        foreach (var edge in generator.graphEdges)
        {
            Handles.DrawLine(edge.startNode.position, edge.endNode.position);
        }
    }

    private void HandleSceneInput(Road road, TownGenerator generator, Event e)
    {
        // Handle adding nodes with Shift + Left Click
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 newPoint = ray.GetPoint(distance);
                Undo.RecordObject(generator, "Add Road Node");
                road.nodes.Add(newPoint);
                EditorUtility.SetDirty(generator);
                e.Use();
            }
        }

        // Handle removing nodes with Ctrl + Left Click
        if (e.type == EventType.MouseDown && e.button == 0 && e.control)
        {
            int nodeToDelete = -1;
            float minDistance = 15f;

            for (int i = 0; i < road.nodes.Count; i++)
            {
                float dist = Vector2.Distance(e.mousePosition, HandleUtility.WorldToGUIPoint(road.nodes[i]));
                if (dist < minDistance)
                {
                    nodeToDelete = i;
                    break;
                }
            }

            if (nodeToDelete != -1)
            {
                Undo.RecordObject(generator, "Remove Road Node");
                road.nodes.RemoveAt(nodeToDelete);
                EditorUtility.SetDirty(generator);
                e.Use();
            }
        }
    }
}