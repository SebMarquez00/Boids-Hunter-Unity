using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HunterCycleValidation
{
    private const int TestLayer = 31;
    private const int TestMask = 1 << TestLayer;
    private const float Step = 0.1f;
    private static int _checks;

    [MenuItem("Tools/IA/Entrega 2/Validar ciclo del cazador")]
    public static void Run()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Esta prueba requiere Edit Mode.");
        Scene original = SceneManager.GetActiveScene();
        Check(original.path == DungeonMapBuilder.ScenePath, "Abri DungeonSimulation antes de validar.");
        NavigationGraph graph = FindSingle<NavigationGraph>(original);
        ScenarioReferences scenario = FindSingle<ScenarioReferences>(original);
        bool dirty = original.isDirty;
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        Scene temporary = default;
        _checks = 0;
        try
        {
            temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temporary);
            ValidateWolfPerception(graph, scenario);
            ValidateSafeSelectionAndRetry(graph, scenario);
            ValidatePrioritiesAndDeath(graph, scenario);
            ValidateSuccessfulAttackReturn(graph, scenario);
            ValidateInterestBeforeGather(graph, scenario);
            ValidateUnavailableGather(graph, scenario);
            ValidateSafeWait(graph, scenario, false);
            ValidateSafeWait(graph, scenario, true);
            ValidateExitAndRestart(graph, scenario);
            Check(original.isDirty == dirty, "La validacion no debe modificar la escena original.");
            Debug.Log($"Ciclo del Cazador: {_checks} comprobaciones correctas; percepcion, refugio por A*, " +
                "bloqueos, prioridades, cancelacion de Gather, espera, Dead, Exit y reinicio validados.");
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (temporary.IsValid()) EditorSceneManager.CloseScene(temporary, true);
            UnityEngine.Random.state = randomState;
            Physics.SyncTransforms();
        }
    }

    private static void ValidateWolfPerception(NavigationGraph graph, ScenarioReferences scenario)
    {
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 55.5f, 25.5f)))
        {
            HunterPerception sensor = new HunterPerception();
            Vector3 origin = f.Hunter.transform.position;
            WolfAgent wolf = f.Wolf(origin - Vector3.forward * 3f);
            Physics.SyncTransforms();
            sensor.Update(origin, 7f, 1f, TestMask, 10f, 0f);
            Check(sensor.FindClosestWolf(origin, 7f, graph.ObstacleMask) == wolf,
                "El sensor local debe detectar un lobo detras del Cazador: no requiere angulo.");
            GameObject wall = f.Barrier(origin - Vector3.forward * 1.5f, new Vector3(2f, 2f, 0.1f));
            Check(sensor.FindClosestWolf(origin, 7f, graph.ObstacleMask) == null,
                "Una pared nueva debe invalidar una deteccion cacheada sin esperar otro OverlapSphere.");
            wall.SetActive(false);
            Physics.SyncTransforms();
            Check(sensor.FindClosestWolf(origin, 7f, graph.ObstacleMask) == wolf, "Retirar la pared debe restaurar la LoS.");
            wolf.transform.position = origin - Vector3.forward * 7.1f;
            Check(sensor.FindClosestWolf(origin, 7f, graph.ObstacleMask) == null, "Debe revalidar el radio del lobo cacheado.");
            wolf.transform.position = origin - Vector3.forward * 3f;
            wolf.enabled = false;
            Check(sensor.FindClosestWolf(origin, 7f, graph.ObstacleMask) == null, "Un lobo deshabilitado no debe activar Flee.");
            GameObject unsensed = f.Create("Lobo sin collider", origin + Vector3.right);
            unsensed.AddComponent<WolfAgent>();
            Physics.SyncTransforms();
            sensor.Update(origin, 7f, 1f, TestMask, 10f, 10f);
            Check(sensor.FindClosestWolf(origin, 7f, graph.ObstacleMask) == null,
                "La deteccion debe provenir del sensor fisico local, no de un registro global de lobos.");
        }
    }

    private static void ValidateSafeSelectionAndRetry(NavigationGraph graph, ScenarioReferences scenario)
    {
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 2.5f, 13.5f)))
        {
            Vector3 origin = f.Hunter.transform.position;
            ScenarioZone straightNearest = null;
            ScenarioZone pathNearest = null;
            float straightDistance = float.PositiveInfinity;
            float bestLength = float.PositiveInfinity;
            foreach (ScenarioZone zone in scenario.SafeZones)
            {
                float distance = PlanarDistance(origin, zone.transform.position);
                if (distance < straightDistance) { straightDistance = distance; straightNearest = zone; }
                if (graph.TryFindPath(origin, zone.transform.position, NavigationAccess.Hunter, out _, out float length)
                    && length < bestLength) { bestLength = length; pathNearest = zone; }
            }
            Check(straightNearest != pathNearest && pathNearest != null, "La fixture debe distinguir distancia recta de longitud del camino.");
            Check(HunterSafeZoneSelection.TrySelect(graph, scenario, origin, out ScenarioZone selected, out float selectedLength)
                && selected == pathNearest && Mathf.Abs(selectedLength - bestLength) < 0.01f,
                "Debe elegir el refugio con el menor camino A* completo.");
            GameObject south = f.Barrier(Point(graph, 19.5f, 10f), new Vector3(3f, 2.4f, 1f));
            Check(HunterSafeZoneSelection.TrySelect(graph, scenario, origin, out selected, out _)
                && selected != pathNearest, "Debe ignorar el refugio cuya unica entrada quedo bloqueada.");
            f.Barrier(Point(graph, 16.5f, 31f), new Vector3(3f, 2.4f, 1f));
            f.Barrier(Point(graph, 38.5f, 26.5f), new Vector3(1f, 2.4f, 3f));
            Check(!HunterSafeZoneSelection.TrySelect(graph, scenario, origin, out selected, out _) && selected == null,
                "Si todos los refugios son inaccesibles no debe inventar una ruta.");
            WolfAgent threat = f.Wolf(origin + Vector3.right * 2f);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterFleeState && f.Hunter.SelectedSafeZone == null,
                "La amenaza debe activar Flee aunque por ahora no haya refugio alcanzable.");
            Vector3 waitingPosition = f.Hunter.transform.position;
            threat.enabled = false;
            f.Tick(0.5f);
            Check(f.Hunter.CurrentState is HunterFleeState && Near(f.Hunter.transform.position, waitingPosition),
                "Sin camino debe detenerse y conservar Flee aunque pierda de vista al lobo.");
            south.SetActive(false);
            f.Tick(0.6f);
            Check(f.Hunter.SelectedSafeZone == pathNearest && f.Hunter.Navigation.HasDestination,
                "Al abrir una entrada, Flee debe reintentar y navegar al refugio recuperado.");
        }
    }

    private static void ValidatePrioritiesAndDeath(NavigationGraph graph, ScenarioReferences scenario)
    {
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 55.5f, 25.5f), 0f))
        {
            f.Boid(f.Hunter.transform.position + Vector3.right * 3f, true);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterAttackState, "Con TBA listo y un Boid vivo debe ingresar a Attack.");
            BoidAgent corpse = f.Boid(f.Hunter.transform.position + Vector3.forward * 0.5f, false);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterGatherState && f.Hunter.Target == corpse && f.Hunter.GatherProgress > 0f,
                "Gather debe tener prioridad sobre Attack y empezar su accion temporizada.");
            f.Wolf(f.Hunter.transform.position - Vector3.right * 3f);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterFleeState && f.Hunter.GatherProgress == 0f
                && f.Hunter.GatherCount == 0 && f.Hunter.Target == null && corpse.gameObject.activeSelf,
                "Flee debe cancelar la recoleccion sin consumir el Boid ni sumar una recompensa.");
            AddRequiredGathers(f.Hunter);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterFleeState && !f.Hunter.CanAttack,
                "Flee debe conservar prioridad sobre Exit y bloquear el combate.");
            Check(f.Hunter.RequestCapture(), "La fixture debe admitir el hecho de captura fuera del refugio.");
            f.Tick(0.01f);
            Check(f.Hunter.IsDead && !f.Hunter.IsAvailableToWolves && !f.Hunter.Navigation.HasDestination
                && !f.Hunter.GetComponent<Renderer>().enabled && !f.Hunter.GetComponent<Collider>().enabled,
                "Dead debe superar a Flee/Exit, detener y ocultar al Cazador.");
            f.Tick(f.Hunter.RespawnTime - 0.02f);
            Check(f.Hunter.IsDead, "Dead no debe terminar antes de RespawnTime.");
            f.Tick(0.02f);
            Check(f.Hunter.CurrentState is HunterSearchState && f.Hunter.GatherCount == 0
                && f.Hunter.DeathCount == 1 && f.Hunter.RespawnCount == 1 && f.Hunter.CompletedCycles == 0
                && Near(f.Hunter.transform.position, scenario.HunterSpawn.position)
                && f.Hunter.GetComponent<Renderer>().enabled && f.Hunter.GetComponent<Collider>().enabled,
                "La muerte debe reaparecer en Search, reiniciar el contador y restaurar la presencia del agente.");
        }
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 55.5f, 25.5f), 0f))
        {
            AddRequiredGathers(f.Hunter);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterExitState, "Al alcanzar la cuota debe ingresar a Exit.");
            Check(f.Hunter.RequestCapture(), "Exit debe permitir una captura antes de completar la salida.");
            f.Tick(Step);
            Check(f.Hunter.IsDead && f.Hunter.CompletedCycles == 0, "Dead debe interrumpir Exit sin acreditar un ciclo exitoso.");
        }
    }

    private static void ValidateSuccessfulAttackReturn(NavigationGraph graph, ScenarioReferences scenario)
    {
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 55.5f, 25.5f), 0f))
        {
            BoidAgent target = f.Boid(f.Hunter.transform.position + Vector3.right * 3f, true);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterAttackState && f.Hunter.Target == target,
                "La regresion de impacto requiere un objetivo vivo elegido por Attack.");
            // Se entrega el mismo resultado publico de dano/impacto que recibe el agente
            // desde el proyectil. La simulacion fisica del proyectil se comprueba en Play.
            target.TakeDamage(float.MaxValue);
            f.Hunter.ResolveProjectile(true);
            f.Tick(Step);
            Check(!target.IsAlive && f.Hunter.CurrentState is HunterSearchState && !f.Hunter.CanAttack
                && f.Hunter.Target == null && f.Hunter.GatherCount == 0,
                "Un ataque exitoso debe reiniciar TBA y pasar por Search antes de recolectar al objetivo eliminado.");
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterGatherState && f.Hunter.Target == target,
                "En la evaluacion siguiente Search debe permitir Gather del Boid recien eliminado.");
        }
    }

    private static void ValidateInterestBeforeGather(NavigationGraph graph, ScenarioReferences scenario)
    {
        int initialActiveCount = InterestObject.ActiveCount;
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 55.5f, 25.5f), 0f))
        {
            f.Boid(f.Hunter.transform.position + Vector3.right * 0.5f, false);
            GameObject template = f.Create("Plantilla temporal de interes", Vector3.one * 1000f);
            template.AddComponent<SphereCollider>().radius = 0.2f;
            InterestObject prefab = template.AddComponent<InterestObject>();
            Transform generated = f.Create("Intereses generados temporalmente", Vector3.zero).transform;
            using (SerializedObject data = new SerializedObject(f.Hunter))
            {
                data.FindProperty("_interestPrefab").objectReferenceValue = prefab;
                data.FindProperty("_interestParent").objectReferenceValue = generated;
                data.FindProperty("_spawnTimer").floatValue = data.FindProperty("_spawnInterval").floatValue;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            f.Tick(Step);
            Check(generated.childCount == 1 && generated.GetChild(0).GetComponent<InterestObject>() != null
                && generated.GetChild(0).gameObject.activeSelf && scenario.HuntingZone.Contains(generated.GetChild(0).position)
                && f.Hunter.CurrentState is HunterGatherState,
                "Con temporizador vencido, Search debe generar el interes dentro del Coto antes de pasar a Gather.");
            f.Tick(Step);
            Check(generated.childCount == 1 && f.Hunter.GatherProgress > 0f,
                "Tras generar el interes debe continuar Gather sin duplicar el objeto en el siguiente Update.");
        }
        if (InterestObject.ActiveCount != initialActiveCount)
            throw new InvalidOperationException("La fixture de interes no restauro el contador de objetos activos.");
    }

    private static void ValidateUnavailableGather(NavigationGraph graph, ScenarioReferences scenario)
    {
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 55.5f, 25.5f), 0f))
        {
            BoidAgent corpse = f.Boid(f.Hunter.transform.position + Vector3.right * 0.5f, false);
            f.Tick(Step);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterGatherState && f.Hunter.GatherProgress > 0f, "Debe empezar la recoleccion disponible.");
            corpse.gameObject.SetActive(false);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterSearchState && f.Hunter.GatherCount == 0 && f.Hunter.GatherProgress == 0f,
                "Si desaparece el objetivo de Gather debe cancelar sin sumar.");
        }
    }

    private static void ValidateSafeWait(NavigationGraph graph, ScenarioReferences scenario, bool quotaComplete)
    {
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 38.5f, 26.5f)))
        {
            WolfAgent wolf = f.Wolf(Point(graph, 36.5f, 26.5f));
            if (quotaComplete) AddRequiredGathers(f.Hunter);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterFleeState && f.Hunter.SelectedSafeZone != null,
                "La amenaza debe superar a Exit y seleccionar un refugio.");
            wolf.enabled = false;
            for (int i = 0; i < 150 && !f.Hunter.IsWaitingInSafeZone; i++) f.Tick(Step);
            Check(f.Hunter.IsWaitingInSafeZone && f.Hunter.IsInSafeZone && !f.Hunter.Navigation.HasDestination
                && Near(f.Hunter.transform.position, f.Hunter.SelectedSafeZone.transform.position),
                "La huida debe completar el camino al centro del refugio antes de esperar, aunque el lobo ya no sea visible.");
            Vector3 waitingPosition = f.Hunter.transform.position;
            f.Tick(0.6f);
            Check(f.Hunter.CurrentState is HunterFleeState && Near(waitingPosition, f.Hunter.transform.position)
                && Mathf.Abs(f.Hunter.SafeZoneWaitRemaining - 0.4f) < 0.01f, "Debe cumplir la espera dentro del refugio.");
            f.Tick(0.39f);
            Check(f.Hunter.CurrentState is HunterFleeState, "La espera no debe terminar anticipadamente.");
            f.Tick(0.02f);
            Check(quotaComplete ? f.Hunter.CurrentState is HunterExitState : f.Hunter.CurrentState is HunterSearchState,
                "Tras la espera debe retomar Exit con cuota completa, o Search en caso contrario.");
            if (quotaComplete) return;
            wolf.enabled = true;
            f.Tick(Step);
            Check(!(f.Hunter.CurrentState is HunterFleeState), "Ver un lobo desde dentro del refugio no debe reiniciar inmediatamente la espera.");
            for (int i = 0; i < 100 && !(f.Hunter.CurrentState is HunterFleeState); i++) f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterFleeState, "Al salir y volver a percibir al lobo debe huir otra vez.");
        }
    }

    private static void ValidateExitAndRestart(NavigationGraph graph, ScenarioReferences scenario)
    {
        using (Fixture f = new Fixture(graph, scenario, Point(graph, 55.5f, 25.5f)))
        {
            f.Boid(f.Hunter.transform.position + Vector3.right * 0.5f, false);
            f.Boid(f.Hunter.transform.position + Vector3.forward * 2f, true);
            AddRequiredGathers(f.Hunter);
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterExitState && f.Hunter.Target == null && !f.Hunter.CanAttack,
                "Exit debe superar a Gather/Attack y mantener deshabilitado el combate.");
            bool validTravel = true;
            for (int i = 0; i < 500 && !f.Hunter.IsCompletingExit; i++)
            {
                Vector3 previous = f.Hunter.transform.position;
                f.Tick(Step);
                validTravel &= f.Hunter.CurrentState is HunterExitState
                    && graph.CanTraverse(previous, f.Hunter.transform.position, NavigationAccess.Hunter)
                    && Vector3.Distance(previous, f.Hunter.transform.position) <= f.Hunter.MovementSpeed * Step + 0.01f;
            }
            Check(validTravel && f.Hunter.IsCompletingExit && f.Hunter.Navigation.CalculationCount > 0,
                "Exit debe alcanzar la salida por A* sin atravesar paredes ni cambiar de actividad.");
            Check(!f.Hunter.IsAvailableToWolves && !f.Hunter.GetComponent<Renderer>().enabled
                && f.Hunter.CompletedCycles == 0, "Al llegar debe desaparecer antes de reiniciar el ciclo.");
            f.Tick(Step);
            Check(f.Hunter.CurrentState is HunterSearchState && f.Hunter.CompletedCycles == 1
                && f.Hunter.DeathCount == 0 && f.Hunter.RespawnCount == 0 && f.Hunter.GatherCount == 0
                && f.Hunter.Target == null && Near(f.Hunter.transform.position, scenario.HunterSpawn.position)
                && f.Hunter.IsAvailableToWolves && f.Hunter.GetComponent<Renderer>().enabled,
                "Una salida exitosa debe reiniciar Search sin contabilizar una muerte ni aplicar RespawnTime.");
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly HunterAgent Hunter;
        public Fixture(NavigationGraph graph, ScenarioReferences scenario, Vector3 position, float speed = 4.5f)
        {
            Root = new GameObject("Fixture temporal del ciclo del Cazador");
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Cazador temporal";
            body.layer = TestLayer;
            body.transform.SetParent(Root.transform);
            body.transform.position = position;
            body.transform.localScale = Vector3.one * 0.75f;
            PathFollower follower = body.AddComponent<PathFollower>();
            follower.Graph = graph;
            AgentNavigator navigator = body.AddComponent<AgentNavigator>();
            navigator.Follower = follower;
            Hunter = body.AddComponent<HunterAgent>();
            using (SerializedObject data = new SerializedObject(Hunter))
            {
                data.FindProperty("_navigation").objectReferenceValue = navigator;
                data.FindProperty("_scenario").objectReferenceValue = scenario;
                data.FindProperty("_perceptionMask").intValue = TestMask;
                data.FindProperty("_perceptionInterval").floatValue = 0.01f;
                data.FindProperty("_speed").floatValue = speed;
                data.FindProperty("_attackTimer").floatValue = Hunter.TBA;
                data.FindProperty("_safeZoneWaitTime").floatValue = 1f;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Hunter.Initialize();
        }
        public GameObject Create(string name, Vector3 position)
        {
            GameObject created = new GameObject(name);
            created.layer = TestLayer;
            created.transform.SetParent(Root.transform);
            created.transform.position = position;
            return created;
        }
        public WolfAgent Wolf(Vector3 position)
        {
            GameObject created = Create("Lobo temporal", position);
            created.AddComponent<SphereCollider>().radius = 0.2f;
            return created.AddComponent<WolfAgent>();
        }
        public BoidAgent Boid(Vector3 position, bool alive)
        {
            GameObject created = Create("Boid temporal", position);
            created.AddComponent<SphereCollider>().radius = 0.2f;
            BoidAgent boid = created.AddComponent<BoidAgent>();
            // Mismo arranque de fixtures que BlockOneValidation; no se fuerza ninguna FSM.
            boid.SendMessage("Awake");
            boid.SendMessage("OnEnable");
            if (!alive) boid.TakeDamage(float.MaxValue);
            return boid;
        }
        public GameObject Barrier(Vector3 position, Vector3 size)
        {
            GameObject barrier = Create("Barrera temporal", position);
            barrier.layer = LayerMask.NameToLayer("MapObstacle");
            barrier.AddComponent<BoxCollider>().size = size;
            Physics.SyncTransforms();
            return barrier;
        }
        public void Tick(float deltaTime) { Physics.SyncTransforms(); Hunter.UpdateAgent(deltaTime); }
        public void Dispose() { Object.DestroyImmediate(Root); Physics.SyncTransforms(); }
    }

    private static void AddRequiredGathers(HunterAgent hunter)
    {
        for (int i = hunter.GatherCount; i < hunter.RequiredGathers; i++) hunter.RegisterGather();
    }
    private static T FindSingle<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
        Check(found.Count == 1, "Debe existir un unico " + typeof(T).Name + ".");
        return found[0];
    }
    private static Vector3 Point(NavigationGraph graph, float x, float z) => DungeonLayout.ToWorld(x, z, graph.SamplingHeight);
    private static float PlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static bool Near(Vector3 a, Vector3 b) => PlanarDistance(a, b) < 0.01f;
    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
