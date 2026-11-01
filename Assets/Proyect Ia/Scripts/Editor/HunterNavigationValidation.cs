using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HunterNavigationValidation
{
    private const float Step = 0.02f;
    private const float Speed = 5f;
    private const float RepathInterval = 0.5f;
    private const float Tolerance = 0.003f;
    private static int _checks;

    [MenuItem("Tools/IA/Entrega 2/Validar navegacion del cazador")]
    public static void Run()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Esta prueba requiere Edit Mode.");
        Scene originalScene = SceneManager.GetActiveScene();
        Require(originalScene.path == DungeonMapBuilder.ScenePath, "Abri DungeonSimulation antes de validar.");
        NavigationGraph graph = FindGraph(originalScene);
        int originalNodes = graph.Nodes.Count;
        int originalEdges = CountEdges(graph);
        bool originalDirty = originalScene.isDirty;
        Scene temporaryScene = default;
        _checks = 0;
        try
        {
            temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporaryScene);
            Physics.SyncTransforms();
            ValidateAlternativeRoute(graph);
            ValidateWaitingAndRecovery(graph);
            ValidateDestinationChanges(graph);
            ValidateStopWhileWaiting(graph);
            ValidateNavigationTurning(graph);
            ValidateDirectTurning();
            Require(graph.Nodes.Count == originalNodes && CountEdges(graph) == originalEdges,
                "Recalcular no debe reconstruir ni modificar la topologia guardada.");
            Require(originalScene.isDirty == originalDirty, "Las pruebas no deben modificar la escena original.");
            Debug.Log($"Navegacion del Cazador: {_checks} comprobaciones correctas; " +
                "rodeo, espera, reintento, cambio de destino, cancelacion y giro gradual validados.");
        }
        finally
        {
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            if (temporaryScene.IsValid()) EditorSceneManager.CloseScene(temporaryScene, true);
            Physics.SyncTransforms();
        }
    }

    private static void ValidateAlternativeRoute(NavigationGraph graph)
    {
        GameObject actor = null;
        GameObject barrier = null;
        try
        {
            AgentNavigator navigator = CreateNavigator(graph, out actor);
            actor.transform.position = Point(graph, 24.5f, 13.5f);
            Vector3 goal = Point(graph, 51.5f, 13.5f);
            navigator.NavigateTo(goal, Speed, 0f);
            Require(!navigator.WaitingForPath && navigator.Follower.Path.Count > 0, "Falta la ruta inicial de Sala B al Coto.");
            List<Vector3> oldPath = new List<Vector3>(navigator.Follower.Path);
            int calculations = navigator.CalculationCount;
            barrier = CreateBarrier(30f, 13.5f, 1f, 3f);
            Require(!PathIsClear(graph, oldPath), "La barrera de prueba debe cortar la ruta calculada previamente.");

            bool replacedPath = false;
            int checkedRevision = navigator.Revision;
            for (int frame = 0; frame < 2500 && !navigator.HasArrived; frame++)
            {
                AdvanceAndCheck(graph, navigator, goal);
                if (navigator.Revision == checkedRevision) continue;
                checkedRevision = navigator.Revision;
                if (navigator.WaitingForPath) continue;
                Require(PathIsClear(graph, navigator.Follower.Path), "A* volvio a elegir aristas bloqueadas por la barrera.");
                replacedPath = true;
            }
            Require(replacedPath && navigator.CalculationCount > calculations,
                "El bloqueo debe provocar un nuevo calculo y una ruta alternativa.");
            Require(navigator.HasArrived && Near(actor.transform.position, goal), "El Cazador no completo el rodeo de Sala B.");
        }
        finally { DestroyFixture(actor, barrier); }
    }

    private static void ValidateWaitingAndRecovery(NavigationGraph graph)
    {
        GameObject actor = null;
        GameObject barrier = null;
        try
        {
            AgentNavigator navigator = CreateNavigator(graph, out actor);
            Vector3 start = Point(graph, 6.5f, 6.5f);
            Vector3 goal = Point(graph, 54.5f, 25.5f);
            actor.transform.position = start;
            barrier = CreateBarrier(6.5f, 10.5f, 3f, 1f);
            navigator.NavigateTo(goal, Speed, 0f);
            Require(navigator.HasDestination && navigator.WaitingForPath && !navigator.HasArrived,
                "Con el unico acceso cerrado debe conservar el objetivo y esperar.");
            int initialCalculations = navigator.CalculationCount;
            for (int frame = 0; frame < 60; frame++)
            {
                navigator.NavigateTo(goal, Speed, Step);
                Require(navigator.WaitingForPath && !navigator.HasArrived && navigator.Velocity == Vector3.zero
                    && Near(actor.transform.position, start), "Sin ruta no debe avanzar ni informar llegada.");
            }
            int retries = navigator.CalculationCount - initialCalculations;
            Require(retries >= 1 && retries <= 3, "Los reintentos deben respetar el intervalo de medio segundo, no ocurrir cada frame.");
            int beforeOpening = navigator.CalculationCount;
            Object.DestroyImmediate(barrier);
            barrier = null;
            Physics.SyncTransforms();
            DriveUntilArrived(graph, navigator, goal);
            Require(navigator.CalculationCount > beforeOpening && navigator.HasArrived && Near(actor.transform.position, goal),
                "Al abrir el pasillo debe recalcular y retomar el mismo objetivo.");
        }
        finally { DestroyFixture(actor, barrier); }
    }

    private static void ValidateDestinationChanges(NavigationGraph graph)
    {
        GameObject actor = null;
        try
        {
            AgentNavigator navigator = CreateNavigator(graph, out actor);
            actor.transform.position = Point(graph, 49.5f, 25.5f);
            Vector3 oldGoal = Point(graph, 59.5f, 25.5f);
            Vector3 newGoal = Point(graph, 49.5f, 34.5f);
            navigator.NavigateTo(oldGoal, Speed, 0f);
            for (int frame = 0; frame < 5; frame++) AdvanceAndCheck(graph, navigator, oldGoal);
            int beforeChange = navigator.CalculationCount;
            for (int frame = 0; frame < 30; frame++)
            {
                AdvanceAndCheck(graph, navigator, newGoal);
                Require(!navigator.HasArrived || Near(actor.transform.position, newGoal), "No debe informar llegada al destino obsoleto.");
            }
            Require(Near(navigator.Destination, newGoal) && navigator.CalculationCount > beforeChange
                && EndsAt(navigator.Follower.Path, newGoal), "El cambio de destino debe reemplazar la ruta dentro del intervalo de recalculo.");
            DriveUntilArrived(graph, navigator, newGoal);

            // Menor que el umbral de seguimiento: al estar detenido igual debe llegar al punto nuevo.
            Vector3 smallChange = newGoal + Vector3.right * 0.1f;
            int beforeSmallChange = navigator.CalculationCount;
            for (int frame = 0; frame < 60; frame++)
            {
                AdvanceAndCheck(graph, navigator, smallChange);
                if (navigator.HasArrived) break;
            }
            Require(navigator.CalculationCount > beforeSmallChange && navigator.HasArrived && Near(actor.transform.position, smallChange),
                "Un cambio de 0.1 m al arribar no debe dejar al agente detenido en el objetivo anterior.");
        }
        finally { DestroyFixture(actor, null); }
    }

    private static void ValidateStopWhileWaiting(NavigationGraph graph)
    {
        GameObject actor = null;
        GameObject barrier = null;
        try
        {
            AgentNavigator navigator = CreateNavigator(graph, out actor);
            actor.transform.position = Point(graph, 6.5f, 6.5f);
            Vector3 start = actor.transform.position;
            barrier = CreateBarrier(6.5f, 10.5f, 3f, 1f);
            navigator.NavigateTo(Point(graph, 54.5f, 25.5f), Speed, 0f);
            Require(navigator.WaitingForPath, "El fixture debe iniciar esperando un camino.");
            navigator.Stop();
            int calculations = navigator.CalculationCount;
            Object.DestroyImmediate(barrier);
            barrier = null;
            Physics.SyncTransforms();
            // No se envia otra NavigateTo: eso seria una nueva orden de la FSM.
            for (int frame = 0; frame < 60; frame++) navigator.Follower.Tick(Step);
            Require(!navigator.HasDestination && !navigator.WaitingForPath && !navigator.HasArrived
                && navigator.Follower.Path.Count == 0 && navigator.CalculationCount == calculations
                && navigator.Velocity == Vector3.zero && Near(actor.transform.position, start),
                "Stop debe cancelar el destino y evitar que reaparezca un movimiento anterior.");
        }
        finally { DestroyFixture(actor, barrier); }
    }

    private static void ValidateNavigationTurning(NavigationGraph graph)
    {
        GameObject actor30 = null;
        GameObject actor60 = null;
        try
        {
            AgentNavigator navigator30 = CreateNavigator(graph, out actor30, 90f);
            AgentNavigator navigator60 = CreateNavigator(graph, out actor60, 90f);
            Vector3 start = Point(graph, 49.5f, 25.5f);
            Vector3 goal = Point(graph, 59.5f, 25.5f);
            actor30.transform.position = actor60.transform.position = start;
            actor30.transform.rotation = actor60.transform.rotation = Quaternion.identity;

            navigator30.NavigateTo(goal, Speed, 1f / 30f);
            float firstTurn = Quaternion.Angle(Quaternion.identity, actor30.transform.rotation);
            Require(firstTurn > 0f && firstTurn <= navigator30.TurnSpeed / 30f + 0.05f,
                "El giro de navegacion debe ser gradual y respetar los grados por segundo configurados.");
            for (int frame = 1; frame < 15; frame++) navigator30.NavigateTo(goal, Speed, 1f / 30f);
            for (int frame = 0; frame < 30; frame++) navigator60.NavigateTo(goal, Speed, 1f / 60f);
            Require(Quaternion.Angle(actor30.transform.rotation, actor60.transform.rotation) <= 0.05f
                && Mathf.Abs(Quaternion.Angle(Quaternion.identity, actor30.transform.rotation) - 45f) <= 0.05f
                && Near(actor30.transform.position, actor60.transform.position)
                && Near(actor30.transform.position, start + Vector3.right * Speed * 0.5f),
                "A 30 y 60 fps la navegacion debe girar los mismos 45 grados sin cambiar el desplazamiento.");

            Vector3 position = actor30.transform.position;
            Quaternion rotation = actor30.transform.rotation;
            navigator30.NavigateTo(goal, Speed, 0f);
            Require(Near(actor30.transform.position, position)
                && Quaternion.Angle(rotation, actor30.transform.rotation) <= 0.05f
                && navigator30.Velocity == Vector3.zero,
                "Con deltaTime cero la navegacion no debe mover ni girar al Cazador.");
        }
        finally { DestroyFixture(actor30, actor60); }
    }

    private static void ValidateDirectTurning()
    {
        GameObject actor30 = null;
        GameObject actor60 = null;
        try
        {
            actor30 = new GameObject("Validacion temporal de giro directo a 30 fps");
            actor60 = new GameObject("Validacion temporal de giro directo a 60 fps");
            HunterMovement movement30 = new HunterMovement(actor30.transform);
            HunterMovement movement60 = new HunterMovement(actor60.transform);
            Vector3 goal = Vector3.right * 100f;
            const float turnSpeed = 90f;

            movement30.MoveTowards(goal, Speed, 1f / 30f, turnSpeed);
            float firstTurn = Quaternion.Angle(Quaternion.identity, actor30.transform.rotation);
            Require(firstTurn > 0f && firstTurn <= turnSpeed / 30f + 0.05f,
                "El tramo directo debe respetar el mismo limite angular que la navegacion.");
            for (int frame = 1; frame < 15; frame++) movement30.MoveTowards(goal, Speed, 1f / 30f, turnSpeed);
            for (int frame = 0; frame < 30; frame++) movement60.MoveTowards(goal, Speed, 1f / 60f, turnSpeed);
            Require(Quaternion.Angle(actor30.transform.rotation, actor60.transform.rotation) <= 0.05f
                && Mathf.Abs(Quaternion.Angle(Quaternion.identity, actor30.transform.rotation) - 45f) <= 0.05f
                && Near(actor30.transform.position, actor60.transform.position)
                && Near(actor30.transform.position, Vector3.right * Speed * 0.5f),
                "El tramo directo debe mantener el mismo giro y desplazamiento a 30 y 60 fps.");

            Vector3 position = actor30.transform.position;
            Quaternion rotation = actor30.transform.rotation;
            Vector3 velocity = movement30.MoveTowards(-goal, Speed, 0f, turnSpeed);
            Require(Near(actor30.transform.position, position)
                && Quaternion.Angle(rotation, actor30.transform.rotation) <= 0.05f && velocity == Vector3.zero,
                "Con deltaTime cero el tramo directo no debe mover ni girar al Cazador.");

            actor30.transform.rotation = Quaternion.identity;
            movement30.MoveTowards(goal, Speed, Step);
            Require(Vector3.Angle(actor30.transform.forward, Vector3.right) <= 0.05f,
                "La llamada original sin velocidad angular debe conservar el giro inmediato de P1.");
        }
        finally { DestroyFixture(actor30, actor60); }
    }

    private static AgentNavigator CreateNavigator(NavigationGraph graph, out GameObject actor, float turnSpeed = 360f)
    {
        actor = new GameObject("Validacion temporal del Cazador");
        PathFollower follower = actor.AddComponent<PathFollower>();
        using (SerializedObject data = new SerializedObject(follower))
        {
            data.FindProperty("_graph").objectReferenceValue = graph;
            data.FindProperty("_access").enumValueIndex = (int)NavigationAccess.Hunter;
            data.FindProperty("_speed").floatValue = Speed;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AgentNavigator navigator = actor.AddComponent<AgentNavigator>();
        using (SerializedObject data = new SerializedObject(navigator))
        {
            data.FindProperty("_follower").objectReferenceValue = follower;
            data.FindProperty("_repathInterval").floatValue = RepathInterval;
            data.FindProperty("_destinationThreshold").floatValue = 0.25f;
            data.FindProperty("_turnSpeed").floatValue = turnSpeed;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        return navigator;
    }

    private static GameObject CreateBarrier(float x, float z, float width, float depth)
    {
        int layer = LayerMask.NameToLayer("MapObstacle");
        Require(layer >= 0, "Falta la capa MapObstacle.");
        GameObject barrier = new GameObject("Barrera temporal de validacion", typeof(BoxCollider));
        barrier.layer = layer;
        barrier.transform.position = DungeonLayout.ToWorld(x, z, 1.2f);
        barrier.GetComponent<BoxCollider>().size = new Vector3(width, 2.4f, depth);
        Physics.SyncTransforms();
        return barrier;
    }

    private static void DriveUntilArrived(NavigationGraph graph, AgentNavigator navigator, Vector3 goal)
    {
        for (int frame = 0; frame < 3000 && !navigator.HasArrived; frame++) AdvanceAndCheck(graph, navigator, goal);
        Require(navigator.HasArrived && Near(navigator.transform.position, goal), "El agente no llego al destino antes del limite de simulacion.");
    }

    private static void AdvanceAndCheck(NavigationGraph graph, AgentNavigator navigator, Vector3 goal)
    {
        Vector3 before = navigator.transform.position;
        navigator.NavigateTo(goal, Speed, Step);
        Vector3 after = navigator.transform.position;
        Require(Mathf.Abs(before.y - after.y) <= Tolerance
            && Vector3.Distance(before, after) <= Speed * Step + Tolerance && SegmentIsClear(graph, before, after),
            "El movimiento cambio la altura, excedio la velocidad o atraveso un obstaculo.");
        Require(!navigator.WaitingForPath || navigator.Velocity == Vector3.zero, "La espera debe informar velocidad cero.");
    }

    private static bool PathIsClear(NavigationGraph graph, IReadOnlyList<Vector3> path)
    {
        if (path == null || path.Count == 0) return false;
        if (!SegmentIsClear(graph, path[0], path[0])) return false;
        for (int i = 1; i < path.Count; i++)
            if (!SegmentIsClear(graph, path[i - 1], path[i])) return false;
        return true;
    }

    private static bool SegmentIsClear(NavigationGraph graph, Vector3 from, Vector3 to)
    {
        from.y = to.y = graph.SamplingHeight;
        if (Physics.CheckSphere(from, graph.AgentRadius, graph.ObstacleMask, QueryTriggerInteraction.Ignore)
            || Physics.CheckSphere(to, graph.AgentRadius, graph.ObstacleMask, QueryTriggerInteraction.Ignore)) return false;
        Vector3 delta = to - from;
        return delta.magnitude <= Tolerance || !Physics.SphereCast(from, graph.AgentRadius, delta.normalized,
            out _, delta.magnitude, graph.ObstacleMask, QueryTriggerInteraction.Ignore);
    }

    private static NavigationGraph FindGraph(Scene scene)
    {
        NavigationGraph graph = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (NavigationGraph candidate in root.GetComponentsInChildren<NavigationGraph>(true))
            {
                Require(graph == null, "La escena contiene mas de un grafo.");
                graph = candidate;
            }
        Require(graph != null && graph.Nodes.Count > 0, "Falta el grafo de navegacion generado.");
        return graph;
    }

    private static int CountEdges(NavigationGraph graph)
    {
        int count = 0;
        foreach (NavigationNode node in graph.Nodes) count += node.Neighbors.Count;
        return count;
    }

    private static void DestroyFixture(GameObject actor, GameObject barrier)
    {
        if (barrier != null) Object.DestroyImmediate(barrier);
        if (actor != null) Object.DestroyImmediate(actor);
        Physics.SyncTransforms();
    }

    private static Vector3 Point(NavigationGraph graph, float x, float z) => DungeonLayout.ToWorld(x, z, graph.SamplingHeight);
    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= Tolerance * Tolerance;
    private static bool EndsAt(IReadOnlyList<Vector3> path, Vector3 goal) => path.Count > 0 && Near(path[path.Count - 1], goal);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Navegacion del Cazador: " + message);
        _checks++;
    }
}
