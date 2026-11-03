using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DungeonMapBuilder
{
    public const string ScenePath = "Assets/Proyect Ia/Scenes/DungeonSimulation.unity";
    private const string AssetsRoot = "Assets/Proyect Ia";
    private const string MaterialsPath = AssetsRoot + "/Materials/Dungeon";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private static readonly Color FloorColor = new Color(0.57f, 0.58f, 0.55f);
    private static readonly Color WallColor = new Color(0.15f, 0.19f, 0.23f);

    [MenuItem("Tools/IA/Entrega 2/Crear escenario de salas")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Debug.Log("El escenario ya existe. Abrilo desde Assets/Proyect Ia/Scenes; no se sobrescribio.");
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (!SceneManager.GetSceneAt(i).isDirty) continue;
            Debug.LogWarning("Guarda los cambios de la escena abierta antes de crear el escenario.");
            return;
        }

        GameObject boidPrefab = LoadPrefab("Boid_Prototype");
        GameObject hunterPrefab = LoadPrefab("Hunter_Prototype");
        GameObject interestPrefab = LoadPrefab("InterestObject_Prototype");
        int obstacleLayer = EnsureObstacleLayer();
        EnsureMaterialsFolder();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject scenario = new GameObject("DungeonScenario");
        ScenarioReferences references = scenario.AddComponent<ScenarioReferences>();
        Transform environment = CreateGroup("Environment", scenario.transform);
        Transform zones = CreateGroup("Zones", scenario.transform);
        Transform agents = CreateGroup("Agents", scenario.transform);
        Transform interests = CreateGroup("InterestObjects", scenario.transform);

        CreateGeometry(environment, obstacleLayer);
        ConfigureZones(references, zones);
        ConfigureAgents(references, agents, boidPrefab, hunterPrefab);
        CreateInitialInterests(interests, interestPrefab);
        CreateView();
        Physics.SyncTransforms();
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("No se pudo guardar el escenario. La escena queda abierta para revisarlo.");
        Selection.activeGameObject = scenario;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), 40f, true, true);
        Debug.Log("Escenario de salas creado: 6 Boids, 3 refugios, inicio, Coto y salida. Navegacion e IA nueva corresponden a los siguientes bloques.");
    }

    private static void CreateGeometry(Transform parent, int obstacleLayer)
    {
        Material floor = CreateMaterial("Floor", FloorColor);
        Material wall = CreateMaterial("Wall", WallColor);
        CreateBox("Ground", parent, Vector3.down * 0.15f, new Vector3(64f, 0.3f, 42f), floor);
        Transform obstacles = CreateGroup("Obstacles", parent);
        int index = 0;
        foreach (RectInt rectangle in DungeonLayout.GetSolidRectangles())
        {
            Vector3 center = DungeonLayout.ToWorld(rectangle.center.x, rectangle.center.y, 1.2f);
            GameObject block = CreateBox("Wall_" + index++, obstacles, center,
                new Vector3(rectangle.width, 2.4f, rectangle.height), wall);
            block.layer = obstacleLayer;
        }
        Transform labels = CreateGroup("RoomLabels", parent);
        CreateLabel("SALA OESTE", labels, DungeonLayout.ToWorld(8f, 17f, 2.43f), 8f, Color.white, 6f);
        CreateLabel("SALA B", labels, DungeonLayout.ToWorld(30f, 17f, 2.43f), 10f, Color.white, 6f);
        CreateLabel("SALA A", labels, DungeonLayout.ToWorld(30f, 38f, 0.08f), 10f, WallColor, 9f);
    }

    private static void ConfigureZones(ScenarioReferences references, Transform parent)
    {
        ScenarioZone start = CreateZone("Start", "INICIO", DungeonLayout.Start, ScenarioZoneType.Start,
            new Color(0.34f, 0.68f, 0.49f), parent);
        ScenarioZone hunting = CreateZone("HuntingGround", "COTO DE CAZA", DungeonLayout.HuntingGround,
            ScenarioZoneType.HuntingGround, new Color(0.84f, 0.65f, 0.30f), parent);
        ScenarioZone exit = CreateZone("Exit", "SALIDA", DungeonLayout.Exit, ScenarioZoneType.Exit,
            new Color(0.32f, 0.62f, 0.79f), parent);
        ScenarioZone[] safeZones = new ScenarioZone[DungeonLayout.SafeZones.Length];
        for (int i = 0; i < safeZones.Length; i++)
            safeZones[i] = CreateZone("SafeZone_" + (i + 1), i == 1 ? "REF. 2" : "REFUGIO " + (i + 1), DungeonLayout.SafeZones[i],
                ScenarioZoneType.Safe, new Color(0.67f, 0.48f, 0.77f), parent);

        WorldBounds bounds = hunting.gameObject.AddComponent<WorldBounds>();
        SerializedObject boundsData = new SerializedObject(bounds);
        // Medio metro de margen mantiene tambien el cuerpo de los Boids lejos del muro.
        boundsData.FindProperty("_width").floatValue = DungeonLayout.HuntingGround.width - 1f;
        boundsData.FindProperty("_depth").floatValue = DungeonLayout.HuntingGround.height - 1f;
        boundsData.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject data = new SerializedObject(references);
        data.FindProperty("_startZone").objectReferenceValue = start;
        data.FindProperty("_huntingZone").objectReferenceValue = hunting;
        data.FindProperty("_exitZone").objectReferenceValue = exit;
        data.FindProperty("_huntingBounds").objectReferenceValue = bounds;
        SerializedProperty safeArray = data.FindProperty("_safeZones");
        safeArray.arraySize = safeZones.Length;
        for (int i = 0; i < safeZones.Length; i++) safeArray.GetArrayElementAtIndex(i).objectReferenceValue = safeZones[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ScenarioZone CreateZone(string name, string label, RectInt area, ScenarioZoneType type, Color color, Transform parent)
    {
        GameObject zone = new GameObject(name);
        zone.transform.SetParent(parent, false);
        zone.transform.position = DungeonLayout.ToWorld(area.center.x, area.center.y);
        ScenarioZone component = zone.AddComponent<ScenarioZone>();
        SerializedObject data = new SerializedObject(component);
        data.FindProperty("_type").enumValueIndex = (int)type;
        data.FindProperty("_size").vector2Value = new Vector2(area.width, area.height);
        data.ApplyModifiedPropertiesWithoutUndo();
        Material material = CreateMaterial(type.ToString(), color);
        GameObject floor = CreateBox("Floor", zone.transform, zone.transform.position + Vector3.up * 0.015f,
            new Vector3(area.width, 0.03f, area.height), material);
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        float labelZ = type == ScenarioZoneType.HuntingGround ? area.yMax - 1.3f : area.yMin + 1.2f;
        CreateLabel(label, zone.transform, DungeonLayout.ToWorld(area.center.x, labelZ, 0.06f),
            type == ScenarioZoneType.HuntingGround ? 12f : 10f, new Color(0.08f, 0.12f, 0.16f), area.width);
        return component;
    }

    private static void ConfigureAgents(ScenarioReferences references, Transform parent, GameObject boidPrefab, GameObject hunterPrefab)
    {
        Transform boidsRoot = CreateGroup("Boids", parent);
        BoidAgent[] boids = new BoidAgent[6];
        for (int i = 0; i < boids.Length; i++)
        {
            GameObject boid = (GameObject)PrefabUtility.InstantiatePrefab(boidPrefab, boidsRoot);
            boid.name = "Boid_" + (i + 1);
            boid.transform.position = DungeonLayout.ToWorld(51.5f + i % 3 * 2.5f, 23f + i / 3 * 3f, 0.5f);
            boids[i] = boid.GetComponent<BoidAgent>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(boid.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(boid);
        }
        Transform spawn = CreateGroup("HunterSpawn", parent);
        spawn.position = DungeonLayout.ToWorld(6f, 4f, 0.75f);
        // El prefab de P1 es solo el cuerpo. Su FSM se integra en el bloque del Cazador.
        GameObject hunter = (GameObject)PrefabUtility.InstantiatePrefab(hunterPrefab, spawn);
        hunter.name = "HunterBody";
        hunter.transform.localPosition = Vector3.zero;
        hunter.transform.localRotation = Quaternion.identity;
        PrefabUtility.RecordPrefabInstancePropertyModifications(hunter.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(hunter);
        SerializedObject data = new SerializedObject(references);
        data.FindProperty("_hunterSpawn").objectReferenceValue = spawn;
        SerializedProperty array = data.FindProperty("_boids");
        array.arraySize = boids.Length;
        for (int i = 0; i < boids.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = boids[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateInitialInterests(Transform parent, GameObject prefab)
    {
        Vector3[] positions = { DungeonLayout.ToWorld(50f, 31f, 0.125f), DungeonLayout.ToWorld(58f, 19f, 0.125f) };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject interest = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            interest.name = "InterestObject_" + (i + 1);
            interest.transform.position = positions[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(interest.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(interest);
        }
    }

    private static void CreateView()
    {
        Camera camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 65f, 0f);
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = 23f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.075f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        Light light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(65f, -25f, 0f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.65f, 0.70f);
    }

    private static void CreateLabel(string text, Transform parent, Vector3 position, float size, Color color, float width)
    {
        TextMeshPro label = new GameObject(text, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(parent, false);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        label.text = text;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.rectTransform.sizeDelta = new Vector2(width, 1.5f);
        label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static Transform CreateGroup(string name, Transform parent)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static GameObject LoadPrefab(string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Prefabs/" + name + ".prefab");
        if (prefab == null) throw new InvalidOperationException("Falta el prefab " + name);
        return prefab;
    }

    private static void EnsureMaterialsFolder()
    {
        if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder(AssetsRoot + "/Materials", "Dungeon");
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = MaterialsPath + "/MAT_Dungeon_" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("No se encontro el shader URP Lit.");
        material = new Material(shader) { name = "MAT_Dungeon_" + name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.05f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static int EnsureObstacleLayer()
    {
        int existing = LayerMask.NameToLayer("MapObstacle");
        if (existing >= 0) return existing;
        SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tags.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue)) continue;
            layer.stringValue = "MapObstacle";
            tags.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }
        throw new InvalidOperationException("No hay una capa libre para MapObstacle.");
    }
}
