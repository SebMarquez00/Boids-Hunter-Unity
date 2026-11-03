using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class NavigationValidation
{
    private const float Tolerance = 0.002f;
    private static int _checks;

    [MenuItem("Tools/IA/Entrega 2/Validar continuidad de movimiento")]
    public static void RunMotionRegressions()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Esta validacion requiere Edit Mode.");
        Scene original = SceneManager.GetActiveScene();
        Require(original.path == DungeonMapBuilder.ScenePath, "Abri DungeonSimulation antes de validar.");
        NavigationGraph graph = FindSingle<NavigationGraph>(original);
        Scene temporary = default;
        try
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            ValidateMotionContinuity(graph);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (temporary.IsValid()) EditorSceneManager.CloseScene(temporary, true);
        }
    }

    [MenuItem("Tools/IA/Entrega 2/Validar navegacion")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Esta validacion requiere Edit Mode.");
        Scene originalScene = SceneManager.GetActiveScene();
        Require(originalScene.path == DungeonMapBuilder.ScenePath, "Abri DungeonSimulation antes de validar.");
        NavigationGraph graph = FindSingle<NavigationGraph>(originalScene);
        ScenarioReferences scenario = FindSingle<ScenarioReferences>(originalScene);
        Scene temporaryScene = default;
        _checks = 0;
        try
        {
            // La escena real permanece abierta para consultar sus colliders, sin editarla.
            temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporaryScene);
            Physics.SyncTransforms();
            ValidateGraph(graph, scenario);
            ValidateScenePaths(graph, scenario);
            ValidateDiagonals(graph);
            ValidateAlgorithm();
            ValidateConnectionsToGraph(graph, scenario);
            ValidateSafeCrossing(graph);
            ValidateFollower(graph, scenario);
            ValidateMotionContinuity(graph);
            Debug.Log($"Navegacion: {_checks} comprobaciones correctas; grafo, A*, refugios y seguidor validados.");
        }
        finally
        {
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            if (temporaryScene.IsValid()) EditorSceneManager.CloseScene(temporaryScene, true);
            Physics.SyncTransforms();
        }
    }

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        T found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T candidate in root.GetComponentsInChildren<T>(true))
            {
                Require(found == null, "Hay mas de un " + typeof(T).Name + " en la escena.");
                found = candidate;
            }
        Require(found != null, "Falta " + typeof(T).Name + " en la escena.");
        return found;
    }

    private static void ValidateGraph(NavigationGraph graph, ScenarioReferences scenario)
    {
        Require(graph.Nodes.Count == 1347, "El mapa debe conservar sus 1347 nodos transitables.");
        Require(graph.AgentRadius >= 0.375f && graph.AgentRadius < 0.5f, "El radio debe cubrir la capsula y caber en la grilla.");
        int obstacleLayer = LayerMask.NameToLayer("MapObstacle");
        Require(obstacleLayer >= 0 && (graph.ObstacleMask.value & (1 << obstacleLayer)) != 0,
            "La mascara del grafo debe incluir MapObstacle.");
        HashSet<Vector3> positions = new HashSet<Vector3>();
        Dictionary<Vector3, int> nodeIndices = new Dictionary<Vector3, int>();
        for (int i = 0; i < graph.Nodes.Count; i++)
            if (graph.Nodes[i] != null) nodeIndices[graph.Nodes[i].Position] = i;
        int safeNodes = 0;
        int diagonals = 0;
        bool diagonalSidesClear = true;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            NavigationNode node = graph.Nodes[i];
            Require(node != null && positions.Add(node.Position), "Hay nodos nulos o posiciones duplicadas.");
            Require(Mathf.Abs(node.Position.y - graph.SamplingHeight) <= Tolerance, "Un nodo tiene una altura incorrecta.");
            Require(!Physics.CheckSphere(node.Position, graph.AgentRadius, graph.ObstacleMask, QueryTriggerInteraction.Ignore),
                "El volumen del agente invade un muro en el nodo " + i + ".");
            bool isSafe = false;
            foreach (ScenarioZone safe in scenario.SafeZones) isSafe |= safe.Contains(node.Position);
            Require(node.IsSafeZone == isSafe, "Un nodo tiene una clasificacion de refugio incorrecta.");
            Require(graph.CanUseNode(i, NavigationAccess.Hunter), "El Cazador debe poder utilizar los nodos del escenario.");
            Require(graph.CanUseNode(i, NavigationAccess.Wolf) == !isSafe, "El filtro de nodos para lobos es incorrecto.");
            if (isSafe) safeNodes++;
            HashSet<int> neighbors = new HashSet<int>();
            foreach (int neighbor in node.Neighbors)
            {
                Require(neighbor >= 0 && neighbor < graph.Nodes.Count && neighbor != i && neighbors.Add(neighbor),
                    "Hay una arista invalida, repetida o hacia el mismo nodo.");
                NavigationNode next = graph.Nodes[neighbor];
                Require(HasNeighbor(next, i), "Una conexion del mapa no es bidireccional.");
                Vector3 offset = next.Position - node.Position;
                float dx = Mathf.Abs(offset.x);
                float dz = Mathf.Abs(offset.z);
                bool cardinal = (Mathf.Abs(dx - 1f) <= Tolerance && dz <= Tolerance)
                    || (dx <= Tolerance && Mathf.Abs(dz - 1f) <= Tolerance);
                bool diagonal = Mathf.Abs(dx - 1f) <= Tolerance && Mathf.Abs(dz - 1f) <= Tolerance;
                Require(cardinal || diagonal, "La grilla debe conectar vecinos cardinales o diagonales de una celda.");
                if (neighbor <= i) continue;
                Require(PhysicalSegmentClear(graph, node.Position, next.Position), "Una arista atraviesa un obstaculo.");
                if (!diagonal) continue;
                diagonals++;
                diagonalSidesClear &= HasClearDiagonalSides(graph, i, neighbor, nodeIndices);
            }
        }
        Require(safeNodes > 0, "El grafo no contiene nodos de refugio.");
        Require(diagonals > 0, "El grafo no contiene diagonales: regeneralo para usar ocho vecinos.");
        Require(diagonalSidesClear, "Una diagonal corta una esquina: faltan vecinos laterales o alguno de sus lados esta bloqueado.");
        Require(!graph.CanUseNode(-1, NavigationAccess.Hunter)
            && !graph.CanUseNode(graph.Nodes.Count, NavigationAccess.Hunter), "Los indices invalidos deben rechazarse.");
        ValidateConnectivity(graph, NavigationAccess.Hunter);
        ValidateConnectivity(graph, NavigationAccess.Wolf);
    }

    private static bool HasClearDiagonalSides(NavigationGraph graph, int from, int to, Dictionary<Vector3, int> indices)
    {
        Vector3 a = graph.Nodes[from].Position;
        Vector3 b = graph.Nodes[to].Position;
        if (!indices.TryGetValue(new Vector3(b.x, a.y, a.z), out int sideX)
            || !indices.TryGetValue(new Vector3(a.x, a.y, b.z), out int sideZ)) return false;
        return HasNeighbor(graph.Nodes[from], sideX) && HasNeighbor(graph.Nodes[sideX], to)
            && HasNeighbor(graph.Nodes[from], sideZ) && HasNeighbor(graph.Nodes[sideZ], to)
            && PhysicalSegmentClear(graph, a, graph.Nodes[sideX].Position)
            && PhysicalSegmentClear(graph, graph.Nodes[sideX].Position, b)
            && PhysicalSegmentClear(graph, a, graph.Nodes[sideZ].Position)
            && PhysicalSegmentClear(graph, graph.Nodes[sideZ].Position, b);
    }

    private static void ValidateDiagonals(NavigationGraph graph)
    {
        Vector3 from = DungeonLayout.ToWorld(51.5f, 23.5f, graph.SamplingHeight);
        Vector3 to = DungeonLayout.ToWorld(55.5f, 27.5f, graph.SamplingHeight);
        float length = ValidateRoute(graph, from, to, NavigationAccess.Hunter);
        Require(Mathf.Abs(length - 4f * Mathf.Sqrt(2f)) <= Tolerance,
            "En espacio abierto, cuatro diagonales deben recorrer 4 por raiz de 2 unidades.");
        CompareOptimalCost(graph, ClosestNode(graph, from), ClosestNode(graph, to), NavigationAccess.Hunter);

        from = DungeonLayout.ToWorld(4.5f, 15.5f, graph.SamplingHeight);
        to = DungeonLayout.ToWorld(5.5f, 14.5f, graph.SamplingHeight);
        int cornerStart = ClosestNode(graph, from);
        int cornerEnd = ClosestNode(graph, to);
        Require(Near(graph.Nodes[cornerStart].Position, from) && Near(graph.Nodes[cornerEnd].Position, to)
            && !HasNeighbor(graph.Nodes[cornerStart], cornerEnd) && !PhysicalSegmentClear(graph, from, to),
            "La diagonal junto a la esquina de la columna no debe existir ni permitir cortar el muro.");
        Require(Mathf.Abs(ValidateRoute(graph, from, to, NavigationAccess.Hunter) - 2f) <= Tolerance,
            "La esquina bloqueada debe rodearse por sus dos tramos cardinales.");
        ValidateBlockedDiagonal(graph);
    }

    private static void ValidateBlockedDiagonal(NavigationGraph graph)
    {
        Vector3 from = DungeonLayout.ToWorld(51.5f, 25.5f, graph.SamplingHeight);
        Vector3 to = DungeonLayout.ToWorld(52.5f, 26.5f, graph.SamplingHeight);
        int start = ClosestNode(graph, from);
        int goal = ClosestNode(graph, to);
        Require(HasNeighbor(graph.Nodes[start], goal) && graph.CanUseConnection(start, goal, NavigationAccess.Hunter),
            "Falta la diagonal libre utilizada para probar un bloqueo posterior.");
        GameObject obstacle = new GameObject("Validacion bloqueo de diagonal", typeof(BoxCollider));
        try
        {
            obstacle.layer = LayerMask.NameToLayer("MapObstacle");
            obstacle.transform.position = DungeonLayout.ToWorld(52f, 26f, 1f);
            obstacle.GetComponent<BoxCollider>().size = new Vector3(0.1f, 2f, 0.1f);
            Physics.SyncTransforms();
            Require(graph.CanUseNode(start, NavigationAccess.Hunter) && graph.CanUseNode(goal, NavigationAccess.Hunter)
                && !graph.CanUseConnection(start, goal, NavigationAccess.Hunter),
                "Un obstaculo sobre la diagonal debe invalidar su conexion aunque ambos extremos sigan libres.");
            Require(Mathf.Abs(ValidateRoute(graph, from, to, NavigationAccess.Hunter) - 2f) <= Tolerance,
                "A* debe sustituir la diagonal bloqueada por un rodeo libre de dos unidades.");
            CompareOptimalCost(graph, start, goal, NavigationAccess.Hunter);
        }
        finally
        {
            Object.DestroyImmediate(obstacle);
            Physics.SyncTransforms();
        }
        Require(graph.CanUseConnection(start, goal, NavigationAccess.Hunter),
            "La diagonal debe volver a estar disponible al retirar el obstaculo, sin regenerar el grafo.");
    }

    private static void ValidateConnectivity(NavigationGraph graph, NavigationAccess access)
    {
        int first = -1;
        int expected = 0;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            if (!graph.CanUseNode(i, access)) continue;
            if (first < 0) first = i;
            expected++;
        }
        Require(first >= 0, "No hay nodos permitidos para " + access + ".");
        bool[] visited = new bool[graph.Nodes.Count];
        Queue<int> pending = new Queue<int>();
        pending.Enqueue(first);
        visited[first] = true;
        int reached = 0;
        while (pending.Count > 0)
        {
            int current = pending.Dequeue();
            reached++;
            foreach (int next in graph.Nodes[current].Neighbors)
            {
                if (visited[next] || !graph.CanUseConnection(current, next, access)) continue;
                visited[next] = true;
                pending.Enqueue(next);
            }
        }
        Require(reached == expected, "Hay nodos permitidos aislados para " + access + ".");
    }

    private static void ValidateScenePaths(NavigationGraph graph, ScenarioReferences scenario)
    {
        Vector3 start = scenario.HunterSpawn.position;
        Vector3 hunting = scenario.HuntingZone.transform.position;
        Vector3 exit = scenario.ExitZone.transform.position;
        ValidateRoute(graph, start, hunting, NavigationAccess.Hunter);
        ValidateRoute(graph, hunting, exit, NavigationAccess.Hunter);
        ValidateRoute(graph, hunting, exit, NavigationAccess.Wolf);
        foreach (ScenarioZone safe in scenario.SafeZones)
        {
            ValidateRoute(graph, start, safe.transform.position, NavigationAccess.Hunter);
            RequireNoRoute(graph, start, safe.transform.position, NavigationAccess.Wolf);
            RequireNoRoute(graph, safe.transform.position, hunting, NavigationAccess.Wolf);
            CompareOptimalCost(graph, ClosestNode(graph, start), ClosestNode(graph, safe.transform.position), NavigationAccess.Hunter);
        }
        ValidateColumnRoute(graph, 2.5f, 13.5f);
        ValidateColumnRoute(graph, 24.5f, 35.5f);
        CompareOptimalCost(graph, ClosestNode(graph, start), ClosestNode(graph, hunting), NavigationAccess.Hunter);
        CompareOptimalCost(graph, ClosestNode(graph, hunting), ClosestNode(graph, exit), NavigationAccess.Wolf);
        RequireNoRoute(graph, start, DungeonLayout.ToWorld(8f, 17f), NavigationAccess.Hunter);
        RequireNoRoute(graph, DungeonLayout.ToWorld(8f, 17f), hunting, NavigationAccess.Hunter);
        RequireNoRoute(graph, start, new Vector3(-1000f, 0f, -1000f), NavigationAccess.Hunter);
        Require(graph.TryFindPath(start, start, NavigationAccess.Hunter, out List<Vector3> same, out float length)
            && same.Count > 0 && Mathf.Abs(length) <= Tolerance, "Origen igual a destino debe producir una ruta de longitud cero.");
    }

    private static void ValidateColumnRoute(NavigationGraph graph, float fromX, float toX)
    {
        Vector3 from = DungeonLayout.ToWorld(fromX, 17.5f, graph.SamplingHeight);
        Vector3 to = DungeonLayout.ToWorld(toX, 17.5f, graph.SamplingHeight);
        Require(!graph.CanTraverse(from, to, NavigationAccess.Hunter), "Una columna debe impedir el recorrido directo.");
        float length = ValidateRoute(graph, from, to, NavigationAccess.Hunter);
        Require(length > Vector3.Distance(from, to) + 1f, "La ruta debe rodear la columna.");
        CompareOptimalCost(graph, ClosestNode(graph, from), ClosestNode(graph, to), NavigationAccess.Hunter);
    }

    private static float ValidateRoute(NavigationGraph graph, Vector3 from, Vector3 to, NavigationAccess access)
    {
        Require(graph.TryFindPath(from, to, access, out List<Vector3> path, out float length), "No se encontro una ruta requerida para " + access + ".");
        from.y = to.y = graph.SamplingHeight;
        Require(path != null && path.Count > 0 && Near(path[0], from) && Near(path[path.Count - 1], to),
            "La ruta debe conectar las posiciones solicitadas, no solamente sus nodos.");
        float measuredLength = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            Require(PhysicalSegmentClear(graph, path[i - 1], path[i]), "Un tramo de la ruta invade geometria.");
            Require(graph.CanTraverse(path[i - 1], path[i], access), "Un tramo no respeta el perfil de acceso.");
            measuredLength += Vector3.Distance(path[i - 1], path[i]);
        }
        Require(Mathf.Abs(measuredLength - length) <= Tolerance, "La longitud informada no coincide con la ruta.");
        return length;
    }

    private static void RequireNoRoute(NavigationGraph graph, Vector3 from, Vector3 to, NavigationAccess access)
    {
        Require(!graph.TryFindPath(from, to, access, out List<Vector3> path, out _), "Un destino prohibido o invalido produjo una ruta.");
        Require(path == null || path.Count == 0, "Una busqueda fallida conserva puntos de otra ruta.");
    }

    private static void ValidateAlgorithm()
    {
        GameObject fixture = new GameObject("Validacion AStar");
        try
        {
            NavigationGraph graph = fixture.AddComponent<NavigationGraph>();
            Vector3 origin = new Vector3(10000f, 0.75f, 10000f);
            List<NavigationNode> nodes = new List<NavigationNode>
            {
                new NavigationNode(origin, false), new NavigationNode(origin + Vector3.forward * 4f, false),
                new NavigationNode(origin + Vector3.right * 2f, false), new NavigationNode(origin + Vector3.right * 4f, false),
                new NavigationNode(origin + Vector3.right * 6f, false), new NavigationNode(origin + Vector3.back * 5f, false)
            };
            Link(nodes, 0, 1); Link(nodes, 1, 4);
            Link(nodes, 0, 2); Link(nodes, 2, 3); Link(nodes, 3, 4);
            graph.ReplaceNodes(nodes);
            List<int> shortest = NavigationPathfinding.FindPath(graph, 0, 4, NavigationAccess.Hunter);
            Require(shortest.Count == 4 && shortest[1] == 2 && shortest[2] == 3,
                "A* debe preferir tres aristas de longitud total 6 frente a dos aristas mas largas.");
            CompareOptimalCost(graph, 0, 4, NavigationAccess.Hunter);
            Require(NavigationPathfinding.FindPath(graph, 0, 5, NavigationAccess.Hunter).Count == 0, "Un nodo aislado debe dar sin camino.");
            Require(NavigationPathfinding.FindPath(graph, -1, 4, NavigationAccess.Hunter).Count == 0
                && NavigationPathfinding.FindPath(graph, 0, 99, NavigationAccess.Hunter).Count == 0, "A* debe rechazar indices invalidos.");
            List<int> same = NavigationPathfinding.FindPath(graph, 2, 2, NavigationAccess.Hunter);
            Require(same.Count == 1 && same[0] == 2, "A* debe resolver origen igual a destino.");
            CompareOptimalCost(graph, 4, 0, NavigationAccess.Hunter);

            // La ruta corta pasa ahora por un refugio; el lobo debe elegir el rodeo.
            nodes[2] = new NavigationNode(origin + Vector3.right * 2f, true);
            nodes[2].AddNeighbor(0);
            nodes[2].AddNeighbor(3);
            graph.ReplaceNodes(nodes);
            List<int> wolf = NavigationPathfinding.FindPath(graph, 0, 4, NavigationAccess.Wolf);
            Require(wolf.Count == 3 && wolf[1] == 1, "El lobo debe rodear un nodo de refugio aunque encarezca la ruta.");
            CompareOptimalCost(graph, 0, 4, NavigationAccess.Wolf);
            Require(NavigationPathfinding.FindPath(graph, 0, 2, NavigationAccess.Wolf).Count == 0, "A* no debe finalizar en un refugio para lobos.");
        }
        finally { Object.DestroyImmediate(fixture); }
    }

    private static void CompareOptimalCost(NavigationGraph graph, int start, int goal, NavigationAccess access)
    {
        float expected = DijkstraCost(graph, start, goal, access);
        List<int> path = NavigationPathfinding.FindPath(graph, start, goal, access);
        Require(!float.IsPositiveInfinity(expected) && path.Count > 0 && path[0] == start && path[path.Count - 1] == goal,
            "A* y Dijkstra deben alcanzar los mismos nodos solicitados.");
        float cost = 0f;
        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < path.Count; i++)
        {
            Require(graph.CanUseNode(path[i], access) && seen.Add(path[i]), "La ruta contiene un nodo prohibido o un ciclo.");
            if (i == 0) continue;
            Require(graph.CanUseConnection(path[i - 1], path[i], access), "A* devolvio una conexion inexistente o prohibida.");
            cost += Vector3.Distance(graph.Nodes[path[i - 1]].Position, graph.Nodes[path[i]].Position);
        }
        Require(Mathf.Abs(cost - expected) <= Tolerance, "El coste de A* no coincide con Dijkstra independiente.");
    }

    // Referencia sin heuristica ni cola de prioridad compartida con A*.
    private static float DijkstraCost(NavigationGraph graph, int start, int goal, NavigationAccess access)
    {
        int count = graph.Nodes.Count;
        float[] distance = new float[count];
        bool[] visited = new bool[count];
        for (int i = 0; i < count; i++) distance[i] = float.PositiveInfinity;
        distance[start] = 0f;
        for (int iteration = 0; iteration < count; iteration++)
        {
            int current = -1;
            for (int i = 0; i < count; i++)
                if (!visited[i] && (current < 0 || distance[i] < distance[current])) current = i;
            if (current < 0 || float.IsPositiveInfinity(distance[current])) break;
            if (current == goal) return distance[current];
            visited[current] = true;
            foreach (int next in graph.Nodes[current].Neighbors)
            {
                if (visited[next] || !graph.CanUseConnection(current, next, access)) continue;
                float candidate = distance[current] + Vector3.Distance(graph.Nodes[current].Position, graph.Nodes[next].Position);
                if (candidate < distance[next]) distance[next] = candidate;
            }
        }
        return float.PositiveInfinity;
    }

    private static void ValidateConnectionsToGraph(NavigationGraph realGraph, ScenarioReferences scenario)
    {
        GameObject fixture = new GameObject("Validacion conexion a nodos");
        try
        {
            NavigationGraph graph = fixture.AddComponent<NavigationGraph>();
            ConfigureGraph(graph, scenario, realGraph, 4f);
            Vector3 from = DungeonLayout.ToWorld(5.1f, 19.45f, graph.SamplingHeight);
            Vector3 blocked = DungeonLayout.ToWorld(4.5f, 18.5f, graph.SamplingHeight);
            Vector3 reachable = DungeonLayout.ToWorld(7.5f, 20.5f, graph.SamplingHeight);
            Vector3 goal = DungeonLayout.ToWorld(13.5f, 20.5f, graph.SamplingHeight);
            List<NavigationNode> nodes = new List<NavigationNode>
            {
                new NavigationNode(blocked, false), new NavigationNode(reachable, false), new NavigationNode(goal, false)
            };
            Link(nodes, 1, 2);
            graph.ReplaceNodes(nodes);
            Require(Vector3.Distance(from, blocked) < Vector3.Distance(from, reachable), "El fixture necesita un nodo bloqueado mas cercano.");
            Require(!graph.CanTraverse(from, blocked, NavigationAccess.Hunter)
                && graph.CanTraverse(from, reachable, NavigationAccess.Hunter), "El volumen debe bloquear el atajo por la esquina.");
            Require(graph.FindClosestNode(from, NavigationAccess.Hunter) == 1
                && graph.TryFindPath(from, goal, NavigationAccess.Hunter, out List<Vector3> path, out _)
                && !ContainsPoint(path, blocked), "Debe conectar con el nodo accesible, no con el mas cercano tras una pared.");
            ValidateRoute(graph, from, goal, NavigationAccess.Hunter);
            RequireNoRoute(graph, DungeonLayout.ToWorld(5.1f, 17.5f), goal, NavigationAccess.Hunter);
        }
        finally { Object.DestroyImmediate(fixture); }
    }

    private static void ValidateSafeCrossing(NavigationGraph realGraph)
    {
        GameObject fixture = new GameObject("Validacion cruce de refugio");
        try
        {
            ScenarioReferences scenario = fixture.AddComponent<ScenarioReferences>();
            ScenarioZone safe = fixture.AddComponent<ScenarioZone>();
            fixture.transform.position = DungeonLayout.ToWorld(54f, 25f);
            using (SerializedObject data = new SerializedObject(safe))
            {
                data.FindProperty("_type").enumValueIndex = (int)ScenarioZoneType.Safe;
                data.FindProperty("_size").vector2Value = new Vector2(2f, 2f);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            using (SerializedObject data = new SerializedObject(scenario))
            {
                SerializedProperty zones = data.FindProperty("_safeZones");
                zones.arraySize = 1;
                zones.GetArrayElementAtIndex(0).objectReferenceValue = safe;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            NavigationGraph graph = fixture.AddComponent<NavigationGraph>();
            ConfigureGraph(graph, scenario, realGraph, 1.5f);
            Vector3 from = DungeonLayout.ToWorld(51.5f, 25f, graph.SamplingHeight);
            Vector3 to = DungeonLayout.ToWorld(56.5f, 25f, graph.SamplingHeight);
            List<NavigationNode> nodes = new List<NavigationNode> { new NavigationNode(from, false), new NavigationNode(to, false) };
            Link(nodes, 0, 1);
            graph.ReplaceNodes(nodes);
            Require(!safe.Contains(from) && !safe.Contains(to), "Los extremos del fixture deben quedar fuera del refugio.");
            ValidateRoute(graph, from, to, NavigationAccess.Hunter);
            Require(!graph.CanTraverse(from, to, NavigationAccess.Wolf)
                && !graph.CanUseConnection(0, 1, NavigationAccess.Wolf), "Filtrar solo nodos no basta: el segmento cruza el refugio.");
            RequireNoRoute(graph, from, to, NavigationAccess.Wolf);
        }
        finally { Object.DestroyImmediate(fixture); }
    }

    private static void ValidateFollower(NavigationGraph graph, ScenarioReferences scenario)
    {
        GameObject marker = new GameObject("Validacion seguidor temporal");
        GameObject obstacle = null;
        try
        {
            PathFollower follower = marker.AddComponent<PathFollower>();
            using (SerializedObject data = new SerializedObject(follower))
            {
                data.FindProperty("_graph").objectReferenceValue = graph;
                data.FindProperty("_speed").floatValue = 5f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            marker.transform.position = scenario.HunterSpawn.position;
            Require(follower.SetDestination(scenario.HuntingZone.transform.position), "El seguidor no acepta una ruta valida.");
            Vector3 before = marker.transform.position;
            follower.Tick(0f);
            follower.Tick(-1f);
            Require(Near(before, marker.transform.position) && follower.Velocity == Vector3.zero, "Un deltaTime no positivo no debe mover el agente.");
            Require(!follower.SetDestination(DungeonLayout.ToWorld(8f, 17f)) && follower.Path.Count == 0
                && !follower.IsMoving && !follower.HasArrived && !follower.IsBlocked, "Una solicitud fallida debe limpiar la ruta anterior.");
            Require(follower.SetDestination(scenario.HuntingZone.transform.position), "No puede replanificar despues de un fallo.");
            follower.Tick(0.02f);
            before = marker.transform.position;
            follower.Stop();
            follower.Tick(1f);
            Require(Near(before, marker.transform.position) && follower.Path.Count == 0 && follower.PathLength == 0f
                && follower.Velocity == Vector3.zero && !follower.IsMoving, "Stop debe cancelar ruta y movimiento.");

            Vector3 start = DungeonLayout.ToWorld(2.5f, 17.5f, 1.25f);
            Vector3 goal = DungeonLayout.ToWorld(13.5f, 17.5f, 1.25f);
            marker.transform.position = start;
            Require(follower.SetDestination(goal), "Falta la ruta de prueba alrededor de la columna.");
            for (int frame = 0; frame < 2000 && !follower.HasArrived; frame++)
            {
                before = marker.transform.position;
                follower.Tick(0.02f);
                Require(!follower.IsBlocked && Mathf.Abs(marker.transform.position.y - start.y) <= Tolerance,
                    "El seguidor se bloqueo o cambio su altura en una ruta libre.");
                Require(Vector3.Distance(before, marker.transform.position) <= follower.Speed * 0.02f + Tolerance
                    && PhysicalSegmentClear(graph, before, marker.transform.position), "Un paso del seguidor excede velocidad o invade un muro.");
            }
            Require(follower.HasArrived && Near(marker.transform.position, goal) && !follower.IsMoving
                && follower.WaypointIndex == follower.Path.Count
                && Near(follower.Velocity, (marker.transform.position - before) / .02f), "El seguidor no finalizo correctamente.");
            follower.Tick(.02f);
            Require(follower.Velocity == Vector3.zero, "Sin desplazamiento posterior a la llegada la velocidad debe volver a cero.");

            marker.transform.position = start;
            Require(follower.SetDestination(goal), "No puede repetir una ruta terminada.");
            float budget = follower.PathLength * 0.65f;
            Vector3 expected = PointAtDistance(follower.Path, budget);
            expected.y = start.y;
            follower.Tick(budget / follower.Speed);
            Require(Near(marker.transform.position, expected) && !follower.IsBlocked,
                "Un paso largo debe consumir distancia sobre la polilinea, sin cortar esquinas.");
            follower.Tick(100f);
            Require(follower.HasArrived && Near(marker.transform.position, goal), "Un paso largo no debe sobrepasar el destino.");
            Require(follower.SetDestination(marker.transform.position), "El seguidor debe aceptar su posicion como destino.");
            follower.Tick(0.02f);
            Require(follower.HasArrived && follower.Velocity == Vector3.zero, "El destino actual debe completarse sin movimiento.");

            marker.transform.position = scenario.HuntingZone.transform.position + Vector3.up * graph.SamplingHeight;
            Require(follower.SetDestination(scenario.ExitZone.transform.position), "Falta ruta antes de cambiar acceso.");
            follower.Access = NavigationAccess.Wolf;
            Require(follower.Path.Count == 0 && !follower.IsMoving, "Cambiar acceso debe invalidar la ruta anterior.");
            Require(!follower.SetDestination(scenario.SafeZones[0].transform.position), "El seguidor lobo no debe entrar a un refugio.");
            follower.Access = NavigationAccess.Hunter;
            marker.transform.position = DungeonLayout.ToWorld(51.5f, 25.5f, graph.SamplingHeight);
            Require(follower.SetDestination(DungeonLayout.ToWorld(56.5f, 25.5f)), "Falta ruta para probar bloqueo posterior.");
            obstacle = new GameObject("Obstaculo temporal", typeof(BoxCollider));
            obstacle.layer = LayerMask.NameToLayer("MapObstacle");
            obstacle.transform.position = DungeonLayout.ToWorld(54f, 25.5f, 1f);
            obstacle.GetComponent<BoxCollider>().size = new Vector3(0.6f, 2f, 0.6f);
            Physics.SyncTransforms();
            follower.Tick(2f);
            Require(follower.IsBlocked && !follower.HasArrived && !follower.IsMoving
                && follower.Velocity == Vector3.zero, "El seguidor debe detenerse ante un obstaculo aparecido despues del calculo.");
            Require(PhysicalSegmentClear(graph, marker.transform.position, marker.transform.position), "El bloqueo dejo al agente dentro del obstaculo.");
            follower.Graph = null;
            Require(follower.Path.Count == 0 && !follower.SetDestination(goal), "Sin grafo no debe conservar ni aceptar rutas.");
        }
        finally
        {
            if (obstacle != null) Object.DestroyImmediate(obstacle);
            Object.DestroyImmediate(marker);
            Physics.SyncTransforms();
        }
    }

    private static void ValidateMotionContinuity(NavigationGraph graph)
    {
        GameObject actor = new GameObject("Validacion continuidad temporal");
        try
        {
            PathFollower follower = actor.AddComponent<PathFollower>();
            follower.Graph = graph;
            follower.Access = NavigationAccess.Wolf;
            AgentNavigator navigator = actor.AddComponent<AgentNavigator>();
            navigator.Follower = follower;
            Vector3 start = DungeonLayout.ToWorld(51.5f, 25.5f, graph.SamplingHeight);
            Vector3 goal = start + Vector3.right * 4f;
            actor.transform.position = start;
            navigator.NavigateTo(goal, 2.8f, 0f);
            int initialCalculations = navigator.CalculationCount;
            float minimumAdvance = float.PositiveInfinity;
            bool clear = true;
            // A medio tramo cambia el objetivo visible. Al recalcular, el nodo mas
            // cercano queda detras del agente; esa conexion no debe hacerlo retroceder.
            for (int frame = 0; frame < 42; frame++)
            {
                Vector3 before = actor.transform.position;
                navigator.NavigateTo(goal + (frame >= 10 ? Vector3.right * .4f : Vector3.zero), 2.8f, 1f / 60f);
                Vector3 after = actor.transform.position;
                minimumAdvance = Mathf.Min(minimumAdvance, after.x - before.x);
                clear &= graph.CanTraverse(before, after, NavigationAccess.Wolf);
            }
            bool forwardReplan = navigator.CalculationCount > initialCalculations
                && minimumAdvance > 0f && clear;

            navigator.Stop();
            actor.transform.position = start;
            actor.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Vector3 shortGoal = start + Vector3.right * .125f;
            navigator.NavigateTo(shortGoal, 2f, .0625f);
            Vector3 arrivalVelocity = navigator.Velocity;
            float arrivalTurn = Quaternion.Angle(Quaternion.identity, actor.transform.rotation);
            bool arrivalReportsMotion = navigator.HasArrived && arrivalVelocity.x > 1.99f && arrivalTurn > 0f;
            int beforeNextGoal = navigator.CalculationCount;
            navigator.NavigateTo(start + Vector3.right, 2.5f, .02f);
            float nextGoalAdvance = actor.transform.position.x - shortGoal.x;
            bool noArrivalPause = navigator.CalculationCount == beforeNextGoal + 1 && nextGoalAdvance > 0f;

            navigator.Stop();
            actor.transform.position = start;
            Vector3 betweenNodes = start + Vector3.right * 1.6f;
            follower.Speed = 2.5f;
            bool endConnector = follower.SetDestination(betweenNodes);
            for (int frame = 0; frame < 200 && !follower.HasArrived; frame++)
            {
                Vector3 before = actor.transform.position;
                follower.Tick(.02f);
                endConnector &= actor.transform.position.x >= before.x - Tolerance
                    && actor.transform.position.x <= betweenNodes.x + Tolerance;
            }
            endConnector &= follower.HasArrived;

            Debug.Log($"Continuidad: recalculo hacia delante={forwardReplan} (avance minimo={minimumAdvance:F4}); "
                + $"nuevo destino sin pausa={noArrivalPause} (avance={nextGoalAdvance:F4}); "
                + $"velocidad/giro al llegar={arrivalReportsMotion} (vx={arrivalVelocity.x:F3}, giro={arrivalTurn:F2}); "
                + $"conector final sin sobrepaso={endConnector}.");
            ValidateEndpointCorners(graph);
            Require(forwardReplan && noArrivalPause && arrivalReportsMotion && endConnector,
                "Fallo de continuidad: revisar los cuatro resultados del registro anterior.");
        }
        finally { Object.DestroyImmediate(actor); }
    }

    private static void ValidateEndpointCorners(NavigationGraph graph)
    {
        Vector3 from = DungeonLayout.ToWorld(4.5f, 19.1f, graph.SamplingHeight);
        Vector3 corner = DungeonLayout.ToWorld(4.5f, 19.5f, graph.SamplingHeight);
        Vector3 to = DungeonLayout.ToWorld(5.5f, 19.5f, graph.SamplingHeight);
        Require(!graph.CanTraverse(from, to, NavigationAccess.Wolf)
            && graph.CanTraverse(from, corner, NavigationAccess.Wolf)
            && graph.CanTraverse(corner, to, NavigationAccess.Wolf),
            "La fixture debe requerir un conector para rodear la esquina de la columna.");
        Require(graph.TryFindPath(from, to, NavigationAccess.Wolf, out List<Vector3> forward, out _)
            && ContainsPoint(forward, corner), "El conector inicial no puede eliminarse si corta una pared.");
        Require(graph.TryFindPath(to, from, NavigationAccess.Wolf, out List<Vector3> reverse, out _)
            && ContainsPoint(reverse, corner), "El conector final no puede eliminarse si corta una pared.");
        ValidateRoute(graph, from, to, NavigationAccess.Wolf);
        ValidateRoute(graph, to, from, NavigationAccess.Wolf);
    }

    private static void ConfigureGraph(NavigationGraph graph, ScenarioReferences scenario, NavigationGraph source, float connectionDistance)
    {
        using (SerializedObject data = new SerializedObject(graph))
        {
            data.FindProperty("_scenario").objectReferenceValue = scenario;
            data.FindProperty("_obstacleMask").intValue = source.ObstacleMask.value;
            data.FindProperty("_agentRadius").floatValue = source.AgentRadius;
            data.FindProperty("_samplingHeight").floatValue = source.SamplingHeight;
            data.FindProperty("_connectionDistance").floatValue = connectionDistance;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static bool PhysicalSegmentClear(NavigationGraph graph, Vector3 from, Vector3 to)
    {
        from.y = to.y = graph.SamplingHeight;
        if (Physics.CheckSphere(from, graph.AgentRadius, graph.ObstacleMask, QueryTriggerInteraction.Ignore)
            || Physics.CheckSphere(to, graph.AgentRadius, graph.ObstacleMask, QueryTriggerInteraction.Ignore)) return false;
        Vector3 delta = to - from;
        return delta.magnitude <= Tolerance || !Physics.SphereCast(from, graph.AgentRadius, delta.normalized,
            out _, delta.magnitude, graph.ObstacleMask, QueryTriggerInteraction.Ignore);
    }

    private static Vector3 PointAtDistance(IReadOnlyList<Vector3> path, float distance)
    {
        float accumulated = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            float length = Vector3.Distance(path[i - 1], path[i]);
            if (accumulated + length >= distance && length > 0f)
                return Vector3.Lerp(path[i - 1], path[i], (distance - accumulated) / length);
            accumulated += length;
        }
        return path[path.Count - 1];
    }

    private static int ClosestNode(NavigationGraph graph, Vector3 position)
    {
        int closest = -1;
        float best = float.PositiveInfinity;
        position.y = graph.SamplingHeight;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            float distance = (graph.Nodes[i].Position - position).sqrMagnitude;
            if (distance >= best) continue;
            closest = i;
            best = distance;
        }
        Require(closest >= 0, "No hay nodo de referencia para comparar los algoritmos.");
        return closest;
    }

    private static void Link(List<NavigationNode> nodes, int a, int b)
    {
        nodes[a].AddNeighbor(b);
        nodes[b].AddNeighbor(a);
    }

    private static bool HasNeighbor(NavigationNode node, int index)
    {
        foreach (int neighbor in node.Neighbors) if (neighbor == index) return true;
        return false;
    }

    private static bool ContainsPoint(IReadOnlyList<Vector3> path, Vector3 point)
    {
        foreach (Vector3 candidate in path) if (Near(candidate, point)) return true;
        return false;
    }

    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= Tolerance * Tolerance;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Navegacion: " + message);
        _checks++;
    }
}
