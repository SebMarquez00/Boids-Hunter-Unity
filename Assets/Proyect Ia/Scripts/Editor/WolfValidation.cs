using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class WolfValidation
{
    private const float Step = 0.02f;
    private const float Tolerance = 0.01f;
    private static int _checks;

    [MenuItem("Tools/IA/Entrega 2/Validar lobos")]
    public static void Run() => RunValidation(false);

    [MenuItem("Tools/IA/Entrega 2/Validar comunicacion de lobos")]
    public static void RunCommunicationRegression() => RunValidation(true);

    private static void RunValidation(bool communicationOnly)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Esta prueba requiere Edit Mode.");
        Scene original = SceneManager.GetActiveScene();
        Require(original.path == DungeonMapBuilder.ScenePath, "Abri DungeonSimulation antes de validar.");
        NavigationGraph graph = FindSingle<NavigationGraph>(original);
        ScenarioReferences scenario = FindSingle<ScenarioReferences>(original);
        HunterAgent hunter = FindSingle<HunterAgent>(original);
        bool wasDirty = original.isDirty;
        int nodeCount = graph.Nodes.Count;
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        Scene temporary = default;
        _checks = 0;
        try
        {
            if (!communicationOnly) ValidateScene(original, graph, scenario, hunter);
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            Physics.SyncTransforms();
            if (!communicationOnly)
            {
                ValidatePerception(graph);
                ValidateOccludedPatrol(graph, scenario);
                ValidateMemoryAndSafeZone(graph, scenario);
                ValidateStateContinuity(graph, scenario);
                ValidateReacquisition(graph, scenario);
                ValidateCaptureRequest(graph, scenario);
            }
            ValidateCommunication(graph, scenario);
            Require(graph.Nodes.Count == nodeCount && original.isDirty == wasDirty,
                "Las pruebas no deben modificar el grafo ni la escena original.");
            Debug.Log(communicationOnly
                ? $"Comunicacion de lobos: {_checks} comprobaciones correctas; avisos pendientes, vision vigente, cancelacion y ausencia de rebroadcast."
                : $"Lobos: {_checks} comprobaciones correctas; configuracion, FoV/LoS, patrulla A*, " +
                    "alertas snapshot, continuidad de estados, busqueda, refugios y solicitud de captura validados.");
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (temporary.IsValid()) EditorSceneManager.CloseScene(temporary, true);
            UnityEngine.Random.state = randomState;
            Physics.SyncTransforms();
        }
    }

    private static void ValidateScene(Scene scene, NavigationGraph graph, ScenarioReferences scenario, HunterAgent hunter)
    {
        List<WolfAgent> wolves = FindAll<WolfAgent>(scene);
        Require(wolves.Count >= 4, "El escenario debe contener al menos cuatro lobos.");
        WolfPack pack = FindSingle<WolfPack>(scene);
        foreach (WolfAgent wolf in wolves)
        {
            Require(wolf.Hunter == hunter && wolf.Scenario == scenario && wolf.Pack == pack,
                "Las referencias de " + wolf.name + " no corresponden al escenario.");
            Require(wolf.Navigation != null && wolf.Navigation.Follower != null
                && wolf.Navigation.Follower.Graph == graph && wolf.Navigation.Follower.Access == NavigationAccess.Wolf,
                "Cada lobo debe utilizar el grafo compartido con acceso Wolf.");
            Require(wolf.PatrolSpeed > 0f && wolf.PatrolSpeed < hunter.MovementSpeed
                && wolf.ChaseSpeed > 0f && wolf.ChaseSpeed < hunter.MovementSpeed
                && wolf.SearchSpeed > 0f && wolf.SearchSpeed < hunter.MovementSpeed,
                "El lobo debe ser mas lento que el Cazador en todos sus estados.");
            Require(wolf.ViewRadius > 0f && wolf.ViewAngle > 0f && wolf.ViewAngle < 360f && wolf.CaptureRadius > 0f,
                "Los parametros de percepcion y captura deben ser validos.");
            Require(wolf.Waypoints != null && wolf.Waypoints.Count >= 2, "Faltan puntos de patrulla.");
            for (int i = 0; i < wolf.Waypoints.Count; i++)
            {
                Transform from = wolf.Waypoints[i];
                Transform to = wolf.Waypoints[(i + 1) % wolf.Waypoints.Count];
                Require(from != null && to != null && !scenario.ContainsSafeZone(from.position),
                    "Una patrulla contiene puntos nulos o dentro de un refugio.");
                Require(graph.TryFindPath(from.position, to.position, NavigationAccess.Wolf, out List<Vector3> path, out float length)
                    && path.Count > 0 && length > 0f, "Un tramo de patrulla no tiene camino para lobos.");
                Require(PathIsClear(graph, path), "Una patrulla atraviesa una pared o refugio.");
            }
        }
        foreach (ScenarioZone safe in scenario.SafeZones)
        {
            Require(!graph.TryFindPath(hunter.transform.position, safe.transform.position, NavigationAccess.Wolf, out _, out _),
                "Un lobo no debe poder planificar un destino dentro de un refugio.");
        }
    }

    private static void ValidateOccludedPatrol(NavigationGraph graph, ScenarioReferences scenario)
    {
        HunterAgent target = CreateHunter(graph, scenario, Point(graph, 58.5f, 34.5f));
        target.gameObject.SetActive(false);
        WolfPack pack = new GameObject("Manada temporal de patrulla").AddComponent<WolfPack>();
        Vector3 start = Point(graph, 24.5f, 17.5f);
        Vector3 goal = Point(graph, 35.5f, 17.5f);
        WolfAgent wolf = CreateWolf(graph, scenario, target, pack, start, Vector3.right, goal);
        Require(!graph.CanTraverse(start, goal, NavigationAccess.Wolf), "La columna debe ocultar el siguiente waypoint de prueba.");
        bool arrived = false;
        bool usedPath = false;
        for (int frame = 0; frame < 1200 && !arrived; frame++)
        {
            Vector3 previous = wolf.transform.position;
            wolf.UpdateAgent(Step);
            Vector3 current = wolf.transform.position;
            usedPath |= wolf.Navigation.Follower.Path.Count > 2;
            Require(wolf.CurrentStateName == "WolfPatrolState" && !wolf.HasLastKnownPosition
                && Vector3.Distance(previous, current) <= wolf.PatrolSpeed * Step + Tolerance
                && graph.CanTraverse(previous, current, NavigationAccess.Wolf), "La patrulla no rodea la columna de forma valida.");
            arrived = PlanarDistance(current, goal) < 0.1f;
        }
        Require(arrived && usedPath && wolf.Navigation.CalculationCount > 0, "El waypoint oculto debe alcanzarse utilizando A*.");
    }

    private static void ValidatePerception(NavigationGraph graph)
    {
        Vector3 origin = Point(graph, 53f, 27f);
        Vector3 forward = Vector3.right;
        LayerMask mask = graph.ObstacleMask;
        Require(WolfPerception.CanSee(origin, forward, origin + forward * 4.99f, 5f, 90f, mask), "No detecta un objetivo dentro del radio.");
        Require(!WolfPerception.CanSee(origin, forward, origin + forward * 5.01f, 5f, 90f, mask), "Detecta fuera del radio.");
        Require(WolfPerception.CanSee(origin, forward, origin + Quaternion.AngleAxis(44f, Vector3.up) * forward * 3f, 5f, 90f, mask),
            "No detecta dentro del semiangulo de vision.");
        Require(!WolfPerception.CanSee(origin, forward, origin + Quaternion.AngleAxis(46f, Vector3.up) * forward * 3f, 5f, 90f, mask),
            "Detecta fuera del semiangulo de vision.");
        Require(!WolfPerception.CanSee(origin, forward, origin - forward * 2f, 5f, 90f, mask), "Detecta a su espalda.");
        GameObject wall = new GameObject("Validacion temporal LoS", typeof(BoxCollider));
        try
        {
            wall.layer = LayerMask.NameToLayer("MapObstacle");
            wall.transform.position = origin + forward * 2f;
            wall.GetComponent<BoxCollider>().size = new Vector3(0.2f, 2f, 2f);
            Physics.SyncTransforms();
            Require(!WolfPerception.CanSee(origin, forward, origin + forward * 4f, 5f, 90f, mask), "La vision atraviesa una pared.");
            wall.transform.position = origin + forward * 5.5f;
            Physics.SyncTransforms();
            Require(WolfPerception.CanSee(origin, forward, origin + forward * 4f, 5f, 90f, mask),
                "Un obstaculo detras del objetivo no debe ocultarlo.");
        }
        finally { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
    }

    private static void ValidateMemoryAndSafeZone(NavigationGraph graph, ScenarioReferences scenario)
    {
        HunterAgent target = CreateHunter(graph, scenario, Point(graph, 26.5f, 13.5f));
        WolfPack pack = new GameObject("Manada temporal").AddComponent<WolfPack>();
        WolfAgent observer = CreateWolf(graph, scenario, target, pack, Point(graph, 24.5f, 13.5f), Vector3.right);
        WolfAgent receiver = CreateWolf(graph, scenario, target, pack, Point(graph, 35.5f, 17.5f), Vector3.right);
        Vector3 snapshot = target.transform.position;
        Require(observer.CanSeeHunter() && !receiver.CanSeeHunter(), "La fixture requiere un observador y un receptor sin vision.");
        observer.UpdateAgent(Step);
        receiver.UpdateAgent(Step);
        receiver.UpdateAgent(Step);
        Require(observer.HowlCount > 0 && receiver.ReceivedAlertCount > 0,
            "Una deteccion debe emitir un aullido y llegar a los otros lobos.");
        Require(receiver.HowlCount == 0 && !receiver.HasVisualContact && receiver.HasLastKnownPosition
            && Near(receiver.LastKnownPosition, snapshot), "El receptor debe conservar el snapshot sin fingir una deteccion propia.");
        Require(receiver.CurrentStateName == "WolfSearchState" && receiver.Navigation.HasDestination
            && Near(receiver.Navigation.Destination, snapshot) && PathIsClear(graph, receiver.Navigation.Follower.Path),
            "El receptor debe buscar la posicion conocida mediante una ruta valida con A*.");

        int howls = observer.HowlCount;
        int received = receiver.ReceivedAlertCount;
        target.transform.position = Point(graph, 58.5f, 34.5f);
        observer.UpdateAgent(0.2f);
        receiver.UpdateAgent(0.2f);
        Require(!observer.HasVisualContact && observer.HasLastKnownPosition && Near(observer.LastKnownPosition, snapshot),
            "Al perder la vision debe conservarse la ultima posicion vista, no la actual oculta.");
        Require(receiver.ReceivedAlertCount == received && observer.HowlCount == howls
            && Near(receiver.LastKnownPosition, snapshot) && Near(receiver.Navigation.Destination, snapshot),
            "Mover al objetivo oculto no debe actualizar la alerta ni su destino.");

        bool reachedSnapshot = false;
        Vector3 previous = receiver.transform.position;
        for (int frame = 0; frame < 1200 && !reachedSnapshot; frame++)
        {
            receiver.UpdateAgent(Step);
            Vector3 current = receiver.transform.position;
            Require(Vector3.Distance(previous, current) <= receiver.SearchSpeed * Step + Tolerance
                && graph.CanTraverse(previous, current, NavigationAccess.Wolf), "La busqueda excede su velocidad o atraviesa un obstaculo.");
            reachedSnapshot = PlanarDistance(current, snapshot) < 0.6f;
            previous = current;
        }
        Require(reachedSnapshot, "El receptor no llego a la ultima posicion conocida.");
        for (int frame = 0; frame < 150; frame++) receiver.UpdateAgent(Step);
        Require(receiver.CurrentStateName == "WolfSearchState" && receiver.HasLastKnownPosition
            && Near(receiver.LastKnownPosition, snapshot), "Tras llegar debe continuar buscando alrededor de la misma informacion.");

        Vector3 newObservation = observer.transform.position + Vector3.right * 3f;
        target.transform.position = newObservation;
        observer.transform.forward = Vector3.right;
        observer.UpdateAgent(1.6f);
        receiver.UpdateAgent(Step);
        Require(observer.HasVisualContact && observer.HowlCount > howls && receiver.ReceivedAlertCount > received
            && Near(observer.LastKnownPosition, newObservation) && Near(receiver.LastKnownPosition, newObservation),
            "Una nueva observacion valida debe actualizar y retransmitir la posicion conocida.");

        target.transform.position = scenario.SafeZones[0].transform.position;
        observer.UpdateAgent(Step);
        receiver.UpdateAgent(Step);
        Require(!observer.CanSeeHunter() && !receiver.CanSeeHunter()
            && !observer.HasLastKnownPosition && !receiver.HasLastKnownPosition
            && observer.CurrentStateName == "WolfPatrolState" && receiver.CurrentStateName == "WolfPatrolState",
            "El refugio debe cancelar la persecucion y borrar la memoria de toda la manada.");
        Require(!target.RequestCapture(), "El Cazador no puede ser capturado dentro del refugio.");
        pack.ReportSighting(observer, snapshot);
        receiver.UpdateAgent(Step);
        Require(!receiver.HasLastKnownPosition && receiver.CurrentStateName == "WolfPatrolState",
            "Una alerta atrasada no debe reactivar la persecucion mientras el Cazador esta seguro.");
        target.gameObject.SetActive(false);
        Require(!observer.CanSeeHunter(), "Un objetivo inactivo no debe ser detectable.");
    }

    private static void ValidateStateContinuity(NavigationGraph graph, ScenarioReferences scenario)
    {
        Vector3 snapshot = Point(graph, 56.5f, 25.5f);
        HunterAgent target = CreateHunter(graph, scenario, snapshot);
        WolfPack pack = new GameObject("Manada temporal de continuidad").AddComponent<WolfPack>();
        WolfAgent observer = CreateWolf(graph, scenario, target, pack, Point(graph, 52.5f, 25.5f), Vector3.right);
        WolfAgent receiver = CreateWolf(graph, scenario, target, pack, Point(graph, 52.5f, 29.5f), Vector3.left);
        Require(observer.CanSeeHunter() && !receiver.CanSeeHunter(),
            "La continuidad requiere un observador con vision y un receptor fuera de su FoV.");

        Vector3 previous = observer.transform.position;
        observer.UpdateAgent(Step);
        RequireMovedInState(observer, previous, "WolfChaseState", observer.ChaseSpeed, Step,
            "La deteccion debe iniciar Chase y moverse en ese mismo tick.");
        previous = receiver.transform.position;
        receiver.UpdateAgent(Step);
        RequireMovedInState(receiver, previous, "WolfSearchState", receiver.SearchSpeed, Step,
            "Una alerta debe iniciar Search y recorrer el camino en ese mismo tick.");
        Require(!receiver.HasVisualContact && receiver.HasLastKnownPosition && Near(receiver.LastKnownPosition, snapshot),
            "El movimiento inmediato del receptor debe usar el snapshot, sin inventar contacto visual.");

        target.transform.position = Point(graph, 50.5f, 25.5f);
        Require(!observer.CanSeeHunter(), "El objetivo desplazado a la espalda debe quedar fuera del FoV.");
        previous = observer.transform.position;
        observer.UpdateAgent(Step);
        RequireMovedInState(observer, previous, "WolfSearchState", observer.SearchSpeed, Step,
            "Perder vision debe continuar hacia la ultima observacion sin un tick inmovil.");
        Require(!observer.HasVisualContact && Near(observer.LastKnownPosition, snapshot)
            && Near(observer.Navigation.Destination, snapshot),
            "Search no debe adoptar la posicion actual del objetivo oculto.");

        target.transform.position = snapshot;
        previous = observer.transform.position;
        observer.UpdateAgent(0.1f);
        RequireMovedInState(observer, previous, "WolfChaseState", observer.ChaseSpeed, 0.1f,
            "Readquirir vision debe retomar Chase sin un tick inmovil.");

        target.transform.position = scenario.SafeZones[0].transform.position;
        previous = observer.transform.position;
        observer.UpdateAgent(Step);
        RequireMovedInState(observer, previous, "WolfPatrolState", observer.PatrolSpeed, Step,
            "Entrar el objetivo a un refugio debe retomar el patrullaje en ese mismo tick.");
        Require(!observer.HasVisualContact && !observer.HasLastKnownPosition,
            "La continuidad no debe conservar el objetivo ni la memoria al entrar al refugio.");
    }

    private static void RequireMovedInState(WolfAgent wolf, Vector3 previous, string state, float speed,
        float deltaTime, string message)
    {
        float distance = PlanarDistance(previous, wolf.transform.position);
        Require(wolf.CurrentStateName == state && distance > Tolerance && distance <= speed * deltaTime + Tolerance,
            message);
    }

    private static void ValidateReacquisition(NavigationGraph graph, ScenarioReferences scenario)
    {
        Vector3 snapshot = Point(graph, 56.5f, 25.5f);
        HunterAgent target = CreateHunter(graph, scenario, snapshot);
        WolfPack pack = new GameObject("Manada temporal de readquisicion").AddComponent<WolfPack>();
        WolfAgent wolf = CreateWolf(graph, scenario, target, pack, Point(graph, 52.5f, 25.5f), Vector3.right);
        // Esta fixture queda quieta para aislar el cambio de visibilidad y el cooldown.
        using (SerializedObject data = new SerializedObject(wolf))
        {
            data.FindProperty("_patrolSpeed").floatValue = 0f;
            data.FindProperty("_chaseSpeed").floatValue = 0f;
            data.FindProperty("_searchSpeed").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        wolf.UpdateAgent(Step);
        Require(wolf.HasVisualContact && wolf.HowlCount == 1, "La primera deteccion debe aullar una vez.");
        wolf.transform.forward = Vector3.left;
        wolf.UpdateAgent(Step);
        Require(!wolf.HasVisualContact && Near(wolf.LastKnownPosition, snapshot), "Perder vision debe conservar el snapshot.");
        wolf.transform.forward = Vector3.right;
        wolf.UpdateAgent(0.2f);
        Require(wolf.HasVisualContact && wolf.HowlCount == 1,
            "Readquirir al objetivo quieto no debe ignorar el cooldown del aullido anterior.");
        wolf.UpdateAgent(0.3f);
        Require(wolf.HowlCount == 1, "El aullido pendiente no debe emitirse antes de cumplir el intervalo.");
        wolf.UpdateAgent(1f);
        Require(wolf.HowlCount == 2 && Near(wolf.LastKnownPosition, snapshot),
            "Al vencer el cooldown debe anunciar la nueva deteccion aunque el Cazador no se haya movido.");

        target.transform.position = scenario.SafeZones[0].transform.position;
        wolf.UpdateAgent(Step);
        target.transform.position = snapshot;
        wolf.UpdateAgent(0.2f);
        Require(wolf.HasVisualContact && wolf.HowlCount == 2,
            "Entrar y salir de un refugio no debe reiniciar el cooldown ni producir aullidos consecutivos.");
        wolf.UpdateAgent(1.3f);
        Require(wolf.HowlCount == 3, "La readquisicion tras abandonar el refugio debe anunciarse cuando corresponda.");
    }

    private static void ValidateCommunication(NavigationGraph graph, ScenarioReferences scenario)
    {
        List<string> failures = new List<string>();
        RunCommunicationCase("avistamiento breve durante cooldown", () => ValidateBriefSighting(graph, scenario), failures);
        RunCommunicationCase("recepcion con vision cacheada", () => ValidateStaleVisualContact(graph, scenario), failures);
        RunCommunicationCase("snapshot recibido reemplaza aviso pendiente", () => ValidateSupersededSighting(graph, scenario), failures);
        RunCommunicationCase("cancelacion por refugio", () => ValidatePendingCancellation(graph, scenario, true), failures);
        RunCommunicationCase("cancelacion por captura", () => ValidatePendingCancellation(graph, scenario, false), failures);
        RunCommunicationCase("observacion propia todavia visible", () => ValidateCurrentVisualContact(graph, scenario), failures);
        Require(failures.Count == 0, "Regresiones de comunicacion: " + string.Join(" | ", failures));
    }

    private static void RunCommunicationCase(string name, Action test, List<string> failures)
    {
        try
        {
            test();
            Debug.Log("Comunicacion de lobos OK: " + name);
        }
        catch (Exception exception)
        {
            failures.Add(name + ": " + exception.Message);
            Debug.LogWarning("Comunicacion de lobos FALLA: " + name + ": " + exception.Message);
        }
    }

    private static void ValidateBriefSighting(NavigationGraph graph, ScenarioReferences scenario)
    {
        Vector3 first = Point(graph, 56.5f, 25.5f);
        Vector3 brief = Point(graph, 57.5f, 25.5f);
        HunterAgent target = CreateHunter(graph, scenario, first);
        WolfPack pack = new GameObject("Manada temporal de aviso pendiente").AddComponent<WolfPack>();
        WolfAgent observer = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 52.5f, 25.5f), Vector3.right);
        WolfAgent receiver = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 52.5f, 29.5f), Vector3.left);
        observer.UpdateAgent(Step);
        Require(observer.HowlCount == 1 && receiver.ReceivedAlertCount == 1 && Near(receiver.LastKnownPosition, first),
            "La primera observacion debe comunicarse una vez.");
        target.transform.position = brief;
        observer.UpdateAgent(0.2f);
        Require(observer.HasVisualContact && observer.HowlCount == 1 && Near(observer.LastKnownPosition, brief),
            "El nuevo punto debe observarse sin omitir el cooldown.");
        GameObject wall = CreateCommunicationWall(graph);
        try
        {
            observer.UpdateAgent(0.2f);
            Require(!observer.HasVisualContact && !observer.CanSeeHunter(), "La pared debe cortar la vision antes del proximo aullido.");
            target.transform.position = Point(graph, 58.5f, 25.5f);
            observer.UpdateAgent(1.2f);
            receiver.UpdateAgent(Step);
            Require(observer.HowlCount == 2 && receiver.ReceivedAlertCount == 2
                && Near(observer.LastKnownPosition, brief) && Near(receiver.LastKnownPosition, brief),
                "El aviso aplazado debe emitir la ultima posicion realmente vista aunque el objetivo siga oculto.");
            observer.UpdateAgent(3f);
            receiver.UpdateAgent(3f);
            Require(observer.HowlCount == 2 && receiver.HowlCount == 0 && receiver.ReceivedAlertCount == 2,
                "Un snapshot oculto no debe repetirse ni generar rebroadcast del receptor.");
        }
        finally { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
    }

    private static void ValidateStaleVisualContact(NavigationGraph graph, ScenarioReferences scenario)
    {
        Vector3 first = Point(graph, 56.5f, 25.5f);
        Vector3 latest = Point(graph, 57.5f, 26.5f);
        HunterAgent target = CreateHunter(graph, scenario, first);
        WolfPack pack = new GameObject("Manada temporal de vision cacheada").AddComponent<WolfPack>();
        WolfAgent receiver = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 52.5f, 25.5f), Vector3.right);
        WolfAgent observer = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 58.5f, 29.5f), Vector3.back);
        receiver.UpdateAgent(Step);
        target.transform.position = latest;
        GameObject wall = CreateCommunicationWall(graph);
        try
        {
            Require(receiver.HasVisualContact && !receiver.CanSeeHunter() && observer.CanSeeHunter(),
                "La fixture debe reproducir la alerta antes del Update del receptor que perdio LoS.");
            observer.UpdateAgent(Step);
            Require(receiver.ReceivedAlertCount == 1 && !receiver.HasVisualContact && Near(receiver.LastKnownPosition, latest),
                "ReceiveAlert debe revalidar LoS y aceptar el snapshot nuevo en ese mismo evento.");
            receiver.UpdateAgent(Step);
            Require(receiver.CurrentStateName == "WolfSearchState" && Near(receiver.Navigation.Destination, latest),
                "El siguiente Update debe buscar la informacion recibida y no el punto visible del frame anterior.");
        }
        finally { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
    }

    private static void ValidateSupersededSighting(NavigationGraph graph, ScenarioReferences scenario)
    {
        HunterAgent target = CreateHunter(graph, scenario, Point(graph, 56.5f, 25.5f));
        WolfPack pack = new GameObject("Manada temporal de snapshot reemplazado").AddComponent<WolfPack>();
        WolfAgent receiver = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 52.5f, 25.5f), Vector3.right);
        WolfAgent observer = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 58.5f, 29.5f), Vector3.back);
        receiver.UpdateAgent(Step);
        target.transform.position = Point(graph, 57.5f, 25.5f);
        receiver.UpdateAgent(0.2f);
        Require(receiver.HowlCount == 1 && receiver.HasVisualContact, "Debe existir un aviso propio aplazado por cooldown.");
        GameObject wall = CreateCommunicationWall(graph);
        try
        {
            receiver.UpdateAgent(0.2f);
            Vector3 latest = Point(graph, 57.5f, 26.5f);
            target.transform.position = latest;
            observer.UpdateAgent(Step);
            Require(!receiver.HasVisualContact && Near(receiver.LastKnownPosition, latest),
                "El receptor sin vision debe adoptar el snapshot nuevo del otro lobo.");
            observer.transform.forward = Vector3.forward;
            for (int i = 0; i < 3; i++)
            {
                receiver.UpdateAgent(2f);
                observer.UpdateAgent(2f);
            }
            Require(receiver.HowlCount == 1 && observer.HowlCount == 1
                && receiver.ReceivedAlertCount == 1 && observer.ReceivedAlertCount == 1
                && Near(receiver.LastKnownPosition, latest) && Near(observer.LastKnownPosition, latest),
                "El aviso propio anterior debe cancelarse: ni sobrescribir la memoria nueva ni reenviar alertas recibidas.");
        }
        finally { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
    }

    private static void ValidatePendingCancellation(NavigationGraph graph, ScenarioReferences scenario, bool refuge)
    {
        HunterAgent target = CreateHunter(graph, scenario, Point(graph, 56.5f, 25.5f));
        WolfPack pack = new GameObject("Manada temporal de cancelacion").AddComponent<WolfPack>();
        WolfAgent wolf = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 52.5f, 25.5f), Vector3.right);
        wolf.UpdateAgent(Step);
        target.transform.position = Point(graph, 57.5f, 25.5f);
        wolf.UpdateAgent(0.2f);
        Require(wolf.HowlCount == 1 && wolf.HasVisualContact, "La cancelacion requiere un avistamiento pendiente.");
        if (refuge) target.transform.position = scenario.SafeZones[0].transform.position;
        else Require(target.RequestCapture(), "La fixture debe poder solicitar una captura real sin forzar la FSM.");
        wolf.UpdateAgent(2f);
        Require(wolf.HowlCount == 1 && !wolf.HasLastKnownPosition && !wolf.HasVisualContact
            && wolf.CurrentStateName == "WolfPatrolState", "Refugio o captura deben borrar memoria y cancelar el aullido pendiente.");
        if (refuge)
        {
            target.transform.position = Point(graph, 58.5f, 25.5f);
            wolf.transform.forward = Vector3.left;
        }
        wolf.UpdateAgent(2f);
        Require(wolf.HowlCount == 1 && !wolf.HasLastKnownPosition,
            "El aviso cancelado no debe reaparecer mas tarde sin un nuevo avistamiento.");
    }

    private static void ValidateCurrentVisualContact(NavigationGraph graph, ScenarioReferences scenario)
    {
        Vector3 first = Point(graph, 56.5f, 25.5f);
        HunterAgent target = CreateHunter(graph, scenario, first);
        WolfPack pack = new GameObject("Manada temporal de vision vigente").AddComponent<WolfPack>();
        WolfAgent receiver = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 52.5f, 25.5f), Vector3.right);
        WolfAgent observer = CreateStationaryWolf(graph, scenario, target, pack, Point(graph, 58.5f, 29.5f), Vector3.back);
        receiver.UpdateAgent(Step);
        target.transform.position = Point(graph, 57.5f, 26.5f);
        Require(receiver.CanSeeHunter() && observer.CanSeeHunter(), "Ambos lobos deben conservar vision real en esta fixture.");
        observer.UpdateAgent(Step);
        Require(receiver.HasVisualContact && Near(receiver.LastKnownPosition, first),
            "Una alerta no debe sustituir la observacion propia mientras la vision realmente siga vigente.");
        receiver.UpdateAgent(Step);
        Require(receiver.HasVisualContact && Near(receiver.LastKnownPosition, target.transform.position),
            "El sensor propio debe seguir actualizando al objetivo visible normalmente.");
    }

    private static WolfAgent CreateStationaryWolf(NavigationGraph graph, ScenarioReferences scenario, HunterAgent hunter,
        WolfPack pack, Vector3 position, Vector3 forward)
    {
        WolfAgent wolf = CreateWolf(graph, scenario, hunter, pack, position, forward);
        using (SerializedObject data = new SerializedObject(wolf))
        {
            data.FindProperty("_patrolSpeed").floatValue = 0f;
            data.FindProperty("_chaseSpeed").floatValue = 0f;
            data.FindProperty("_searchSpeed").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        return wolf;
    }

    private static GameObject CreateCommunicationWall(NavigationGraph graph)
    {
        GameObject wall = new GameObject("Pared temporal de comunicacion", typeof(BoxCollider));
        wall.layer = LayerMask.NameToLayer("MapObstacle");
        wall.transform.position = Point(graph, 54.5f, 25.5f);
        wall.GetComponent<BoxCollider>().size = new Vector3(0.2f, 2f, 4f);
        Physics.SyncTransforms();
        return wall;
    }

    private static void ValidateCaptureRequest(NavigationGraph graph, ScenarioReferences scenario)
    {
        HunterAgent target = CreateHunter(graph, scenario, Point(graph, 55.5f, 25.5f));
        WolfPack pack = new GameObject("Manada temporal de captura").AddComponent<WolfPack>();
        WolfAgent wolf = CreateWolf(graph, scenario, target, pack, target.transform.position - Vector3.right * 0.7f, Vector3.right);
        Require(wolf.CanSeeHunter(), "Falta vision en la fixture de captura.");
        GameObject wall = new GameObject("Pared temporal entre agentes cercanos", typeof(BoxCollider));
        try
        {
            wall.layer = LayerMask.NameToLayer("MapObstacle");
            wall.transform.position = (target.transform.position + wolf.transform.position) * 0.5f;
            wall.GetComponent<BoxCollider>().size = new Vector3(0.1f, 2f, 1f);
            Physics.SyncTransforms();
            wolf.UpdateAgent(Step);
            Require(wolf.CaptureCount == 0 && target.IsAvailableToWolves && !wolf.CanSeeHunter(),
                "La proximidad no permite capturar a traves de una pared.");
        }
        finally { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }
        wolf.UpdateAgent(0.2f);
        wolf.UpdateAgent(Step);
        Require(wolf.CaptureCount == 1 && !target.IsAvailableToWolves,
            "Al alcanzar el radio debe solicitarse una captura y quedar pendiente de la FSM del Cazador.");
        wolf.UpdateAgent(Step);
        Require(wolf.CaptureCount == 1 && !target.RequestCapture(), "Las capturas repetidas no deben duplicar la muerte.");
        // La transicion Dead y sus cinco segundos se observan con los actores reales en Play.
    }

    private static HunterAgent CreateHunter(NavigationGraph graph, ScenarioReferences scenario, Vector3 position)
    {
        GameObject actor = new GameObject("Cazador temporal de validacion");
        actor.transform.position = position;
        AgentNavigator navigator = AddNavigator(actor, graph, NavigationAccess.Hunter);
        HunterAgent hunter = actor.AddComponent<HunterAgent>();
        using (SerializedObject data = new SerializedObject(hunter))
        {
            data.FindProperty("_navigation").objectReferenceValue = navigator;
            data.FindProperty("_scenario").objectReferenceValue = scenario;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        return hunter;
    }

    private static WolfAgent CreateWolf(NavigationGraph graph, ScenarioReferences scenario, HunterAgent hunter,
        WolfPack pack, Vector3 position, Vector3 forward, Vector3? patrolDestination = null)
    {
        GameObject actor = new GameObject("Lobo temporal de validacion");
        actor.transform.position = position;
        actor.transform.forward = forward;
        AgentNavigator navigator = AddNavigator(actor, graph, NavigationAccess.Wolf);
        WolfAgent wolf = actor.AddComponent<WolfAgent>();
        using (SerializedObject data = new SerializedObject(wolf))
        {
            data.FindProperty("_navigation").objectReferenceValue = navigator;
            data.FindProperty("_hunter").objectReferenceValue = hunter;
            data.FindProperty("_scenario").objectReferenceValue = scenario;
            data.FindProperty("_pack").objectReferenceValue = pack;
            SerializedProperty waypoints = data.FindProperty("_waypoints");
            waypoints.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                Transform point = new GameObject("Waypoint temporal " + i).transform;
                point.position = i == 1 && patrolDestination.HasValue ? patrolDestination.Value : position + Vector3.forward * i;
                waypoints.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        wolf.Initialize();
        return wolf;
    }

    private static AgentNavigator AddNavigator(GameObject actor, NavigationGraph graph, NavigationAccess access)
    {
        PathFollower follower = actor.AddComponent<PathFollower>();
        follower.Graph = graph;
        follower.Access = access;
        AgentNavigator navigator = actor.AddComponent<AgentNavigator>();
        navigator.Follower = follower;
        return navigator;
    }

    private static bool PathIsClear(NavigationGraph graph, IReadOnlyList<Vector3> path)
    {
        if (path == null || path.Count == 0) return false;
        for (int i = 1; i < path.Count; i++)
            if (!graph.CanTraverse(path[i - 1], path[i], NavigationAccess.Wolf)) return false;
        return true;
    }

    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        List<T> found = FindAll<T>(scene);
        Require(found.Count == 1, "Debe haber exactamente un " + typeof(T).Name + " en la escena.");
        return found[0];
    }

    private static Vector3 Point(NavigationGraph graph, float x, float z) => DungeonLayout.ToWorld(x, z, graph.SamplingHeight);
    private static float PlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static bool Near(Vector3 a, Vector3 b) => PlanarDistance(a, b) <= Tolerance;
    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
