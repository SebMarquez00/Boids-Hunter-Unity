using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HunterExitProgressValidation
{
    private const string PendingKey = "IA.ExitProgress.Pending";
    private const string TimeScaleKey = "IA.ExitProgress.PreviousTimeScale";
    private const string NaturalModeKey = "IA.ExitProgress.NaturalMode";
    private const int Seed = 74213;
    private const float MaximumSimulatedSeconds = 240f;
    private const float NaturalMaximumSimulatedSeconds = 600f;
    private static readonly List<WolfAgent> Wolves = new List<WolfAgent>();
    private static HunterAgent _hunter;
    private static ScenarioReferences _scenario;
    private static float _startedAt;
    private static int _initialCycles;
    private static int _initialDeaths;
    private static int _reportedDeaths;
    private static int _reportedCycles;
    private static bool _naturalMode;
    private static int _fleeEntries;
    private static string _lastState;
    private static ScenarioZone _repeatZone;
    private static Vector3 _repeatPosition;
    private static int _nearbyReentries;
    private static bool _reportedRepeatedEntry;
    private static bool _running;

    static HunterExitProgressValidation()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        Subscribe();
        if (EditorApplication.isPlaying) EditorApplication.delayCall += StartRun;
    }

    [MenuItem("Tools/IA/Entrega 2/Probar salida con lobos activos en Play")]
    public static void Run() => Begin(false);

    [MenuItem("Tools/IA/Entrega 2/Probar ciclos naturales con lobos activos en Play")]
    public static void RunNatural() => Begin(true);

    private static void Begin(bool naturalMode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Inicia la prueba de salida con lobos activos desde Edit Mode.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath || scene.isDirty)
        {
            Debug.LogWarning("Abri DungeonSimulation guardada y sin cambios pendientes.");
            return;
        }
        SessionState.SetFloat(TimeScaleKey, Time.timeScale);
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool(NaturalModeKey, naturalMode);
        UnityEngine.Random.InitState(Seed);
        Subscribe();
        EditorApplication.isPlaying = true;
    }

    private static void Subscribe()
    {
        EditorApplication.update -= Observe;
        EditorApplication.update += Observe;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) UnityEngine.Random.InitState(Seed);
        else if (state == PlayModeStateChange.EnteredPlayMode) StartRun();
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            Cleanup();
            Debug.LogWarning("Diagnostico de salida cancelado al salir de Play.");
        }
    }

    private static void StartRun()
    {
        if (_running || !SessionState.GetBool(PendingKey, false)) return;
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            Check(scene.path == DungeonMapBuilder.ScenePath, "La escena activa cambio al iniciar Play.");
            _hunter = FindSingle<HunterAgent>(scene);
            _scenario = FindSingle<ScenarioReferences>(scene);
            _naturalMode = SessionState.GetBool(NaturalModeKey, false);
            Wolves.AddRange(FindAll<WolfAgent>(scene));
            Check(Wolves.Count == 4, "Esta regresion requiere los cuatro lobos del escenario.");
            foreach (WolfAgent wolf in Wolves) Check(wolf.isActiveAndEnabled, "Todos los lobos deben permanecer activos.");
            Check(_hunter.RequiredGathers == 3 && _hunter.GatherCount == 0 && _hunter.IsAvailableToWolves,
                "La prueba requiere un Cazador disponible con cuota 3 y contador inicial cero.");
            UnityEngine.Random.InitState(Seed);
            if (!_naturalMode)
            {
                Vector3 start = DungeonLayout.ToWorld(55.5f, 25.5f, _hunter.transform.position.y);
                NavigationGraph graph = _hunter.Navigation != null && _hunter.Navigation.Follower != null
                    ? _hunter.Navigation.Follower.Graph : null;
                Check(graph != null && _scenario.HuntingZone.Contains(start)
                    && graph.CanTraverse(start, start, NavigationAccess.Hunter), "El inicio preparado debe estar dentro del Coto transitable.");
                _hunter.Stop();
                _hunter.transform.position = start;
                for (int i = 0; i < 3; i++) _hunter.RegisterGather();
            }
            Physics.SyncTransforms();
            _initialCycles = _hunter.CompletedCycles;
            _initialDeaths = _hunter.DeathCount;
            _reportedCycles = _initialCycles;
            _reportedDeaths = _initialDeaths;
            _fleeEntries = _nearbyReentries = 0;
            _repeatZone = null;
            _reportedRepeatedEntry = false;
            _lastState = null;
            _startedAt = Time.time;
            Time.timeScale = 4f;
            _running = true;
            if (_naturalMode)
                Debug.Log("CICLOS NATURALES CON LOBOS ACTIVOS: no se modifica posicion, contador, configuracion ni actividad de ningun agente. " +
                    "Semilla=" + Seed + ", timeScale=4, limite=" + NaturalMaximumSimulatedSeconds +
                    " s simulados. PASS requiere dos CompletedCycles reales; las capturas se informan y la simulacion continua.");
            else
                Debug.Log("SALIDA CON LOBOS ACTIVOS: preparacion unica de Hunter en Coto con 3 recolecciones mediante RegisterGather. " +
                    "No se recoloca, pausa ni configura ningun lobo; no se fuerza la FSM. Semilla=" + Seed +
                    ", timeScale=4, limite=" + MaximumSimulatedSeconds + " s simulados. PASS requiere completar la salida.");
        }
        catch (Exception error) { Finish("PRECONDICION INVALIDA: " + error.Message, true); }
    }

    private static void Observe()
    {
        if (!_running || !EditorApplication.isPlaying) return;
        try
        {
            Check(_hunter != null && _scenario != null && SceneManager.GetActiveScene() == _scenario.gameObject.scene,
                "Cambio la escena o desaparecio el Cazador.");
            foreach (WolfAgent wolf in Wolves)
                Check(wolf != null && wolf.isActiveAndEnabled, "Un lobo fue deshabilitado; el diagnostico deja de ser valido.");
            Check(Mathf.Abs(Time.timeScale - 4f) < 0.001f, "Cambio timeScale durante la prueba.");
            string state = _hunter.CurrentState?.GetType().Name ?? "Sin estado";
            if (state != _lastState)
            {
                _lastState = state;
                if (_hunter.CurrentState is HunterFleeState)
                {
                    _fleeEntries++;
                    TrackRepeatedEntry();
                    Debug.Log(Snapshot("Flee"));
                }
                else if (_hunter.CurrentState is HunterExitState) Debug.Log(Snapshot("Exit"));
            }
            if (_naturalMode && _hunter.CompletedCycles > _reportedCycles)
            {
                _reportedCycles = _hunter.CompletedCycles;
                Debug.Log("CICLO NATURAL COMPLETADO: " + Summary());
                _repeatZone = null;
                _nearbyReentries = 0;
                _reportedRepeatedEntry = false;
            }
            if (_hunter.CompletedCycles - _initialCycles >= (_naturalMode ? 2 : 1))
            {
                Check(_hunter.GatherCount == 0 && _hunter.CurrentState is HunterSearchState,
                    "El ciclo marcado como completo no reinicio Search y contador.");
                Finish((_naturalMode ? "PASS: dos ciclos naturales completados con los 4 lobos activos. "
                    : "PASS: salida completada con los 4 lobos activos. ") + Summary(), false);
                return;
            }
            if (_naturalMode && _hunter.DeathCount > _reportedDeaths)
            {
                _reportedDeaths = _hunter.DeathCount;
                _repeatZone = null;
                _nearbyReentries = 0;
                _reportedRepeatedEntry = false;
                Debug.LogWarning("CAPTURA EN CICLO NATURAL: es un resultado valido del juego; se espera la reaparicion y se continua. " + Summary());
            }
            if (!_naturalMode && (_hunter.DeathCount > _initialDeaths || _hunter.IsDead))
            {
                Finish("CAPTURA: el lobo interrumpio este intento. No es PASS de salida ni prueba de un bucle permanente. " + Summary(), false, true);
                return;
            }
            float limit = _naturalMode ? NaturalMaximumSimulatedSeconds : MaximumSimulatedSeconds;
            if (Elapsed >= limit)
                Finish((_naturalMode ? "SIN DOS CICLOS: " : "SIN SALIDA: ") + "se agotaron " + limit +
                    " s simulados sin completar el objetivo. " + Summary()
                    + (_reportedRepeatedEntry ? " Se detectaron reentradas repetidas al mismo refugio sin avance espacial." : ""), true);
        }
        catch (Exception error) { Finish("DIAGNOSTICO INVALIDO: " + error.Message, true); }
    }

    private static void TrackRepeatedEntry()
    {
        ScenarioZone zone = _hunter.SelectedSafeZone;
        Vector3 position = _hunter.transform.position;
        if (zone == _repeatZone && PlanarDistance(position, _repeatPosition) <= 1.5f) _nearbyReentries++;
        else { _repeatZone = zone; _repeatPosition = position; _nearbyReentries = 1; }
        if (_nearbyReentries < 8 || _reportedRepeatedEntry) return;
        _reportedRepeatedEntry = true;
        Debug.LogWarning("SALIDA: ocho entradas Flee alrededor de la misma puerta. Posible bucle; " +
            (_naturalMode ? "se sigue observando hasta completar dos ciclos o alcanzar el limite. "
                : "se sigue observando hasta completar la salida, captura o limite. ") + Summary());
    }

    private static string Snapshot(string state)
    {
        StringBuilder text = new StringBuilder("SALIDA t=" + Elapsed.ToString("F1") + "s " + state + " Flee#" + _fleeEntries);
        text.Append(" | Hunter ").Append(GridPosition(_hunter.transform.position));
        text.Append(" detecta=").Append(_hunter.DetectedWolf != null ? _hunter.DetectedWolf.name : "ninguno");
        text.Append(" refugio=").Append(_hunter.SelectedSafeZone != null ? _hunter.SelectedSafeZone.name : "ninguno");
        foreach (WolfAgent wolf in Wolves)
        {
            text.Append(" | ").Append(wolf.name).Append(' ').Append(GridPosition(wolf.transform.position));
            text.Append(' ').Append(wolf.CurrentStateName).Append(" ve=").Append(wolf.HasVisualContact);
            if (wolf.Navigation != null && wolf.Navigation.HasDestination)
                text.Append(" destino=").Append(GridPosition(wolf.Navigation.Destination));
        }
        return text.ToString();
    }

    private static float Elapsed => Time.time - _startedAt;
    private static string Summary() => "Tiempo=" + Elapsed.ToString("F1") + "s, entradasFlee=" + _fleeEntries
        + ", ciclos=" + (_hunter != null ? _hunter.CompletedCycles - _initialCycles : 0)
        + (_naturalMode ? "/2, capturas=" + (_hunter != null ? _hunter.DeathCount - _initialDeaths : 0) : "") + ".";
    private static string GridPosition(Vector3 position) => "(" + (position.x + DungeonLayout.Width * 0.5f).ToString("F2")
        + "," + (position.z + DungeonLayout.Depth * 0.5f).ToString("F2") + ")";
    private static float PlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private static void Finish(string result, bool error, bool warning = false)
    {
        Cleanup();
        if (error) Debug.LogError("Salida con lobos activos: " + result);
        else if (warning) Debug.LogWarning("Salida con lobos activos: " + result);
        else Debug.Log("Salida con lobos activos: " + result);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
    }
    private static void BeforeReload()
    {
        if (_running) Finish("Cancelado por recarga de scripts.", false, true);
    }
    private static void Cleanup()
    {
        EditorApplication.update -= Observe;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall -= StartRun;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        Time.timeScale = SessionState.GetFloat(TimeScaleKey, 1f);
        SessionState.EraseBool(PendingKey);
        SessionState.EraseBool(NaturalModeKey);
        SessionState.EraseFloat(TimeScaleKey);
        _running = false;
        Wolves.Clear();
        _hunter = null;
        _scenario = null;
    }
    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
        return found;
    }
    private static T FindSingle<T>(Scene scene) where T : Component
    {
        List<T> found = FindAll<T>(scene);
        Check(found.Count == 1, "Debe existir un unico " + typeof(T).Name + ".");
        return found[0];
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
