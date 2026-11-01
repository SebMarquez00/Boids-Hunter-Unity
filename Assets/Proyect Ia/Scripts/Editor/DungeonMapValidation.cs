using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonMapValidation
{
    private const float AgentRadius = 0.38f;
    private const float TestHeight = 0.8f;
    private const float BoidMargin = 0.5f;
    private static readonly Vector2Int[] Directions =
        { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    private static ScenarioReferences _monitoredScenario;
    private static List<BoidAgent> _monitoredBoids;
    private static double _monitorStarted;
    private static int _samples;

    [MenuItem("Tools/IA/Entrega 2/Validar escenario")]
    public static void Run()
    {
        ScenarioReferences scenario = GetScenario();
        int obstacleLayer = LayerMask.NameToLayer("MapObstacle");
        Require(obstacleLayer >= 0, "Falta la capa MapObstacle.");
        int obstacleMask = 1 << obstacleLayer;
        Physics.SyncTransforms();
        ValidateConfiguration(scenario);
        ValidateHuntingBounds(scenario, obstacleMask);
        int reachableCells = ValidateConnectivity(scenario, obstacleMask);
        foreach (ScenarioZone safe in scenario.SafeZones) ValidateSingleEntrance(safe, obstacleMask);
        ValidateColumn(2.5f, 13.5f, obstacleMask);
        ValidateColumn(24.5f, 35.5f, obstacleMask);
        Debug.Log($"Escenario correcto: {scenario.Boids.Count} Boids dentro del Coto, " +
            $"{scenario.SafeZones.Count} refugios con una entrada, {reachableCells} celdas conectadas y columnas que bloquean LoS.");
    }

    private static ScenarioReferences GetScenario()
    {
        Scene scene = SceneManager.GetActiveScene();
        Require(scene.path == DungeonMapBuilder.ScenePath, "Abri DungeonSimulation antes de validar el escenario.");
        ScenarioReferences found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (ScenarioReferences candidate in root.GetComponentsInChildren<ScenarioReferences>(true))
            {
                Require(found == null, "La escena tiene mas de un ScenarioReferences.");
                found = candidate;
            }
        }
        Require(found != null, "Falta ScenarioReferences en la escena activa.");
        return found;
    }

    private static void ValidateConfiguration(ScenarioReferences scenario)
    {
        Require(scenario.StartZone != null && scenario.HuntingZone != null && scenario.ExitZone != null,
            "Faltan referencias de inicio, Coto o salida.");
        Require(scenario.HunterSpawn != null && scenario.HuntingBounds != null, "Faltan el inicio del Cazador o los bounds.");
        Require(scenario.HunterSpawn.gameObject.scene == scenario.gameObject.scene
            && scenario.HuntingBounds.gameObject.scene == scenario.gameObject.scene, "El inicio y los bounds deben pertenecer a esta escena.");
        Require(scenario.StartZone.Type == ScenarioZoneType.Start && scenario.HuntingZone.Type == ScenarioZoneType.HuntingGround
            && scenario.ExitZone.Type == ScenarioZoneType.Exit, "Los tipos de las zonas principales no corresponden a sus referencias.");
        Require(scenario.SafeZones != null && scenario.SafeZones.Count == 3, "Este escenario debe tener sus tres refugios configurados.");
        HashSet<ScenarioZone> zones = new HashSet<ScenarioZone> { scenario.StartZone, scenario.HuntingZone, scenario.ExitZone };
        foreach (ScenarioZone safe in scenario.SafeZones)
            Require(safe != null && safe.Type == ScenarioZoneType.Safe && zones.Add(safe), "Hay un refugio ausente, repetido o de tipo incorrecto.");
        foreach (ScenarioZone zone in zones)
            Require(zone.gameObject.scene == scenario.gameObject.scene && zone.Size.x > 0f && zone.Size.y > 0f,
                "Las zonas deben pertenecer a esta escena y tener tamanos positivos.");
        Require(scenario.StartZone.Contains(scenario.HunterSpawn.position), "El Cazador no inicia dentro de la zona inicial.");
        Require(scenario.Boids != null && scenario.Boids.Count >= 6, "Faltan referencias: se requieren al menos seis Boids.");
        HashSet<BoidAgent> boids = new HashSet<BoidAgent>();
        foreach (BoidAgent boid in scenario.Boids)
        {
            Require(boid != null && boids.Add(boid), "Hay referencias de Boids ausentes o repetidas.");
            Require(boid.gameObject.scene == scenario.gameObject.scene, "Un Boid pertenece a otra escena.");
            Require(InsideWithMargin(scenario.HuntingZone, boid.transform.position, BoidMargin),
                boid.name + " esta fuera del Coto o invade su margen de medio metro.");
        }
    }

    private static void ValidateHuntingBounds(ScenarioReferences scenario, int mask)
    {
        WorldBounds bounds = scenario.HuntingBounds;
        Vector3 center = bounds.transform.position;
        Vector3 zoneCenter = scenario.HuntingZone.transform.position;
        Require(Mathf.Abs(center.x - zoneCenter.x) < 0.01f && Mathf.Abs(center.z - zoneCenter.z) < 0.01f,
            "Los bounds deben estar centrados en el Coto.");
        float width;
        float depth;
        using (SerializedObject data = new SerializedObject(bounds))
        {
            width = data.FindProperty("_width").floatValue;
            depth = data.FindProperty("_depth").floatValue;
        }
        Require(width > 0f && depth > 0f, "Los bounds deben tener dimensiones positivas.");
        // Se comprueban los extremos donde puede aterrizar el wrap, no solo los spawns.
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i <= Mathf.CeilToInt(width); i++)
                ValidateBoundaryPoint(center + new Vector3(Mathf.Lerp(-width / 2f, width / 2f, i / Mathf.Ceil(width)), 0f, side * depth / 2f), scenario.HuntingZone, mask);
            for (int i = 0; i <= Mathf.CeilToInt(depth); i++)
                ValidateBoundaryPoint(center + new Vector3(side * width / 2f, 0f, Mathf.Lerp(-depth / 2f, depth / 2f, i / Mathf.Ceil(depth))), scenario.HuntingZone, mask);
        }
    }

    private static void ValidateBoundaryPoint(Vector3 point, ScenarioZone huntingZone, int mask)
    {
        Require(InsideWithMargin(huntingZone, point, BoidMargin), "Los bounds dejan menos de medio metro entre el Boid y el borde del Coto.");
        point.y = TestHeight;
        Require(!Physics.CheckSphere(point, BoidMargin - 0.01f, mask, QueryTriggerInteraction.Ignore),
            "Un extremo de los bounds coloca el cuerpo del Boid dentro de un muro.");
    }

    private static int ValidateConnectivity(ScenarioReferences scenario, int mask)
    {
        bool[,] free = new bool[DungeonLayout.Width, DungeonLayout.Depth];
        bool[,] visited = new bool[DungeonLayout.Width, DungeonLayout.Depth];
        int freeCells = 0;
        for (int x = 0; x < DungeonLayout.Width; x++)
            for (int z = 0; z < DungeonLayout.Depth; z++)
            {
                free[x, z] = !Physics.CheckSphere(CellPosition(new Vector2Int(x, z)), AgentRadius, mask, QueryTriggerInteraction.Ignore);
                if (free[x, z]) freeCells++;
            }

        Vector2Int start = WorldCell(scenario.HunterSpawn.position);
        Require(InGrid(start) && free[start.x, start.y], "El inicio del Cazador esta bloqueado por la geometria.");
        Queue<Vector2Int> pending = new Queue<Vector2Int>();
        pending.Enqueue(start);
        visited[start.x, start.y] = true;
        int reached = 0;
        while (pending.Count > 0)
        {
            Vector2Int current = pending.Dequeue();
            reached++;
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;
                if (!InGrid(next) || visited[next.x, next.y] || !free[next.x, next.y]) continue;
                if (!ClearSegment(CellPosition(current), CellPosition(next), mask)) continue;
                visited[next.x, next.y] = true;
                pending.Enqueue(next);
            }
        }
        List<ScenarioZone> destinations = new List<ScenarioZone> { scenario.StartZone, scenario.HuntingZone, scenario.ExitZone };
        foreach (ScenarioZone safe in scenario.SafeZones) destinations.Add(safe);
        foreach (ScenarioZone zone in destinations)
        {
            Vector2Int cell = WorldCell(zone.transform.position);
            Require(InGrid(cell) && visited[cell.x, cell.y], "No existe un recorrido fisicamente libre hasta " + zone.name + ".");
        }
        Require(reached == freeCells, "La geometria contiene sectores transitables aislados del inicio.");
        return reached;
    }

    private static void ValidateSingleEntrance(ScenarioZone zone, int mask)
    {
        List<bool> openings = new List<bool>();
        float halfX = zone.Size.x * 0.5f;
        float halfZ = zone.Size.y * 0.5f;
        int stepsX = Mathf.CeilToInt(zone.Size.x);
        int stepsZ = Mathf.CeilToInt(zone.Size.y);
        for (int i = 0; i < stepsX; i++) openings.Add(OpenAt(zone, new Vector3(-halfX + (i + 0.5f) * zone.Size.x / stepsX, 0f, -halfZ), Vector3.back, mask));
        for (int i = 0; i < stepsZ; i++) openings.Add(OpenAt(zone, new Vector3(halfX, 0f, -halfZ + (i + 0.5f) * zone.Size.y / stepsZ), Vector3.right, mask));
        for (int i = 0; i < stepsX; i++) openings.Add(OpenAt(zone, new Vector3(halfX - (i + 0.5f) * zone.Size.x / stepsX, 0f, halfZ), Vector3.forward, mask));
        for (int i = 0; i < stepsZ; i++) openings.Add(OpenAt(zone, new Vector3(-halfX, 0f, halfZ - (i + 0.5f) * zone.Size.y / stepsZ), Vector3.left, mask));
        int bands = 0;
        for (int i = 0; i < openings.Count; i++)
            if (openings[i] && !openings[(i + openings.Count - 1) % openings.Count]) bands++;
        Require(bands == 1, zone.name + " debe tener una sola banda de puerta: se detectaron " + bands + ".");
    }

    private static bool OpenAt(ScenarioZone zone, Vector3 localPoint, Vector3 localNormal, int mask)
    {
        Vector3 point = zone.transform.TransformPoint(localPoint);
        point.y = TestHeight;
        Vector3 normal = zone.transform.TransformDirection(localNormal).normalized;
        Vector3 inside = point - normal * 0.5f;
        Vector3 outside = point + normal * 0.5f;
        return !Physics.CheckSphere(inside, AgentRadius, mask, QueryTriggerInteraction.Ignore)
            && !Physics.CheckSphere(outside, AgentRadius, mask, QueryTriggerInteraction.Ignore)
            && ClearSegment(inside, outside, mask);
    }

    private static void ValidateColumn(float fromX, float toX, int mask)
    {
        Vector3 from = DungeonLayout.ToWorld(fromX, 17.5f, TestHeight);
        Vector3 to = DungeonLayout.ToWorld(toX, 17.5f, TestHeight);
        Require(!Physics.CheckSphere(from, AgentRadius, mask, QueryTriggerInteraction.Ignore)
            && !Physics.CheckSphere(to, AgentRadius, mask, QueryTriggerInteraction.Ignore), "Los puntos de prueba junto a una columna estan bloqueados.");
        Require(Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore), "Una columna no bloquea la linea de vision.");
        from = DungeonLayout.ToWorld(fromX, 13.5f, TestHeight);
        to = DungeonLayout.ToWorld(toX, 13.5f, TestHeight);
        Require(!Physics.Linecast(from, to, mask, QueryTriggerInteraction.Ignore), "El paso inferior de una columna esta bloqueado.");
    }

    private static bool ClearSegment(Vector3 from, Vector3 to, int mask)
    {
        Vector3 direction = to - from;
        return !Physics.SphereCast(from, AgentRadius, direction.normalized, out _, direction.magnitude, mask, QueryTriggerInteraction.Ignore);
    }

    private static Vector2Int WorldCell(Vector3 world)
    {
        Vector3 origin = DungeonLayout.ToWorld(0f, 0f);
        return new Vector2Int(Mathf.FloorToInt(world.x - origin.x), Mathf.FloorToInt(world.z - origin.z));
    }

    private static Vector3 CellPosition(Vector2Int cell) => DungeonLayout.ToWorld(cell.x + 0.5f, cell.y + 0.5f, TestHeight);
    private static bool InGrid(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < DungeonLayout.Width && cell.y < DungeonLayout.Depth;

    private static bool InsideWithMargin(ScenarioZone zone, Vector3 position, float margin)
    {
        Vector3 local = zone.transform.InverseTransformPoint(position);
        float scaleX = zone.transform.TransformVector(Vector3.right).magnitude;
        float scaleZ = zone.transform.TransformVector(Vector3.forward).magnitude;
        return scaleX > 0f && scaleZ > 0f
            && Mathf.Abs(local.x) + margin / scaleX <= zone.Size.x * 0.5f + 0.001f
            && Mathf.Abs(local.z) + margin / scaleZ <= zone.Size.y * 0.5f + 0.001f;
    }

    [MenuItem("Tools/IA/Entrega 2/Comprobar Boids en Play")]
    public static void BeginBoidCheck()
    {
        Require(EditorApplication.isPlaying && !EditorApplication.isPaused, "Inicia Play sin pausa para observar a los Boids.");
        ScenarioReferences scenario = GetScenario();
        ValidateConfiguration(scenario);
        StopBoidCheck();
        _monitoredScenario = scenario;
        _monitoredBoids = new List<BoidAgent>(scenario.Boids);
        _monitorStarted = EditorApplication.timeSinceStartup;
        _samples = 0;
        EditorApplication.update += SampleBoids;
        Debug.Log("Muestreando posiciones de Boids durante 15 segundos de Play.");
    }

    private static void SampleBoids()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || _monitoredScenario == null
            || _monitoredScenario.HuntingZone == null || SceneManager.GetActiveScene() != _monitoredScenario.gameObject.scene)
        {
            StopBoidCheck();
            Debug.Log("Muestreo de Boids cancelado: Play se detuvo, se pauso o cambio la escena.");
            return;
        }
        foreach (BoidAgent boid in _monitoredBoids)
        {
            if (boid == null || (boid.gameObject.activeInHierarchy && !InsideWithMargin(_monitoredScenario.HuntingZone, boid.transform.position, BoidMargin)))
            {
                string problem = boid == null ? "Desaparecio un Boid referenciado." : boid.name + " salio del Coto o de su margen fisico.";
                StopBoidCheck();
                Debug.LogError(problem);
                return;
            }
        }
        _samples++;
        if (EditorApplication.timeSinceStartup - _monitorStarted < 15d) return;
        int samples = _samples;
        StopBoidCheck();
        Debug.Log($"Muestreo completado: {samples} lecturas durante 15 segundos; ninguna salida del Coto observada.");
    }

    private static void StopBoidCheck()
    {
        EditorApplication.update -= SampleBoids;
        _monitoredScenario = null;
        _monitoredBoids = null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Escenario: " + message);
    }
}
