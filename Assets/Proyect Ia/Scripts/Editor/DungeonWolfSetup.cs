using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DungeonWolfSetup
{
    private const string MaterialsPath = "Assets/Proyect Ia/Materials/Dungeon";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string HowlPath = "Assets/Proyect Ia/Audio/WolfHowl.wav";
    private static readonly Vector2[][] Patrols =
    {
        new[] { new Vector2(2.5f, 13.5f), new Vector2(13.5f, 13.5f), new Vector2(13.5f, 20.5f), new Vector2(2.5f, 20.5f) },
        new[] { new Vector2(24.5f, 30.5f), new Vector2(35.5f, 30.5f), new Vector2(35.5f, 37.5f), new Vector2(24.5f, 37.5f) },
        new[] { new Vector2(24.5f, 13.5f), new Vector2(35.5f, 13.5f), new Vector2(35.5f, 20.5f), new Vector2(24.5f, 20.5f) },
        new[] { new Vector2(29.5f, 22.5f), new Vector2(29.5f, 28.5f) }
    };

    [MenuItem("Tools/IA/Entrega 2/Agregar lobos al escenario")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath || scene.isDirty)
        {
            Debug.LogWarning("Abri DungeonSimulation guardada y sin cambios pendientes antes de agregar los lobos.");
            return;
        }
        ScenarioReferences scenario = GetSingle<ScenarioReferences>(scene);
        NavigationGraph graph = GetSingle<NavigationGraph>(scene);
        HunterAgent hunter = GetSingle<HunterAgent>(scene);
        AudioClip howl = AssetDatabase.LoadAssetAtPath<AudioClip>(HowlPath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (howl == null || font == null) throw new InvalidOperationException("Faltan WolfHowl.wav o la fuente LiberationSans SDF.");
        if (scenario.HunterSpawn == null || scenario.HunterSpawn.parent == null)
            throw new InvalidOperationException("Falta HunterSpawn bajo Agents.");

        Material bodyMaterial = GetMaterial("WolfBody", new Color(0.38f, 0.53f, 0.68f));
        Material detailMaterial = GetMaterial("WolfDetail", new Color(0.08f, 0.1f, 0.13f));
        Material debugMaterial = GetMaterial("WolfDebug", Color.white);
        Material labelMaterial = GetLabelMaterial(font);
        Transform wolves = GetChild(scenario.HunterSpawn.parent, "Wolves");
        WolfPack pack = GetOrAdd<WolfPack>(wolves.gameObject);
        Transform routes = GetChild(wolves, "PatrolRoutes");
        for (int i = 0; i < Patrols.Length; i++)
        {
            string id = "L" + (i + 1);
            Transform[] waypoints = ConfigureWaypoints(GetChild(routes, id + "Route"), Patrols[i]);
            GameObject body = GetChild(wolves, id).gameObject;
            body.transform.position = waypoints[0].position;
            body.transform.rotation = Quaternion.LookRotation(waypoints[1].position - waypoints[0].position, Vector3.up);
            body.transform.localScale = Vector3.one;
            ConfigureBody(body, bodyMaterial, detailMaterial);
            AgentNavigator navigation = ConfigureNavigation(body, graph);
            WolfAgent wolf = GetOrAdd<WolfAgent>(body);
            ConfigureWolf(wolf, navigation, hunter, scenario, pack, waypoints);
            ConfigureFeedback(wolf, id, debugMaterial, labelMaterial, font, howl);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se pudo guardar la incorporacion de los lobos.");
        Debug.Log("L1-L4 configurados sin duplicados: patrullas, FoV/LoS, aullidos, memoria compartida y A* con perfil Wolf.");
    }

    private static Transform[] ConfigureWaypoints(Transform parent, Vector2[] points)
    {
        Transform[] waypoints = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            waypoints[i] = GetChild(parent, "P" + (i + 1));
            waypoints[i].position = DungeonLayout.ToWorld(points[i].x, points[i].y, 0.75f);
        }
        return waypoints;
    }

    private static void ConfigureBody(GameObject body, Material bodyMaterial, Material detailMaterial)
    {
        CapsuleCollider collider = GetOrAdd<CapsuleCollider>(body);
        collider.radius = 0.35f;
        collider.height = 1.5f;
        collider.center = Vector3.zero;
        collider.direction = 1;
        collider.isTrigger = false;
        ConfigureVisual(body.transform, "Body", PrimitiveType.Capsule, Vector3.zero,
            new Vector3(0.7f, 0.75f, 0.7f), bodyMaterial);
        ConfigureVisual(body.transform, "Nose", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0.32f),
            new Vector3(0.18f, 0.16f, 0.26f), detailMaterial);
    }

    private static void ConfigureVisual(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
    {
        Transform visual = ExistingChild(parent, name);
        if (visual == null)
        {
            GameObject created = GameObject.CreatePrimitive(primitive);
            created.name = name;
            created.transform.SetParent(parent, false);
            visual = created.transform;
        }
        visual.localPosition = position;
        visual.localRotation = Quaternion.identity;
        visual.localScale = scale;
        Collider collider = visual.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        Renderer renderer = visual.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static AgentNavigator ConfigureNavigation(GameObject body, NavigationGraph graph)
    {
        PathFollower follower = GetOrAdd<PathFollower>(body);
        follower.Graph = graph;
        follower.Access = NavigationAccess.Wolf;
        follower.Speed = 2.5f;
        AgentNavigator navigation = GetOrAdd<AgentNavigator>(body);
        navigation.Follower = follower;
        using (SerializedObject data = new SerializedObject(navigation))
        {
            data.FindProperty("_turnSpeed").floatValue = 360f;
            data.FindProperty("_repathInterval").floatValue = 0.5f;
            data.FindProperty("_destinationThreshold").floatValue = 0.25f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        return navigation;
    }

    private static void ConfigureWolf(WolfAgent wolf, AgentNavigator navigation, HunterAgent hunter,
        ScenarioReferences scenario, WolfPack pack, Transform[] waypoints)
    {
        using (SerializedObject data = new SerializedObject(wolf))
        {
            data.FindProperty("_navigation").objectReferenceValue = navigation;
            data.FindProperty("_hunter").objectReferenceValue = hunter;
            data.FindProperty("_scenario").objectReferenceValue = scenario;
            data.FindProperty("_pack").objectReferenceValue = pack;
            SerializedProperty points = data.FindProperty("_waypoints");
            points.arraySize = waypoints.Length;
            for (int i = 0; i < waypoints.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            data.FindProperty("_patrolSpeed").floatValue = 2.5f;
            data.FindProperty("_chaseSpeed").floatValue = 3.4f;
            data.FindProperty("_searchSpeed").floatValue = 2.8f;
            data.FindProperty("_viewRadius").floatValue = 9f;
            data.FindProperty("_viewAngle").floatValue = 110f;
            data.FindProperty("_captureRadius").floatValue = 0.85f;
            data.FindProperty("_perceptionInterval").floatValue = 0.1f;
            data.FindProperty("_searchRadius").floatValue = 6f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureFeedback(WolfAgent wolf, string id, Material material, Material labelMaterial,
        TMP_FontAsset font, AudioClip howl)
    {
        LineRenderer path = ConfigureLine(wolf.transform, "Path", material, 0.06f, false);
        LineRenderer view = ConfigureLine(wolf.transform, "ViewContour", material, 0.035f, false);
        LineRenderer memory = ConfigureLine(wolf.transform, "LastKnownPosition", material, 0.07f, true);
        TextMeshPro label = GetOrAdd<TextMeshPro>(GetChild(wolf.transform, "WolfLabel").gameObject);
        label.font = font;
        label.fontSharedMaterial = labelMaterial;
        label.fontSize = 6.5f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.text = id + " | Patrulla";
        label.rectTransform.sizeDelta = new Vector2(6f, 1.8f);
        label.transform.position = wolf.transform.position + new Vector3(0f, 1.05f, 1.1f);
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        AudioSource audio = GetOrAdd<AudioSource>(wolf.gameObject);
        audio.clip = howl;
        audio.playOnAwake = false;
        audio.loop = false;
        audio.volume = 0.15f;
        audio.spatialBlend = 0f;
        WolfFeedback feedback = GetOrAdd<WolfFeedback>(wolf.gameObject);
        using (SerializedObject data = new SerializedObject(feedback))
        {
            data.FindProperty("_wolf").objectReferenceValue = wolf;
            data.FindProperty("_bodyRenderer").objectReferenceValue = ExistingChild(wolf.transform, "Body").GetComponent<Renderer>();
            data.FindProperty("_label").objectReferenceValue = label;
            data.FindProperty("_pathLine").objectReferenceValue = path;
            data.FindProperty("_viewLine").objectReferenceValue = view;
            data.FindProperty("_lastKnownLine").objectReferenceValue = memory;
            data.FindProperty("_howlAudio").objectReferenceValue = audio;
            data.FindProperty("_id").stringValue = id;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static LineRenderer ConfigureLine(Transform parent, string name, Material material, float width, bool loop)
    {
        LineRenderer line = GetOrAdd<LineRenderer>(GetChild(parent, name).gameObject);
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = 0;
        line.widthMultiplier = width;
        line.numCapVertices = line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        return line;
    }

    private static Material GetMaterial(string name, Color color)
    {
        string path = MaterialsPath + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null || !AssetDatabase.IsValidFolder(MaterialsPath))
            throw new InvalidOperationException("Faltan URP/Unlit o la carpeta de materiales del escenario.");
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material GetLabelMaterial(TMP_FontAsset font)
    {
        string path = MaterialsPath + "/WolfLabel.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(font.material) { name = "WolfLabel" };
        material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.05f, 0.07f));
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
        material.EnableKeyword("OUTLINE_ON");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Transform GetChild(Transform parent, string name)
    {
        Transform child = ExistingChild(parent, name);
        if (child != null) return child;
        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static Transform ExistingChild(Transform parent, string name)
    {
        foreach (Transform child in parent) if (child.name == name) return child;
        return null;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static T GetSingle<T>(Scene scene) where T : Component
    {
        T found = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T candidate in root.GetComponentsInChildren<T>(true))
            {
                if (found != null) throw new InvalidOperationException("Hay mas de un " + typeof(T).Name + ".");
                found = candidate;
            }
        if (found == null) throw new InvalidOperationException("Falta " + typeof(T).Name + ".");
        return found;
    }
}
