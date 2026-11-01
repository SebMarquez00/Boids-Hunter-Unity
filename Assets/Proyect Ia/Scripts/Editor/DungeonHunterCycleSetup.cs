using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class DungeonHunterCycleSetup
{
    private const string ThreatMaterialPath = "Assets/Proyect Ia/Materials/Dungeon/MAT_Hunter_Threat.mat";

    [MenuItem("Tools/IA/Entrega 2/Configurar ciclo del cazador")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath || scene.isDirty)
        {
            Debug.LogWarning("Abri DungeonSimulation guardada y sin cambios pendientes antes de configurar el ciclo.");
            return;
        }
        HunterAgent hunter = GetSingle<HunterAgent>(scene);
        HunterNavigationFeedback feedback = hunter.GetComponent<HunterNavigationFeedback>();
        if (feedback == null || !hunter.UsesNavigation || hunter.Scenario.ExitZone == null
            || hunter.Scenario.SafeZones.Count == 0 || hunter.Navigation.Follower == null
            || hunter.Navigation.Follower.Graph == null)
            throw new InvalidOperationException("Faltan navegacion, feedback, salida o refugios del Cazador.");

        using (SerializedObject data = new SerializedObject(feedback))
        {
            LineRenderer path = data.FindProperty("_pathLine").objectReferenceValue as LineRenderer;
            TMP_Text label = data.FindProperty("_statusLabel").objectReferenceValue as TMP_Text;
            if (path == null || path.sharedMaterial == null || label == null)
                throw new InvalidOperationException("Faltan la ruta azul o la etiqueta HunterStatus existentes.");

            ConfigureCycle(hunter);
            LineRenderer threat = ConfigureLine(hunter.transform, "DetectedWolf", GetThreatMaterial(path.sharedMaterial), 0.08f, false);
            LineRenderer refuge = ConfigureLine(hunter.transform, "SelectedSafeZone", path.sharedMaterial, 0.13f, true);
            data.FindProperty("_hunter").objectReferenceValue = hunter;
            data.FindProperty("_navigator").objectReferenceValue = hunter.Navigation;
            data.FindProperty("_threatLine").objectReferenceValue = threat;
            data.FindProperty("_safeZoneMarker").objectReferenceValue = refuge;
            data.ApplyModifiedPropertiesWithoutUndo();
            label.rectTransform.sizeDelta = new Vector2(64f, 2.6f);
            Vector3 labelPosition = label.transform.position;
            labelPosition.y = 2.5f;
            label.rectTransform.anchoredPosition3D = label.transform.parent != null
                ? label.transform.parent.InverseTransformPoint(labelPosition) : labelPosition;
            EditorUtility.SetDirty(label.rectTransform);
            label.text = "CAZADOR | Search | Recolecciones: 0/3 | Ciclos: 0\nIr al Coto | Calculos A*: 0";
            PrefabUtility.RecordPrefabInstancePropertyModifications(hunter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(feedback);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("No se pudo guardar la configuracion del ciclo del Cazador.");
        Debug.Log("Ciclo del Cazador configurado: 3 recolecciones, salida y reinicio; refugios con espera de 3 s.");
    }

    private static void ConfigureCycle(HunterAgent hunter)
    {
        using (SerializedObject data = new SerializedObject(hunter))
        {
            data.FindProperty("_requiredGathers").intValue = 3;
            data.FindProperty("_safeZoneWaitTime").floatValue = 3f;
            data.FindProperty("_viewRadius").floatValue = 7f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static LineRenderer ConfigureLine(Transform parent, string name, Material material, float width, bool loop)
    {
        Transform child = null;
        foreach (Transform candidate in parent)
            if (candidate.name == name) { child = candidate; break; }
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }
        LineRenderer line = child.GetComponent<LineRenderer>();
        if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.loop = loop;
        line.widthMultiplier = width;
        line.numCornerVertices = line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        return line;
    }

    private static Material GetThreatMaterial(Material pathMaterial)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ThreatMaterialPath);
        if (material != null) return material;
        material = new Material(pathMaterial) { name = "MAT_Hunter_Threat" };
        material.SetColor("_BaseColor", new Color(1f, 0.3f, 0.05f));
        AssetDatabase.CreateAsset(material, ThreatMaterialPath);
        return material;
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
