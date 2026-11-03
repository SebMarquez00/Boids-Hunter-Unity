using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HunterNavigationPlayValidation
{
    private const string PendingKey = "IA.HunterNavigationPlayValidation.Pending";
    private static readonly List<WolfAgent> PausedWolves = new List<WolfAgent>();
    private enum Phase { BlockedStart, AwaitingPassage, AwaitingHuntingGround }
    private static HunterAgent _hunter;
    private static ScenarioReferences _scenario;
    private static NavigationGraph _graph;
    private static GameObject _gate;
    private static Vector3 _spawnPosition;
    private static Vector3 _initialPosition;
    private static Vector3 _waitingPosition;
    private static double _startedAt;
    private static float _holdStarted;
    private static int _initialCalculations;
    private static int _detourCalculations;
    private static bool _running;
    private static bool _sawWaiting;
    private static bool _sawResume;
    private static Phase _phase;

    static HunterNavigationPlayValidation()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        Subscribe();
        if (EditorApplication.isPlaying) EditorApplication.delayCall += StartRun;
    }

    [MenuItem("Tools/IA/Entrega 2/Probar recalculo en Play")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Esta prueba se inicia desde Edit Mode; deja que la herramienta entre en Play.");
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
            Debug.LogWarning("Prueba de navegacion cancelada al salir de Play.");
        }
    }

    private static void StartRun()
    {
        if (_running || !SessionState.GetBool(PendingKey, false)) return;
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            Require(scene.path == DungeonMapBuilder.ScenePath, "La escena activa cambio al entrar en Play.");
            _hunter = GetUnique<HunterAgent>(scene);
            _scenario = GetUnique<ScenarioReferences>(scene);
            Require(_hunter.isActiveAndEnabled && _hunter.Navigation != null && _hunter.Navigation.Follower != null,
                "El Cazador no tiene navegacion activa.");
            Require(_hunter.Scenario == _scenario && _scenario.HunterSpawn != null, "Las referencias del Cazador no corresponden al escenario.");
            _graph = _hunter.Navigation.Follower.Graph;
            Require(_graph != null, "El Cazador no tiene grafo.");
            int obstacleLayer = LayerMask.NameToLayer("MapObstacle");
            Require(obstacleLayer >= 0 && (_graph.ObstacleMask.value & (1 << obstacleLayer)) != 0,
                "El grafo no considera la capa MapObstacle.");
            _spawnPosition = _scenario.HunterSpawn.position;
            _initialPosition = _hunter.transform.position;
            Require(PlanarDistance(_initialPosition, _spawnPosition) <= 0.2f, "El Cazador ya se alejo del punto inicial.");
            PauseWolves(scene);
            _gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _gate.name = "VALIDACION TEMPORAL - Bloqueo de navegacion";
            _gate.layer = obstacleLayer;
            _gate.transform.position = new Vector3(-24.5f, 1f, -9.5f);
            _gate.transform.localScale = new Vector3(3f, 2f, 1f);
            Physics.SyncTransforms();
            _startedAt = EditorApplication.timeSinceStartup;
            _holdStarted = -1f;
            _initialCalculations = 0;
            _waitingPosition = default;
            _sawWaiting = _sawResume = false;
            _phase = Phase.BlockedStart;
            _running = true;
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Observe()
    {
        if (!_running || !EditorApplication.isPlaying) return;
        try
        {
            Require(EditorApplication.timeSinceStartup - _startedAt <= 45d, "Se superaron 45 segundos sin completar el recorrido.");
            Require(_hunter != null && _scenario != null && _graph != null && _gate != null, "Desaparecio una referencia durante la prueba.");
            Require(SceneManager.GetActiveScene() == _scenario.gameObject.scene, "La escena activa cambio durante la prueba.");
            Require((_scenario.HunterSpawn.position - _spawnPosition).sqrMagnitude < 0.000001f, "El marcador HunterSpawn se movio durante el recorrido.");
            Physics.SyncTransforms();
            Vector3 probe = _hunter.transform.position;
            probe.y = _graph.SamplingHeight;
            Require(!Physics.CheckSphere(probe, _graph.AgentRadius, _graph.ObstacleMask, QueryTriggerInteraction.Ignore),
                "El cuerpo del Cazador se superpuso a un obstaculo.");
            if (_phase == Phase.BlockedStart) ObserveBlockedStart();
            else if (_phase == Phase.AwaitingPassage) ObservePassage();
            else if (_hunter.IsInHuntingGround) Complete();
        }
        catch (Exception error) { Fail(error); }
    }

    private static void ObserveBlockedStart()
    {
        AgentNavigator navigation = _hunter.Navigation;
        Vector3 position = _hunter.transform.position;
        float barrierFront = _gate.transform.position.z - _gate.transform.localScale.z * 0.5f;
        Require(position.z + _graph.AgentRadius <= barrierFront + 0.001f,
            "El Cazador atraveso la barrera que cierra el corredor inicial.");
        // Una ruta calculada antes del bloqueo puede recorrer sus tramos todavia libres.
        if (!_sawWaiting)
        {
            if (!navigation.WaitingForPath) return;
            _sawWaiting = true;
            _waitingPosition = position;
            _holdStarted = Time.time;
            _initialCalculations = navigation.CalculationCount;
        }
        Require(navigation.WaitingForPath, "La navegacion dejo de esperar aunque el corredor sigue cerrado.");
        Require(PlanarDistance(position, _waitingPosition) <= 0.1f,
            "El Cazador siguio avanzando despues de quedar esperando un camino.");
        int attempts = navigation.CalculationCount - _initialCalculations;
        Require(attempts <= 12, "El Cazador recalcula continuamente mientras no hay camino.");
        if (Time.time - _holdStarted < 3f) return;
        Require(attempts >= 2, "No se observaron reintentos espaciados durante tres segundos de espera.");
        _gate.SetActive(false);
        Physics.SyncTransforms();
        _phase = Phase.AwaitingPassage;
    }

    private static void ObservePassage()
    {
        Vector3 position = _hunter.transform.position;
        Require(position.x < -3f, "El Cazador paso el punto de activacion del segundo bloqueo sin que pudiera observarse.");
        if (position.x <= -8f || position.z >= -5f) return;
        _sawResume = PlanarDistance(position, _waitingPosition) > 1f;
        _detourCalculations = _hunter.Navigation.CalculationCount;
        _gate.transform.position = new Vector3(-1f, 1f, -6.5f);
        _gate.transform.localScale = new Vector3(1f, 2f, 3f);
        _gate.SetActive(true);
        Physics.SyncTransforms();
        Require(RemainingPathIsBlocked(), "El segundo bloqueo debe cortar la ruta que el Cazador esta siguiendo.");
        _phase = Phase.AwaitingHuntingGround;
    }

    private static bool RemainingPathIsBlocked()
    {
        PathFollower follower = _hunter.Navigation.Follower;
        Vector3 from = _hunter.transform.position;
        for (int i = follower.WaypointIndex; i < follower.Path.Count; i++)
        {
            Vector3 to = follower.Path[i];
            if (!_graph.CanTraverse(from, to, NavigationAccess.Hunter)) return true;
            from = to;
        }
        return false;
    }

    private static void Complete()
    {
        int detourAttempts = _hunter.Navigation.CalculationCount - _detourCalculations;
        Require(_sawWaiting && _sawResume && detourAttempts > 0, "Llego al Coto sin demostrar espera, reanudacion y un nuevo calculo tras el segundo bloqueo.");
        Cleanup();
        Debug.Log("Navegacion real del Cazador OK: espero sin ruta, reanudo al abrir el paso y rodeo el segundo bloqueo " +
            $"hasta entrar al Coto ({detourAttempts} nuevos calculos). HunterSpawn permanecio fijo. Play sigue activo.");
    }

    private static T GetUnique<T>(Scene scene) where T : Component
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

    private static void PauseWolves(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (WolfAgent wolf in root.GetComponentsInChildren<WolfAgent>(true))
            {
                if (!wolf.enabled) continue;
                PausedWolves.Add(wolf);
                wolf.enabled = false;
            }
        Debug.Log("Prueba de recalculo: lobos pausados temporalmente para aislar la navegacion de la huida. " +
            "Se restauran al terminar la prueba.");
    }

    private static void BeforeReload()
    {
        if (!_running) return; // La solicitud inicial sobrevive al domain reload que inicia Play.
        Cleanup();
        Debug.LogWarning("Prueba de navegacion cancelada por recarga de scripts.");
    }

    private static void Fail(Exception error)
    {
        Cleanup();
        Debug.LogError("Prueba de navegacion real del Cazador: " + error.Message);
    }

    private static void Cleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.update -= Observe;
        EditorApplication.delayCall -= StartRun;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        SessionState.EraseBool(PendingKey);
        _running = false;
        foreach (WolfAgent wolf in PausedWolves)
            if (wolf != null) wolf.enabled = true;
        PausedWolves.Clear();
        if (_gate != null)
        {
            _gate.SetActive(false);
            if (Application.isPlaying) Object.Destroy(_gate);
            else Object.DestroyImmediate(_gate);
        }
        _gate = null;
        _hunter = null;
        _scenario = null;
        _graph = null;
    }

    private static float PlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
