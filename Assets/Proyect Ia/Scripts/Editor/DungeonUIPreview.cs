using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Presentacion de Editor: no inicializa ni actualiza las FSM de los agentes.
[InitializeOnLoad]
public static class DungeonUIPreview
{
    private const string ScenePath = "Assets/Proyect Ia/Scenes/DungeonSimulation.unity";
    private static Scene _scene;
    private static DungeonSimulationSetup _setup;
    private static DungeonSimulationUI _ui;
    private static GameObject _root;
    private static Camera _camera;
    private static readonly List<WolfFeedback> Sources = new List<WolfFeedback>();
    private static readonly List<LineRenderer> VisionLines = new List<LineRenderer>();
    private static bool _resolved, _saving;
    private static double _nextRefresh;

    static DungeonUIPreview()
    {
        EditorApplication.update += UpdatePreview;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += Clear;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosed += OnSceneClosed;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
    }

    private static void UpdatePreview()
    {
        if (_saving || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Clear();
            return;
        }
        Scene active = SceneManager.GetActiveScene();
        if (!_resolved || active != _scene || (_ui == null && _root != null)) ResolveScene(active);
        if (_setup == null || _setup.UI == null || _setup.SceneCamera == null
            || _setup.Hunter == null || _setup.Scenario == null || _setup.Wolves == null)
        {
            Clear();
            return;
        }
        if (_ui != null && _ui != _setup.UI) Clear();
        if (_root == null) CreatePreview();
        if (EditorApplication.timeSinceStartup < _nextRefresh) return;
        _nextRefresh = EditorApplication.timeSinceStartup + 0.1d;
        RefreshPresentation();
    }

    private static void ResolveScene(Scene scene)
    {
        Clear();
        _scene = scene;
        _resolved = true;
        _setup = null;
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DungeonSimulationSetup candidate = root.GetComponentInChildren<DungeonSimulationSetup>();
            if (candidate != null) { _setup = candidate; break; }
        }
    }

    private static void CreatePreview()
    {
        _ui = _setup.UI;
        _root = new GameObject("Dungeon UI Preview") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(_root, _scene);
        _camera = new GameObject("Dungeon Preview Camera", typeof(Camera)).GetComponent<Camera>();
        _camera.gameObject.hideFlags = HideFlags.HideAndDontSave;
        _camera.transform.SetParent(_root.transform, false);
        _camera.CopyFrom(_setup.SceneCamera);
        _camera.depth = _setup.SceneCamera.depth + 2f;
        _camera.enabled = true;
        UpdateCameraTransform();
        _ui.InitializePreview(_setup.Hunter, _setup.Scenario, _setup.Wolves, _camera, _root.transform);
        CreateVisionLines();
        _nextRefresh = 0d;
    }

    private static void CreateVisionLines()
    {
        foreach (WolfAgent wolf in _setup.Wolves)
        {
            WolfFeedback feedback = wolf != null ? wolf.GetComponent<WolfFeedback>() : null;
            if (feedback == null) continue;
            using (SerializedObject data = new SerializedObject(feedback))
            {
                LineRenderer source = data.FindProperty("_viewLine").objectReferenceValue as LineRenderer;
                if (source == null) continue;
                LineRenderer line = Object.Instantiate(source, _root.transform);
                line.gameObject.name = "Preview vision " + wolf.name;
                line.gameObject.hideFlags = HideFlags.HideAndDontSave;
                line.enabled = true;
                Sources.Add(feedback);
                VisionLines.Add(line);
            }
        }
    }

    private static void UpdateCameraTransform()
    {
        Camera source = _setup.SceneCamera;
        _camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
    }

    private static void RefreshPresentation()
    {
        UpdateCameraTransform();
        Canvas.ForceUpdateCanvases();
        _ui.Refresh();
        for (int i = 0; i < Sources.Count; i++)
            if (Sources[i] != null && VisionLines[i] != null) Sources[i].RenderVision(VisionLines[i]);
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private static void Clear()
    {
        if (_ui != null) _ui.Release();
        if (_root != null) Object.DestroyImmediate(_root);
        _ui = null;
        _root = null;
        _camera = null;
        Sources.Clear();
        VisionLines.Clear();
    }

    private static void Invalidate()
    {
        Clear();
        _resolved = false;
        _nextRefresh = 0d;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state) => Invalidate();
    private static void OnActiveSceneChanged(Scene previous, Scene next) => Invalidate();
    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Invalidate();
    private static void OnSceneClosed(Scene scene) => Invalidate();
    private static void OnSceneSaving(Scene scene, string path) { _saving = true; Invalidate(); }
    private static void OnSceneSaved(Scene scene) { _saving = false; Invalidate(); }
}
