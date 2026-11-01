using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class DungeonHunterSetup
{
    [MenuItem("Tools/IA/Entrega 2/Integrar navegacion del cazador")]
    public static void Install() => DungeonNavigationBuilder.AddNavigation();

    public static void Configure(ScenarioReferences scenario, NavigationGraph graph)
    {
        GameObject body = GetHunterBody(scenario);
        PathFollower follower = GetOrAdd<PathFollower>(body);
        follower.Graph = graph;
        follower.Access = NavigationAccess.Hunter;
        follower.Speed = 4.5f;
        AgentNavigator navigator = GetOrAdd<AgentNavigator>(body);
        navigator.Follower = follower;
        HunterAgent hunter = GetOrAdd<HunterAgent>(body);
        ConfigureHunter(hunter, navigator, scenario);
        ConfigureFeedback(hunter, navigator, graph);
        RemoveOldDemo(graph);
        PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        Selection.activeGameObject = body;
    }

    private static GameObject GetHunterBody(ScenarioReferences scenario)
    {
        foreach (Transform child in scenario.HunterSpawn)
            if (child.GetComponent<CapsuleCollider>() != null) return child.gameObject;
        throw new InvalidOperationException("No se encontro el cuerpo del Cazador bajo HunterSpawn.");
    }

    private static void ConfigureHunter(HunterAgent hunter, AgentNavigator navigator, ScenarioReferences scenario)
    {
        Transform interests = null;
        foreach (Transform child in scenario.transform)
            if (child.name == "InterestObjects") interests = child;
        if (interests == null) throw new InvalidOperationException("Falta InterestObjects en el escenario.");
        SerializedObject data = new SerializedObject(hunter);
        data.FindProperty("_navigation").objectReferenceValue = navigator;
        data.FindProperty("_scenario").objectReferenceValue = scenario;
        data.FindProperty("_interestPrefab").objectReferenceValue = LoadPrefabComponent<InterestObject>("InterestObject_Prototype");
        data.FindProperty("_projectilePrefab").objectReferenceValue = LoadPrefabComponent<HunterProjectile>("HunterProjectile");
        data.FindProperty("_interestParent").objectReferenceValue = interests;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFeedback(HunterAgent hunter, AgentNavigator navigator, NavigationGraph graph)
    {
        LineRenderer line = GetOrAdd<LineRenderer>(hunter.gameObject);
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.widthMultiplier = 0.13f;
        line.numCornerVertices = line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = GetPathMaterial();
        TMP_Text label = GetStatusLabel(graph.transform);
        label.text = "CAZADOR | Search: ir al Coto mediante A*";
        HunterNavigationFeedback feedback = GetOrAdd<HunterNavigationFeedback>(hunter.gameObject);
        SerializedObject data = new SerializedObject(feedback);
        data.FindProperty("_hunter").objectReferenceValue = hunter;
        data.FindProperty("_navigator").objectReferenceValue = navigator;
        data.FindProperty("_pathLine").objectReferenceValue = line;
        data.FindProperty("_statusLabel").objectReferenceValue = label;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text GetStatusLabel(Transform parent)
    {
        foreach (TMP_Text text in parent.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name != "NavigationStatus" && text.name != "HunterStatus") continue;
            text.name = "HunterStatus";
            return text;
        }
        TextMeshPro label = new GameObject("HunterStatus", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(parent, false);
        label.transform.position = new Vector3(0f, 0.1f, 21.2f);
        label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        label.fontSize = 7f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(64f, 1.5f);
        label.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        return label;
    }

    private static Material GetPathMaterial()
    {
        const string path = "Assets/Proyect Ia/Materials/Dungeon/MAT_Navigation_Path.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("Falta URP/Unlit.");
        material = new Material(shader) { name = "MAT_Navigation_Path" };
        material.SetColor("_BaseColor", new Color(0.04f, 0.25f, 0.42f));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void RemoveOldDemo(NavigationGraph graph)
    {
        for (int i = graph.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = graph.transform.GetChild(i);
            if (child.name == "NavigationDemo") Object.DestroyImmediate(child.gameObject);
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static T LoadPrefabComponent<T>(string name) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proyect Ia/Prefabs/" + name + ".prefab");
        T component = prefab != null ? prefab.GetComponent<T>() : null;
        if (component == null) throw new InvalidOperationException("Falta el componente requerido en " + name);
        return component;
    }
}
