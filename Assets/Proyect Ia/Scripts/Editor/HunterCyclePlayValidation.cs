using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HunterCyclePlayValidation
{
    private const string PendingKey = "IA.HunterCyclePlay.Pending";
    private const string ControlledKey = "IA.HunterCyclePlay.Controlled";
    private enum Phase { Natural, Projectile, Flee, Capture, Respawn, Gathers, Exit }
    private static HunterAgent _hunter;
    private static ScenarioReferences _scenario;
    private static NavigationGraph _graph;
    private static readonly List<WolfAgent> Wolves = new List<WolfAgent>();
    private static readonly Dictionary<WolfAgent, bool> WolfEnabled = new Dictionary<WolfAgent, bool>();
    private static readonly Dictionary<BoidAgent, bool> BoidActive = new Dictionary<BoidAgent, bool>();
    private static WolfAgent _captureWolf;
    private static BoidAgent _shotTarget;
    private static float[] _wolfSpeeds;
    private static float _hunterSpeed;
    private static float _boidSpeed;
    private static float _shotHealth;
    private static float _shotTimer;
    private static Vector3 _spawn;
    private static Vector3 _waitingPosition;
    private static ScenarioZone _safeZone;
    private static double _phaseStarted;
    private static float _waitStarted;
    private static float _deathStarted;
    private static int _initialCaptures;
    private static int _initialDeaths;
    private static int _initialRespawns;
    private static int _initialCycles;
    private static int _previousGathers;
    private static int _requiredGathers;
    private static bool _sawFlee;
    private static bool _sawPath;
    private static bool _sawGather;
    private static bool _settingsSaved;
    private static bool _controlled;
    private static bool _running;
    private static Phase _phase;

    static HunterCyclePlayValidation()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        Subscribe();
        if (EditorApplication.isPlaying) EditorApplication.delayCall += StartRun;
    }

    [MenuItem("Tools/IA/Entrega 2/Probar huida natural del cazador en Play")]
    public static void RunNatural() => Begin(false);

    [MenuItem("Tools/IA/Entrega 2/Probar ciclo controlado del cazador en Play")]
    public static void RunControlled() => Begin(true);

    private static void Begin(bool controlled)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Inicia esta prueba desde Edit Mode.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath || scene.isDirty)
        {
            Debug.LogWarning("Abri DungeonSimulation guardada y sin cambios pendientes.");
            return;
        }
        Cleanup();
        SessionState.SetBool(PendingKey, true);
        SessionState.SetBool(ControlledKey, controlled);
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
        if (state == PlayModeStateChange.EnteredPlayMode) StartRun();
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            Cleanup();
            Debug.LogWarning("Prueba del ciclo cancelada al salir de Play.");
        }
    }

    private static void StartRun()
    {
        if (_running || !SessionState.GetBool(PendingKey, false)) return;
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            Check(scene.path == DungeonMapBuilder.ScenePath, "La escena activa cambio.");
            _hunter = FindSingle<HunterAgent>(scene);
            _scenario = FindSingle<ScenarioReferences>(scene);
            _graph = FindSingle<NavigationGraph>(scene);
            Wolves.AddRange(FindAll<WolfAgent>(scene));
            _spawn = _scenario.HunterSpawn.position;
            _controlled = SessionState.GetBool(ControlledKey, false);
            _waitStarted = -1f;
            _sawFlee = _sawPath = _sawGather = false;
            _running = true;
            if (_controlled) PrepareProjectileCase();
            else SetPhase(Phase.Natural);
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Observe()
    {
        if (!_running || !EditorApplication.isPlaying) return;
        try
        {
            Check(_hunter != null && _scenario != null && _graph != null, "Desaparecio una referencia de la prueba.");
            Check(SceneManager.GetActiveScene() == _scenario.gameObject.scene, "La escena activa cambio durante la prueba.");
            Check(EditorApplication.timeSinceStartup - _phaseStarted <= (_phase == Phase.Natural ? 90d : 60d),
                "La fase " + _phase + " supero su tiempo de observacion. Estado: " + _hunter.CurrentState?.GetType().Name);
            Check(Vector3.Distance(_spawn, _scenario.HunterSpawn.position) < 0.001f, "HunterSpawn se desplazo.");
            Physics.SyncTransforms();
            Check(_graph.CanTraverse(_hunter.transform.position, _hunter.transform.position, NavigationAccess.Hunter),
                "El Cazador abandono el espacio transitable.");
            switch (_phase)
            {
                case Phase.Natural: ObserveNatural(); break;
                case Phase.Projectile: ObserveProjectile(); break;
                case Phase.Flee: ObserveControlledFlee(); break;
                case Phase.Capture: ObserveCapture(); break;
                case Phase.Respawn: ObserveRespawn(); break;
                case Phase.Gathers: ObserveGathers(); break;
                case Phase.Exit: ObserveExit(); break;
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static void ObserveNatural()
    {
        if (_hunter.IsDead) { _waitStarted = -1f; return; }
        if (_hunter.CurrentState is HunterFleeState)
        {
            _sawFlee = true;
            _sawPath |= _hunter.Navigation.HasDestination && _hunter.Navigation.Follower.Path.Count > 1;
            if (!_hunter.IsWaitingInSafeZone) return;
            if (_waitStarted < 0f)
            {
                _waitStarted = Time.time;
                _waitingPosition = _hunter.transform.position;
                _safeZone = _hunter.SelectedSafeZone;
            }
            Check(_safeZone != null && _safeZone.Contains(_hunter.transform.position)
                && PlanarDistance(_hunter.transform.position, _waitingPosition) < 0.01f
                && !_hunter.Navigation.HasDestination && !_hunter.CanUseCombat,
                "Durante la espera debe permanecer en el refugio, sin movimiento ni combate.");
            return;
        }
        if (_waitStarted < 0f) return;
        Check(_sawFlee && _sawPath && Time.time - _waitStarted >= _hunter.SafeZoneWaitTime - 0.25f
            && (_hunter.CurrentState is HunterSearchState || _hunter.CurrentState is HunterExitState),
            "La huida debe usar A*, completar la espera y retomar Search o Exit.");
        string safeName = _safeZone.name;
        Complete("Huida NATURAL OK: percibio un lobo, navego con A* hasta " + safeName +
            ", espero SafeZoneWaitTime sin combatir y retomo su actividad. Play sigue activo.");
    }

    private static void PrepareProjectileCase()
    {
        Check(Wolves.Count >= 4 && _scenario.Boids.Count >= 3 && !_hunter.IsDead, "Faltan actores para el ciclo controlado.");
        _hunterSpeed = ReadFloat(_hunter, "_speed");
        _shotTarget = _scenario.Boids[0];
        _boidSpeed = ReadFloat(_shotTarget, "_maxSpeed");
        _captureWolf = Wolves[0];
        _wolfSpeeds = new[] { ReadFloat(_captureWolf, "_patrolSpeed"), ReadFloat(_captureWolf, "_chaseSpeed"), ReadFloat(_captureWolf, "_searchSpeed") };
        foreach (WolfAgent wolf in Wolves) WolfEnabled.Add(wolf, wolf.enabled);
        foreach (BoidAgent boid in _scenario.Boids) BoidActive.Add(boid, boid.gameObject.activeSelf);
        _settingsSaved = true;
        foreach (WolfAgent wolf in Wolves) wolf.enabled = false;
        foreach (BoidAgent boid in _scenario.Boids) boid.gameObject.SetActive(false);
        _hunter.Stop();
        _hunter.transform.position = CotoPoint();
        SetFloat(_hunter, "_speed", 0f);
        SetFloat(_shotTarget, "_maxSpeed", 0f);
        Vector3 shotTargetPosition = CotoPoint() + Vector3.right * 5f;
        shotTargetPosition.y = _shotTarget.transform.position.y;
        _shotTarget.transform.position = shotTargetPosition;
        _shotTarget.gameObject.SetActive(true);
        Physics.SyncTransforms();
        SetPhase(Phase.Projectile);
        Debug.Log("CICLO CONTROLADO: se aislaron temporalmente lobos y Boids; Cazador y un Boid vivo quedan quietos " +
            "en el Coto para observar Attack y su disparo real. No se asignan estados desde la prueba.");
    }

    private static void ObserveProjectile()
    {
        if (!_hunter.HasActiveProjectile) return;
        Check(_hunter.CurrentState is HunterAttackState, "El disparo debe producirlo el estado Attack.");
        List<HunterProjectile> projectiles = FindAll<HunterProjectile>(_hunter.gameObject.scene);
        Check(projectiles.Count == 1, "Debe haber un unico proyectil de la fixture.");
        // Retener esta instancia permite observar la cancelacion sin depender de la tasa de dibujo del Editor.
        SetFloat(projectiles[0], "_speed", 0f);
        SetFloat(projectiles[0], "_lifeTime", 5f);
        _shotHealth = ReadFloat(_shotTarget, "_currentHealth");
        _shotTimer = ReadFloat(_hunter, "_attackTimer");
        SetFloat(_hunter, "_speed", _hunterSpeed);
        SetWolfSpeeds(0f, 0f, 0f);
        _captureWolf.Stop();
        _captureWolf.transform.position = _hunter.transform.position - Vector3.right * 4f;
        _captureWolf.transform.forward = Vector3.right;
        _captureWolf.enabled = true;
        Physics.SyncTransforms();
        SetPhase(Phase.Flee);
        Debug.Log("CICLO CONTROLADO: disparo retenido brevemente; se habilito un lobo visible para que la propia FSM interrumpa Attack con Flee.");
    }

    private static void ObserveControlledFlee()
    {
        if (!(_hunter.CurrentState is HunterFleeState)) return;
        Check(!_hunter.HasActiveProjectile && _hunter.Target == null && _hunter.GatherProgress == 0f
            && Mathf.Abs(ReadFloat(_shotTarget, "_currentHealth") - _shotHealth) < 0.01f
            && ReadFloat(_hunter, "_attackTimer") >= _shotTimer,
            "Flee debe cancelar el disparo sin dano ni reinicio de TBA.");
        if (_captureWolf.CurrentStateName != "WolfChaseState" || !_captureWolf.HasVisualContact) return;
        _initialCaptures = _captureWolf.CaptureCount;
        _initialDeaths = _hunter.DeathCount;
        _initialRespawns = _hunter.RespawnCount;
        Vector3 direction = _hunter.Velocity;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.left;
        direction.Normalize();
        _captureWolf.Stop();
        _captureWolf.transform.position = _hunter.transform.position + direction * 0.5f;
        _captureWolf.transform.forward = -direction;
        SetWolfSpeeds(_wolfSpeeds[0], _wolfSpeeds[1], _wolfSpeeds[2]);
        Physics.SyncTransforms();
        SetPhase(Phase.Capture);
        Debug.Log("CICLO CONTROLADO: comprobada la cancelacion, se acerco una vez el lobo a 0.5 m durante Flee. La captura queda a cargo del lobo.");
    }

    private static void ObserveCapture()
    {
        if (!_hunter.IsDead) return;
        Check(_captureWolf.CaptureCount == _initialCaptures + 1 && _hunter.DeathCount == _initialDeaths + 1,
            "La interrupcion de Flee debe provenir de una captura real del lobo.");
        Check(!_hunter.IsAvailableToWolves && !_hunter.GetComponent<Renderer>().enabled
            && !_hunter.GetComponent<Collider>().enabled, "Dead debe ocultar y retirar el cuerpo del Cazador.");
        _deathStarted = Time.time;
        SetPhase(Phase.Respawn);
    }

    private static void ObserveRespawn()
    {
        if (_hunter.IsDead)
        {
            Check(!_hunter.Navigation.HasDestination && !_hunter.HasActiveProjectile && !_hunter.CanUseCombat,
                "Dead no debe continuar la navegacion ni el combate.");
            return;
        }
        if (_hunter.RespawnCount <= _initialRespawns) return;
        Check(_hunter.RespawnCount == _initialRespawns + 1 && _hunter.GatherCount == 0
            && _hunter.CurrentState is HunterSearchState && PlanarDistance(_hunter.transform.position, _spawn) < 1f
            && Time.time - _deathStarted >= _hunter.RespawnTime - 0.25f
            && _hunter.GetComponent<Renderer>().enabled && _hunter.GetComponent<Collider>().enabled,
            "Debe cumplir RespawnTime y reaparecer en Search con cuerpo y contador restaurados.");
        PrepareGathers();
    }

    private static void PrepareGathers()
    {
        foreach (WolfAgent wolf in Wolves) wolf.enabled = false;
        foreach (BoidAgent boid in _scenario.Boids) boid.gameObject.SetActive(false);
        SetFloat(_shotTarget, "_maxSpeed", _boidSpeed);
        SetFloat(_hunter, "_speed", _hunterSpeed);
        _hunter.Stop();
        _hunter.transform.position = CotoPoint();
        _requiredGathers = _hunter.RequiredGathers;
        Check(_requiredGathers > 0 && _requiredGathers <= _scenario.Boids.Count, "No hay suficientes Boids para preparar la cuota configurada.");
        for (int i = 0; i < _requiredGathers; i++)
        {
            BoidAgent boid = _scenario.Boids[i];
            float angle = i * Mathf.PI * 2f / _requiredGathers;
            Vector3 position = CotoPoint() + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.8f;
            position.y = boid.transform.position.y;
            boid.transform.position = position;
            boid.gameObject.SetActive(true);
            boid.TakeDamage(float.MaxValue);
        }
        _initialCycles = _hunter.CompletedCycles;
        _initialDeaths = _hunter.DeathCount;
        _initialRespawns = _hunter.RespawnCount;
        _previousGathers = 0;
        _sawGather = _sawPath = false;
        Physics.SyncTransforms();
        SetPhase(Phase.Gathers);
        Debug.Log("CICLO CONTROLADO: se prepararon " + _requiredGathers + " Boids eliminados en el Coto y se pausaron los lobos. " +
            "El contador no se modifica: deben ejecutarse Gather reales y luego Exit mediante A*.");
    }

    private static void ObserveGathers()
    {
        _sawGather |= _hunter.CurrentState is HunterGatherState;
        Check(_hunter.GatherCount >= _previousGathers && _hunter.GatherCount <= _requiredGathers
            && !(_hunter.CurrentState is HunterFleeState) && !_hunter.IsDead,
            "La fixture sin amenazas debe acumular recolecciones reales sin reiniciar el ciclo.");
        _previousGathers = _hunter.GatherCount;
        if (!(_hunter.CurrentState is HunterExitState)) return;
        Check(_sawGather && _hunter.GatherCount == _requiredGathers, "Exit debe comenzar solo despues de completar la cuota mediante Gather.");
        SetPhase(Phase.Exit);
    }

    private static void ObserveExit()
    {
        if (_hunter.CompletedCycles <= _initialCycles)
        {
            Check(_hunter.CurrentState is HunterExitState && !_hunter.CanUseCombat && _hunter.Target == null,
                "Mientras va a la salida debe mantenerse en Exit, sin atacar ni recolectar.");
            _sawPath |= _hunter.Navigation.HasDestination && _hunter.Navigation.Follower.Path.Count > 1;
            return;
        }
        Check(_sawPath && _hunter.CompletedCycles == _initialCycles + 1 && _hunter.GatherCount == 0
            && _hunter.DeathCount == _initialDeaths && _hunter.RespawnCount == _initialRespawns
            && _hunter.CurrentState is HunterSearchState && _hunter.IsAvailableToWolves
            && PlanarDistance(_hunter.transform.position, _spawn) < 1f,
            "La salida debe completar un ciclo, volver al inicio y reiniciar Search sin contar otra muerte.");
        Complete("Ciclo CONTROLADO OK: Attack fue interrumpido por Flee sin impacto del proyectil; el lobo capturo durante Flee; " +
            "se verificaron Dead y reaparicion; luego " + _requiredGathers + " Gather reales activaron Exit y un reinicio exitoso. " +
            "Se detiene Play para descartar todas las preparaciones.");
    }

    private static void SetPhase(Phase phase) { _phase = phase; _phaseStarted = EditorApplication.timeSinceStartup; }
    private static Vector3 CotoPoint() => DungeonLayout.ToWorld(55.5f, 25.5f, _hunter.transform.position.y);
    private static void SetWolfSpeeds(float patrol, float chase, float search)
    {
        SetFloat(_captureWolf, "_patrolSpeed", patrol);
        SetFloat(_captureWolf, "_chaseSpeed", chase);
        SetFloat(_captureWolf, "_searchSpeed", search);
    }

    private static void Complete(string message)
    {
        bool stopPlay = _controlled;
        Cleanup();
        Debug.Log(message);
        if (stopPlay) EditorApplication.isPlaying = false;
    }
    private static void Fail(Exception error)
    {
        bool stopPlay = _controlled;
        Cleanup();
        Debug.LogError("Prueba del ciclo del Cazador: " + error.Message);
        if (stopPlay) EditorApplication.isPlaying = false;
    }
    private static void BeforeReload()
    {
        if (!_running) return;
        Fail(new InvalidOperationException("Prueba cancelada por recarga de scripts."));
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Observe;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall -= StartRun;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        SessionState.EraseBool(PendingKey);
        SessionState.EraseBool(ControlledKey);
        if (_settingsSaved)
        {
            if (_hunter != null) SetFloat(_hunter, "_speed", _hunterSpeed);
            if (_shotTarget != null) SetFloat(_shotTarget, "_maxSpeed", _boidSpeed);
            if (_captureWolf != null && _wolfSpeeds != null) SetWolfSpeeds(_wolfSpeeds[0], _wolfSpeeds[1], _wolfSpeeds[2]);
            foreach (KeyValuePair<WolfAgent, bool> pair in WolfEnabled) if (pair.Key != null) pair.Key.enabled = pair.Value;
            foreach (KeyValuePair<BoidAgent, bool> pair in BoidActive)
                if (pair.Key != null && pair.Key.gameObject.activeSelf != pair.Value) pair.Key.gameObject.SetActive(pair.Value);
        }
        _settingsSaved = _running = false;
        Wolves.Clear();
        WolfEnabled.Clear();
        BoidActive.Clear();
        _hunter = null;
        _scenario = null;
        _graph = null;
        _captureWolf = null;
        _shotTarget = null;
        _safeZone = null;
        _wolfSpeeds = null;
    }

    private static float ReadFloat(Component component, string field)
    {
        using (SerializedObject data = new SerializedObject(component)) return data.FindProperty(field).floatValue;
    }
    private static void SetFloat(Component component, string field, float value)
    {
        using (SerializedObject data = new SerializedObject(component))
        {
            data.FindProperty(field).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }
    private static T FindSingle<T>(Scene scene) where T : Component
    {
        List<T> result = FindAll<T>(scene);
        Check(result.Count == 1, "Debe existir un unico " + typeof(T).Name + ".");
        return result[0];
    }
    private static float PlanarDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
