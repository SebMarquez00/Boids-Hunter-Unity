using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonNavigationBuilder
{
    [MenuItem("Tools/IA/Entrega 2/Agregar navegacion al escenario")]
    public static void AddNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath || scene.isDirty)
        {
            Debug.LogWarning("Abri DungeonSimulation y guarda sus cambios antes de configurar la navegacion.");
            return;
        }
        ScenarioReferences scenario = GetScenario(scene);
        NavigationGraph graph = scenario.GetComponentInChildren<NavigationGraph>(true);
        if (graph == null)
        {
            GameObject navigation = new GameObject("Navigation");
            navigation.transform.SetParent(scenario.transform, false);
            graph = navigation.AddComponent<NavigationGraph>();
            ConfigureGraph(graph, scenario);
            GenerateNodes(graph);
        }
        DungeonHunterSetup.Configure(scenario, graph);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se pudo guardar la navegacion.");
        Debug.Log("Navegacion del Cazador integrada: Search, A* y recalculo ante bloqueos. HunterSpawn permanece fijo.");
    }

    [MenuItem("Tools/IA/Entrega 2/Regenerar grafo")]
    public static void Regenerate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath) return;
        ScenarioReferences scenario = GetScenario(scene);
        NavigationGraph graph = scenario.GetComponentInChildren<NavigationGraph>(true);
        if (graph == null) { Debug.LogWarning("Primero agrega la navegacion al escenario."); return; }
        Undo.RecordObject(graph, "Regenerar grafo");
        GenerateNodes(graph);
        EditorUtility.SetDirty(graph);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Grafo regenerado: " + graph.Nodes.Count + " nodos. Guarda la escena para conservarlo.");
    }

    private static ScenarioReferences GetScenario(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ScenarioReferences scenario = root.GetComponentInChildren<ScenarioReferences>(true);
            if (scenario != null) return scenario;
        }
        throw new InvalidOperationException("Falta ScenarioReferences.");
    }

    private static void ConfigureGraph(NavigationGraph graph, ScenarioReferences scenario)
    {
        int layer = LayerMask.NameToLayer("MapObstacle");
        if (layer < 0) throw new InvalidOperationException("Falta MapObstacle.");
        SerializedObject data = new SerializedObject(graph);
        data.FindProperty("_scenario").objectReferenceValue = scenario;
        data.FindProperty("_obstacleMask").intValue = 1 << layer;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void GenerateNodes(NavigationGraph graph)
    {
        Physics.SyncTransforms();
        List<NavigationNode> nodes = new List<NavigationNode>();
        Dictionary<Vector2Int, int> cells = new Dictionary<Vector2Int, int>();
        for (int z = 0; z < DungeonLayout.Depth; z++)
        {
            for (int x = 0; x < DungeonLayout.Width; x++)
            {
                Vector3 position = DungeonLayout.ToWorld(x + 0.5f, z + 0.5f, graph.SamplingHeight);
                if (!graph.CanTraverse(position, position, NavigationAccess.Hunter)) continue;
                cells.Add(new Vector2Int(x, z), nodes.Count);
                nodes.Add(new NavigationNode(position, graph.IsSafePosition(position)));
            }
        }
        foreach (KeyValuePair<Vector2Int, int> cell in cells)
        {
            ConnectNeighbor(cell.Key + Vector2Int.right, cell.Value, cells, nodes, graph);
            ConnectNeighbor(cell.Key + Vector2Int.up, cell.Value, cells, nodes, graph);
            ConnectDiagonal(cell.Key, new Vector2Int(1, 1), cell.Value, cells, nodes, graph);
            ConnectDiagonal(cell.Key, new Vector2Int(-1, 1), cell.Value, cells, nodes, graph);
        }
        graph.ReplaceNodes(nodes);
    }

    private static void ConnectNeighbor(Vector2Int coordinate, int from, Dictionary<Vector2Int, int> cells,
        List<NavigationNode> nodes, NavigationGraph graph)
    {
        if (!cells.TryGetValue(coordinate, out int to)
            || !graph.CanTraverse(nodes[from].Position, nodes[to].Position, NavigationAccess.Hunter)) return;
        nodes[from].AddNeighbor(to);
        nodes[to].AddNeighbor(from);
    }

    private static void ConnectDiagonal(Vector2Int coordinate, Vector2Int direction, int from,
        Dictionary<Vector2Int, int> cells, List<NavigationNode> nodes, NavigationGraph graph)
    {
        if (!cells.TryGetValue(coordinate + direction, out int to)
            || !cells.TryGetValue(coordinate + new Vector2Int(direction.x, 0), out int sideX)
            || !cells.TryGetValue(coordinate + new Vector2Int(0, direction.y), out int sideZ)) return;
        // Ambos recorridos por los lados deben estar libres antes de cruzar la diagonal.
        if (!graph.CanTraverse(nodes[from].Position, nodes[sideX].Position, NavigationAccess.Hunter)
            || !graph.CanTraverse(nodes[sideX].Position, nodes[to].Position, NavigationAccess.Hunter)
            || !graph.CanTraverse(nodes[from].Position, nodes[sideZ].Position, NavigationAccess.Hunter)
            || !graph.CanTraverse(nodes[sideZ].Position, nodes[to].Position, NavigationAccess.Hunter)) return;
        ConnectNeighbor(coordinate + direction, from, cells, nodes, graph);
    }
}
