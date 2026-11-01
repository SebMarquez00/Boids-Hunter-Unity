using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WolfPlayValidation
{
    private const string PendingKey = "IA.WolfPlayValidation.Pending";
    private const string ModeKey = "IA.WolfPlayValidation.ControlledCapture";
    private enum Phase { NaturalObservation, ControlledCapture }
    private static readonly List<WolfAgent> Wolves = new List<WolfAgent>();
    private static readonly Dictionary<WolfAgent, Vector3> PatrolOrigins = new Dictionary<WolfAgent, Vector3>();
    private static readonly HashSet<WolfAgent> MovedInPatrol = new HashSet<WolfAgent>();
    private static readonly HashSet<WolfAgent> ReceivedAlerts = new HashSet<WolfAgent>();
    private static HunterAgent _hunter;
    private static ScenarioReferences _scenario;
    private static NavigationGraph _graph;
    private static Vector3 _spawnPosition;
    private static Vector3 _deadPosition;
    private static double _startedAt;
    private static float _observedDeadAt;
    private static int _initialDeaths;
    private static int _initialRespawns;
    private static int _initialCaptures;
    private static bool _controlledCapture;
    private static Phase _phase;
    private static Renderer[] _renderers;
    private static Collider[] _colliders;
    private static bool[] _rendererStates;
    private static bool[] _colliderStates;
    private static bool _running;
    private static bool _sawVisual;
    private static bool _sawHowl;
    private static bool _sawKnownPosition;
    private static bool _sawPursuitPath;
    private static bool _sawCapture;
    private static bool _sawDead;

    static WolfPlayValidation()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        Subscribe();
        if (EditorApplication.isPlaying) EditorApplication.delayCall += StartRun;
    }

    [MenuItem("Tools/IA/Entrega 2/Probar lobos en Play")]
    public static void Run() => Begin(false);

    [MenuItem("Tools/IA/Entrega 2/Probar captura y reaparicion en Play")]
    public static void RunCapture() => Begin(true);

    private static void Begin(bool controlledCapture)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Esta prueba empieza desde Edit Mode y observa una simulacion nueva.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath || scene.isDirty)
        {
            Debug.LogWarning("Abri DungeonSimulation guardada y sin cambios pendientes antes de iniciar la prueba.");
            return;
        }
        Cleanup();
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool(ModeKey, controlledCapture);
        Subscribe();
        EditorApplication.isPlaying = true;
    }

    private static void Subscribe()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update -= Observe;
        EditorApplication.update += Observe;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) StartRun();
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            Cleanup();
            Debug.LogWarning("Prueba de lobos cancelada al salir de Play.");
        }
    }

    private static void StartRun()
    {
        if (_running || !SessionState.GetBool(PendingKey, false)) return;
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            Require(scene.path == DungeonMapBuilder.ScenePath, "La escena activa cambio al entrar en Play.");
            _hunter = FindSingle<HunterAgent>(scene);
            _scenario = FindSingle<ScenarioReferences>(scene);
            _graph = FindSingle<NavigationGraph>(scene);
            Wolves.AddRange(FindAll<WolfAgent>(scene));
            Require(Wolves.Count >= 4 && _scenario.HunterSpawn != null, "Faltan los cuatro lobos o el marcador inicial.");
            _spawnPosition = _scenario.HunterSpawn.position;
            _initialDeaths = _hunter.DeathCount;
            _initialRespawns = _hunter.RespawnCount;
            _controlledCapture = SessionState.GetBool(ModeKey, false);
            _phase = Phase.NaturalObservation;
            foreach (WolfAgent wolf in Wolves) PatrolOrigins.Add(wolf, wolf.transform.position);
            _startedAt = EditorApplication.timeSinceStartup;
            _observedDeadAt = -1f;
            _sawVisual = _sawHowl = _sawKnownPosition = _sawPursuitPath = _sawCapture = _sawDead = false;
            _running = true;
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Observe()
    {
        if (!_running || !EditorApplication.isPlaying) return;
        try
        {
            double timeout = _phase == Phase.NaturalObservation ? 60d : 20d;
            Require(EditorApplication.timeSinceStartup - _startedAt <= timeout,
                (_phase == Phase.NaturalObservation ? "No se reunio la evidencia natural en 60 segundos. "
                    : "La captura y reaparicion controladas no finalizaron en 20 segundos. ") + Evidence());
            Require(_hunter != null && _scenario != null && _graph != null
                && SceneManager.GetActiveScene() == _scenario.gameObject.scene, "Cambio la escena o desaparecio un actor.");
            Require(Vector3.Distance(_scenario.HunterSpawn.position, _spawnPosition) < 0.001f,
                "El marcador de reaparicion se desplazo.");
            Physics.SyncTransforms();
            foreach (WolfAgent wolf in Wolves) ObserveWolf(wolf);
            if (_phase == Phase.NaturalObservation) ObserveNaturalBehavior();
            else ObserveCaptureAndRespawn();
        }
        catch (Exception error) { Fail(error); }
    }

    private static void ObserveWolf(WolfAgent wolf)
    {
        Require(wolf != null && wolf.isActiveAndEnabled, "Un lobo desaparecio o se desactivo.");
        Require(wolf.Navigation != null && wolf.Navigation.Follower != null
            && wolf.Navigation.Follower.Graph == _graph && wolf.Navigation.Follower.Access == NavigationAccess.Wolf,
            "Un lobo dejo de utilizar el grafo compartido con acceso Wolf.");
        Require(wolf.PatrolSpeed < _hunter.MovementSpeed && wolf.ChaseSpeed < _hunter.MovementSpeed
            && wolf.SearchSpeed < _hunter.MovementSpeed, "Un lobo es tan rapido como el Cazador.");
        Vector3 probe = wolf.transform.position;
        probe.y = _graph.SamplingHeight;
        Require(!_scenario.ContainsSafeZone(probe)
            && !Physics.CheckSphere(probe, _graph.AgentRadius, _graph.ObstacleMask, QueryTriggerInteraction.Ignore),
            "Un lobo entro en un refugio o se superpuso a una pared.");
        if (wolf.CurrentStateName == "WolfPatrolState" && PlanarDistance(wolf.transform.position, PatrolOrigins[wolf]) > 0.2f)
            MovedInPatrol.Add(wolf);
        _sawVisual |= wolf.HasVisualContact;
        _sawHowl |= wolf.HowlCount > 0;
        _sawKnownPosition |= wolf.HasLastKnownPosition;
        if (wolf.ReceivedAlertCount > 0) ReceivedAlerts.Add(wolf);
        _sawPursuitPath |= (wolf.CurrentStateName == "WolfChaseState" || wolf.CurrentStateName == "WolfSearchState")
            && wolf.Navigation.Follower.IsMoving && wolf.Navigation.Follower.Path.Count > 1;
    }

    private static bool HasNaturalEvidence() => MovedInPatrol.Count == Wolves.Count
        && ReceivedAlerts.Count >= Wolves.Count - 1 && _sawVisual && _sawHowl && _sawKnownPosition && _sawPursuitPath;

    private static void ObserveNaturalBehavior()
    {
        if (!HasNaturalEvidence()) return;
        if (!_controlledCapture)
        {
            string evidence = Evidence();
            Cleanup();
            Debug.Log("Lobos en Play OK: los actores reales patrullaron, detectaron, emitieron y recibieron alertas " +
                "y utilizaron A* para perseguir/buscar. " + evidence +
                " La captura y reaparicion se comprueban con su menu separado. Play sigue activo.");
            return;
        }
        if (!_hunter.IsAvailableToWolves || _hunter.IsInSafeZone) return;
        foreach (WolfAgent wolf in Wolves)
        {
            if (wolf.CurrentStateName != "WolfChaseState" || !wolf.HasVisualContact) continue;
            PrepareControlledCapture(wolf);
            return;
        }
    }

    private static void PrepareControlledCapture(WolfAgent wolf)
    {
        // Preparacion geometrica unica en Play: no solicita captura ni cambia estados.
        // El lobo ya esta persiguiendo; su percepcion y su FSM deben resolver el contacto.
        Vector3 hunterPosition = DungeonLayout.ToWorld(55.5f, 25.5f, _hunter.transform.position.y);
        Vector3 wolfPosition = hunterPosition - Vector3.right * 0.7f;
        wolfPosition.y = wolf.transform.position.y;
        Require(_graph.CanTraverse(hunterPosition, hunterPosition, NavigationAccess.Hunter)
            && _graph.CanTraverse(wolfPosition, wolfPosition, NavigationAccess.Wolf),
            "La preparacion de captura debe quedar dentro del mapa transitable.");
        _initialDeaths = _hunter.DeathCount;
        _initialRespawns = _hunter.RespawnCount;
        _initialCaptures = TotalCaptures();
        _renderers = _hunter.GetComponentsInChildren<Renderer>(true);
        _colliders = _hunter.GetComponentsInChildren<Collider>(true);
        _rendererStates = new bool[_renderers.Length];
        _colliderStates = new bool[_colliders.Length];
        for (int i = 0; i < _renderers.Length; i++) _rendererStates[i] = _renderers[i].enabled;
        for (int i = 0; i < _colliders.Length; i++) _colliderStates[i] = _colliders[i].enabled;
        _hunter.RegisterGather();
        _hunter.RegisterGather();
        Require(_hunter.GatherCount > 0, "Debe haber recolecciones previas para comprobar el reinicio del contador.");
        _hunter.Stop();
        wolf.Stop();
        _hunter.transform.position = hunterPosition;
        wolf.transform.position = wolfPosition;
        wolf.transform.forward = Vector3.right;
        Physics.SyncTransforms();
        _phase = Phase.ControlledCapture;
        _startedAt = EditorApplication.timeSinceStartup;
        _sawCapture = _sawDead = false;
        _observedDeadAt = -1f;
        Debug.Log("CAPTURA CONTROLADA: tras comprobar la interaccion natural, se reposicionaron una sola vez " +
            "el Cazador y " + wolf.name + " en el Coto, a 0.7 m, con recolecciones previas. " +
            "Desde ahora la prueba solo observa la percepcion, la captura y la FSM. Estos cambios se descartan al salir de Play.");
    }

    private static void ObserveCaptureAndRespawn()
    {
        _sawCapture |= TotalCaptures() > _initialCaptures;
        Require(_graph.CanTraverse(_hunter.transform.position, _hunter.transform.position, NavigationAccess.Hunter),
            "El Cazador abandono el espacio transitable durante la prueba controlada.");
        if (_hunter.IsDead)
        {
            if (!_sawDead)
            {
                _sawDead = true;
                _observedDeadAt = Time.time;
                _deadPosition = _hunter.transform.position;
            }
            Require(!_hunter.IsAvailableToWolves && !_hunter.HasActiveProjectile
                && _hunter.GatherProgress == 0f && _hunter.Navigation != null && !_hunter.Navigation.HasDestination
                && PlanarDistance(_hunter.transform.position, _deadPosition) <= 0.001f,
                "Durante Dead debe quedar inmovil, sin combate, recoleccion ni navegacion.");
            foreach (Collider collider in _hunter.GetComponentsInChildren<Collider>(true))
                Require(!collider.enabled, "El Cazador muerto conserva un collider activo.");
            foreach (Renderer renderer in _hunter.GetComponentsInChildren<Renderer>(true))
                Require(!renderer.enabled, "El Cazador muerto sigue visible.");
            return;
        }
        if (!_sawDead || _hunter.RespawnCount <= _initialRespawns) return;
        Require(_hunter.DeathCount == _initialDeaths + 1 && _hunter.RespawnCount == _initialRespawns + 1,
            "La primera captura debe producir una sola muerte y una reaparicion.");
        Require(Time.time - _observedDeadAt >= _hunter.RespawnTime - 0.25f,
            "El Cazador reaparecio antes de cumplir su RespawnTime.");
        Require(_hunter.GatherCount == 0 && _hunter.IsAvailableToWolves && PlanarDistance(_hunter.transform.position, _spawnPosition) <= 1f
            && _scenario.StartZone.Contains(_hunter.transform.position) && _hunter.CurrentState is HunterSearchState,
            "Debe reaparecer en la zona inicial, con contador cero y estado Search.");
        Require(_hunter.Target == null && _hunter.GatherProgress == 0f && !_hunter.HasActiveProjectile,
            "La reaparicion no debe conservar objetivo, recoleccion en curso ni proyectil del ciclo anterior.");
        Require(_sawCapture, "La muerte debe provenir de una captura observada en los lobos.");
        for (int i = 0; i < _renderers.Length; i++)
            Require(_renderers[i] != null && _renderers[i].enabled == _rendererStates[i], "No se restauro un Renderer del Cazador.");
        for (int i = 0; i < _colliders.Length; i++)
            Require(_colliders[i] != null && _colliders[i].enabled == _colliderStates[i], "No se restauro un Collider del Cazador.");
        string evidence = Evidence();
        Cleanup();
        Debug.Log("Captura CONTROLADA en Play OK: el lobo capturo mediante su percepcion/FSM; Dead detuvo al Cazador, " +
            "y tras RespawnTime reaparecio en Search con contador cero y renderers/colliders restaurados. " + evidence +
            " Play sigue activo; salir de Play descarta las posiciones y recolecciones preparadas por esta prueba.");
    }

    private static string Evidence() => $"Patrullas {MovedInPatrol.Count}/{Wolves.Count}; receptores {ReceivedAlerts.Count}; " +
        $"vision={_sawVisual}, aullido={_sawHowl}, memoria={_sawKnownPosition}, camino={_sawPursuitPath}." +
        (_phase == Phase.ControlledCapture ? $" Captura controlada={_sawCapture}, Dead={_sawDead}." : "");

    private static int TotalCaptures()
    {
        int count = 0;
        foreach (WolfAgent wolf in Wolves) if (wolf != null) count += wolf.CaptureCount;
        return count;
    }

    private static void BeforeReload()
    {
        if (!_running) return;
        Cleanup();
        Debug.LogWarning("Prueba de lobos cancelada por recarga de scripts.");
    }

    private static void Fail(Exception error)
    {
        Cleanup();
        Debug.LogError("Prueba de lobos en Play: " + error.Message);
    }

    private static void Cleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.update -= Observe;
        EditorApplication.delayCall -= StartRun;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        SessionState.EraseBool(PendingKey);
        SessionState.EraseBool(ModeKey);
        _running = false;
        Wolves.Clear();
        PatrolOrigins.Clear();
        MovedInPatrol.Clear();
        ReceivedAlerts.Clear();
        _hunter = null;
        _scenario = null;
        _graph = null;
        _renderers = null;
        _colliders = null;
        _rendererStates = null;
        _colliderStates = null;
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

    private static float PlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
