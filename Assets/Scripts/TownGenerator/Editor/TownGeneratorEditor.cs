using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;
using UnityEngine.AI;

[CustomEditor(typeof(TownGenerator))]
public class TownGeneratorEditor : Editor
{
    private int selectedRoadIndex = -1;
    private bool isEditingGoals = false;

    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields (roadWidth, roadMaterial, etc.)
        DrawDefaultInspector();

        EditorGUILayout.Space();

        TownGenerator generator = (TownGenerator)target;

        // --- Mode Selection ---
        EditorGUILayout.LabelField("Editing Mode", EditorStyles.boldLabel);
        isEditingGoals = EditorGUILayout.Toggle("Edit Goals", isEditingGoals);
        if (isEditingGoals)
        {
            selectedRoadIndex = -1; // Disable road editing when goal editing is active
        }
        EditorGUILayout.Space();


        // --- Road List UI ---
        GUI.enabled = !isEditingGoals; // Disable road UI if editing goals
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
        GUI.enabled = true;
        // --- End Road List UI ---

        EditorGUILayout.Space(10);

        // --- Goal UI ---
        GUI.enabled = isEditingGoals;
        EditorGUILayout.LabelField("Goals", EditorStyles.boldLabel);
        if (GUILayout.Button("Clear All Goals"))
        {
            Undo.RecordObject(generator, "Clear All Goals");
            generator.goalPositions.Clear();
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }
        GUI.enabled = true;
        // --- End Goal UI ---

        EditorGUILayout.Space(10);

        // --- Generation Buttons ---
        EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
        if (GUILayout.Button("Generate All (Roads, Buildings, Goals)"))
        {
            generator.GenerateAll();
        }

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

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Goals"))
        {
            generator.GenerateGoals();
        }

        if (GUILayout.Button("Clear Goals"))
        {
            generator.ClearGoals();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Bake NavMesh"))
        {
            BakeNavMesh(generator);
        }
        // --- End Generation Buttons ---


        EditorGUILayout.Space();
        string helpText = isEditingGoals ?
            "Editing Goals:\nShift + Left Click: Add Goal\nCtrl + Left Click: Remove Goal" :
            "Editing Roads:\nSelect a road to edit it in the Scene View.\nShift + Left Click: Add Node\nCtrl + Left Click: Remove Node";
        EditorGUILayout.HelpBox(helpText, MessageType.Info);
    }

    private void BakeNavMesh(TownGenerator generator)
    {
        NavMeshSurface surface = generator.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            Debug.Log("NavMeshSurface component not found, adding one.");
            surface = generator.gameObject.AddComponent<NavMeshSurface>();
        }

        // Configure the surface to bake specific layers.
        // This requires "Ground", "Road", and "Building" layers to be created in the project settings.
        string[] includedLayers = { "Ground", "Road", "Building" };
        surface.layerMask = LayerMask.GetMask(includedLayers);
        
        // Collect geometry from children of this object, which is where the roads and buildings are generated.
        surface.collectObjects = CollectObjects.Children;

        // Mark the component as 'dirty'. This ensures the editor saves our layer mask changes
        // before the bake process begins, preventing it from reverting to 'Everything'.
        EditorUtility.SetDirty(surface);

        Debug.Log("Starting NavMesh bake for layers: Ground, Road, Building...");
        surface.BuildNavMesh();
        Debug.Log("NavMesh bake complete.");
    }

    private void OnSceneGUI()
    {
        TownGenerator generator = (TownGenerator)target;
        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (isEditingGoals)
        {
            DrawGoalHandles(generator, e);
        }
        else
        {
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

            // Handle input only if a road is selected
            if (selectedRoadIndex != -1 && selectedRoadIndex < generator.roads.Count)
            {
                HandleSceneInput(generator.roads[selectedRoadIndex], generator, e);
            }
        }
    }

    private void DrawGoalHandles(TownGenerator generator, Event e)
    {
        // --- Input Handling First ---
        // Handle adding goals with Shift + Left Click
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            // Raycast against ground/road/building layers to place goal accurately
            int placementMask = LayerMask.GetMask("Ground", "Road", "Building", "Default");
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementMask))
            {
                Undo.RecordObject(generator, "Add Goal Position");
                generator.goalPositions.Add(hit.point);
                EditorUtility.SetDirty(generator);
                e.Use(); // Consume the event
            }
        }
        // Handle removing goals with Ctrl + Left Click
        else if (e.type == EventType.MouseDown && e.button == 0 && e.control)
        {
            int nodeToDelete = -1;
            float minDistance = 15f;

            for (int i = 0; i < generator.goalPositions.Count; i++)
            {
                float dist = Vector2.Distance(e.mousePosition, HandleUtility.WorldToGUIPoint(generator.goalPositions[i]));
                if (dist < minDistance)
                {
                    nodeToDelete = i;
                    break;
                }
            }

            if (nodeToDelete != -1)
            {
                Undo.RecordObject(generator, "Remove Goal Position");
                generator.goalPositions.RemoveAt(nodeToDelete);
                EditorUtility.SetDirty(generator);
                e.Use(); // Consume the event
            }
        }

        // --- Then Draw Visuals ---
        // Draw existing goal handles
        for (int i = 0; i < generator.goalPositions.Count; i++)
        {
            Handles.color = Color.yellow;
            
            EditorGUI.BeginChangeCheck();
            float handleSize = HandleUtility.GetHandleSize(generator.goalPositions[i]) * 0.15f;
            Vector3 newPos = Handles.FreeMoveHandle(generator.goalPositions[i], handleSize, Vector3.zero, Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, "Move Goal Position");
                generator.goalPositions[i] = newPos;
                EditorUtility.SetDirty(generator);
            }
        }
    }

    private void DrawRoadHandles(Road road, TownGenerator generator)
    {
        for (int i = 0; i < road.nodes.Count; i++)
        {
            Handles.color = Color.red;
            
            EditorGUI.BeginChangeCheck();
            float handleSize = HandleUtility.GetHandleSize(road.nodes[i]) * 0.1f;
            Vector3 newPos = Handles.FreeMoveHandle(road.nodes[i], handleSize, Vector3.zero, Handles.SphereHandleCap);

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