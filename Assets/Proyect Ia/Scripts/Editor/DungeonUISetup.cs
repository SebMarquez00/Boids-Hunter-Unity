using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DungeonUISetup
{
    private const string HudPath = "Assets/Proyect Ia/Resources/DungeonHUD.prefab";
    private const string PreviousHudPath = "Assets/Proyect Ia/Resources/SimulationHUD.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private static readonly Color PanelColor = new Color(0.055f, 0.085f, 0.12f, 0.98f);
    private static readonly Color CardColor = new Color(0.09f, 0.135f, 0.18f);
    private static readonly Color Cyan = new Color(0.35f, 0.89f, 0.92f);
    private static readonly Color Orange = new Color(1f, 0.66f, 0.3f);
    private static readonly Color Muted = new Color(0.62f, 0.72f, 0.78f);

    [MenuItem("Tools/IA/Entrega 2/Configurar interfaz de la entrega 2")]
    public static void Install()
    {
        DungeonUIPreview.Suspend();
        try { ConfigureScene(); }
        finally { DungeonUIPreview.Resume(); }
    }

    private static void ConfigureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != DungeonMapBuilder.ScenePath || scene.isDirty)
        {
            Debug.LogWarning("Abri DungeonSimulation guardada y sin cambios pendientes antes de configurar la interfaz.");
            return;
        }
        HunterAgent hunter = GetSingle<HunterAgent>(scene);
        ScenarioReferences scenario = GetSingle<ScenarioReferences>(scene);
        Camera camera = GetSingle<Camera>(scene);
        WolfAgent[] wolves = GetComponents<WolfAgent>(scene);
        if (wolves.Length != 4) throw new InvalidOperationException("La interfaz requiere los cuatro lobos del escenario.");
        Array.Sort(wolves, (a, b) => string.CompareOrdinal(a.name, b.name));
        DungeonSimulationSetup[] setups = GetComponents<DungeonSimulationSetup>(scene);
        if (setups.Length > 1) throw new InvalidOperationException("Hay mas de un DungeonSimulationSetup.");
        GameObject previous = AssetDatabase.LoadAssetAtPath<GameObject>(PreviousHudPath);
        SimulationUI style = previous != null ? previous.GetComponent<SimulationUI>() : null;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (style == null || style.BadgePrefab == null || style.ProgressFill == null
            || style.ProgressFill.sprite == null || font == null)
            throw new InvalidOperationException("Faltan SimulationHUD, AgentStatusBadge, el sprite de progreso o LiberationSans SDF.");

        DungeonSimulationUI[] previousInterfaces = GetComponents<DungeonSimulationUI>(scene);
        if (previousInterfaces.Length > 1) throw new InvalidOperationException("Hay mas de una interfaz en DungeonSimulation.");
        foreach (DungeonSimulationUI previousInterface in previousInterfaces) UnityEngine.Object.DestroyImmediate(previousInterface.gameObject);
        DungeonSimulationUI prefab = CreatePrefab(style, font);
        GameObject interfaceObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene);
        DungeonSimulationUI sceneInterface = interfaceObject.GetComponent<DungeonSimulationUI>();
        DungeonSimulationSetup setup = setups.Length == 1 ? setups[0] : scenario.gameObject.AddComponent<DungeonSimulationSetup>();
        ConfigureSetup(setup, prefab, sceneInterface, hunter, scenario, wolves, camera);
        ConfigureMapLabels(scenario);
        HideLegacyLabel(hunter.GetComponent<HunterNavigationFeedback>(), "_statusLabel");
        foreach (WolfAgent wolf in wolves) HideLegacyLabel(wolf.GetComponent<WolfFeedback>(), "_label");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se pudo guardar la interfaz del escenario.");
        Debug.Log("Interfaz de Entrega 2 configurada: estado del cazador, lobos, indicadores y controles de feedback.");
    }

    private static DungeonSimulationUI CreatePrefab(SimulationUI style, TMP_FontAsset font)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            RectTransform root = CreateRect("DungeonHUD", null);
            SceneManager.MoveGameObjectToScene(root.gameObject, preview);
            Canvas canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.gameObject.AddComponent<GraphicRaycaster>();
            DungeonSimulationUI ui = root.gameObject.AddComponent<DungeonSimulationUI>();
            ui.Viewport = root.gameObject.AddComponent<DungeonViewport>();
            ui.Indicators = root.gameObject.AddComponent<DungeonAgentIndicators>();
            ConfigureIndicators(ui.Indicators, root, style);
            CreateHeader(root, font);
            CreateSidebar(ui, root, style, font);
            CreateControls(ui, root, font);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, HudPath);
            if (prefab == null) throw new InvalidOperationException("No se pudo guardar DungeonHUD.prefab.");
            return prefab.GetComponent<DungeonSimulationUI>();
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static void ConfigureSetup(DungeonSimulationSetup setup, DungeonSimulationUI prefab, DungeonSimulationUI sceneInterface,
        HunterAgent hunter, ScenarioReferences scenario, WolfAgent[] wolves, Camera camera)
    {
        using (SerializedObject data = new SerializedObject(setup))
        {
            data.FindProperty("_uiPrefab").objectReferenceValue = prefab;
            data.FindProperty("_ui").objectReferenceValue = sceneInterface;
            data.FindProperty("_hunter").objectReferenceValue = hunter;
            data.FindProperty("_scenario").objectReferenceValue = scenario;
            data.FindProperty("_camera").objectReferenceValue = camera;
            SerializedProperty agents = data.FindProperty("_wolves");
            agents.arraySize = wolves.Length;
            for (int i = 0; i < wolves.Length; i++) agents.GetArrayElementAtIndex(i).objectReferenceValue = wolves[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void HideLegacyLabel(Component feedback, string property)
    {
        if (feedback == null) return;
        using (SerializedObject data = new SerializedObject(feedback))
        {
            TMP_Text label = data.FindProperty(property).objectReferenceValue as TMP_Text;
            if (label != null) label.gameObject.SetActive(false);
        }
    }

    private static void ConfigureIndicators(DungeonAgentIndicators indicators, RectTransform root, SimulationUI style)
    {
        RectTransform layer = CreateRect("AgentIndicators", root);
        Stretch(layer, 0f, 0f, 0f, 0f);
        indicators.BadgePrefab = style.BadgePrefab;
        indicators.BadgeLayer = layer;
        indicators.SearchIcon = style.PatrolIcon;
        indicators.AttackIcon = style.AttackIcon;
        indicators.GatherIcon = style.GatherIcon;
        indicators.FleeIcon = style.EvadeIcon;
        indicators.ExitIcon = style.BaitIcon;
        indicators.DeadIcon = style.DeadIcon;
        indicators.FlockIcon = style.FlockIcon;
        indicators.BaitIcon = style.BaitIcon;
        indicators.SickIcon = style.SickIcon;
        indicators.WolfIcon = style.EvadeIcon;
    }

    private static void ConfigureMapLabels(ScenarioReferences scenario)
    {
        Transform roomLabels = scenario.transform.Find("Environment/RoomLabels");
        if (roomLabels != null) roomLabels.gameObject.SetActive(false);
        HideZoneLabels(scenario.StartZone);
        HideZoneLabels(scenario.HuntingZone);
        HideZoneLabels(scenario.ExitZone);
        for (int i = 0; i < scenario.SafeZones.Count; i++)
        {
            ScenarioZone zone = scenario.SafeZones[i];
            if (zone == null) continue;
            foreach (TMP_Text label in zone.GetComponentsInChildren<TMP_Text>(true))
            {
                label.gameObject.SetActive(true);
                label.text = (i + 1).ToString();
                label.fontSize = 6f;
                label.rectTransform.sizeDelta = new Vector2(1.5f, 1.5f);
                EditorUtility.SetDirty(label);
                EditorUtility.SetDirty(label.rectTransform);
            }
        }
    }

    private static void HideZoneLabels(ScenarioZone zone)
    {
        if (zone == null) return;
        foreach (TMP_Text label in zone.GetComponentsInChildren<TMP_Text>(true))
            label.gameObject.SetActive(false);
    }

    private static void CreateHeader(RectTransform root, TMP_FontAsset font)
    {
        RectTransform header = CreatePanel("Header", root, PanelColor);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(16f, -80f);
        header.offsetMax = new Vector2(-16f, -16f);
        CreateText("Title", header, font, "PERCEPCION Y NAVEGACION", 24f, Color.white, 20f, 9f, 700f, 29f);
        CreateText("Subtitle", header, font, "ENTREGA 2  /  CAZADOR Y LOBOS", 13f, Muted, 21f, 39f, 650f, 18f);
        TMP_Text live = CreateText("Live", header, font, "SIMULACION EN VIVO", 14f, Cyan, 0f, 0f, 240f, 28f);
        live.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform rect = live.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-20f, 0f);
    }

    private static void CreateSidebar(DungeonSimulationUI ui, RectTransform root, SimulationUI style, TMP_FontAsset font)
    {
        RectTransform sidebar = CreatePanel("InformationPanel", root, PanelColor);
        sidebar.anchorMin = sidebar.anchorMax = Vector2.one;
        sidebar.pivot = Vector2.one;
        sidebar.anchoredPosition = new Vector2(-16f, -96f);
        sidebar.sizeDelta = new Vector2(336f, 496f);
        CreateText("HunterSection", sidebar, font, "01  /  CAZADOR", 14f, Cyan, 16f, 14f, 304f, 20f);
        ui.HunterPanel = CreateHunterPanel(sidebar, style, font);
        CreateLegend(sidebar, style, font);
    }

    private static HunterHUDPanel CreateHunterPanel(RectTransform sidebar, SimulationUI style, TMP_FontAsset font)
    {
        RectTransform card = CreatePanel("HunterCard", sidebar, CardColor);
        Place(card, 12f, 42f, 312f, 158f);
        HunterHUDPanel panel = card.gameObject.AddComponent<HunterHUDPanel>();
        panel.StateIcon = CreateImage("StateIcon", card, style.PatrolIcon, Orange, 16f, 15f, 30f, 30f);
        panel.StateText = CreateText("State", card, font, "Search · Buscando", 18f, Color.white, 58f, 13f, 238f, 32f);
        panel.ObjectiveText = CreateText("Objective", card, font, "Recolecciones 0/3", 16f, Cyan, 16f, 52f, 280f, 22f);
        panel.DetailText = CreateText("Detail", card, font, "Se dirige al coto de caza.", 15f, Color.white, 16f, 82f, 280f, 34f);
        RectTransform progress = CreatePanel("ActionProgress", card, PanelColor);
        Place(progress, 16f, 128f, 280f, 22f);
        panel.ProgressRoot = progress.gameObject;
        panel.ProgressRoot.SetActive(false);
        panel.ProgressFill = CreateImage("Fill", progress, style.ProgressFill.sprite, new Color(0.12f, 0.42f, 0.49f), 0f, 0f, 280f, 22f);
        panel.ProgressFill.preserveAspect = false;
        panel.ProgressFill.type = Image.Type.Filled;
        panel.ProgressFill.fillMethod = Image.FillMethod.Horizontal;
        panel.ProgressFill.fillOrigin = 0;
        panel.ProgressFill.fillAmount = 0f;
        panel.ProgressText = CreateText("Progress", progress, font, "0 %", 13f, Color.white, 0f, 0f, 280f, 22f);
        panel.ProgressText.alignment = TextAlignmentOptions.Center;
        panel.SearchIcon = style.PatrolIcon;
        panel.AttackIcon = style.AttackIcon;
        panel.GatherIcon = style.GatherIcon;
        panel.FleeIcon = style.EvadeIcon;
        panel.ExitIcon = style.BaitIcon;
        panel.DeadIcon = style.DeadIcon;
        return panel;
    }

    private static void CreateLegend(RectTransform sidebar, SimulationUI style, TMP_FontAsset font)
    {
        CreateText("LegendSection", sidebar, font, "02  /  LEYENDA", 14f, Cyan, 16f, 216f, 304f, 20f);
        CreateZoneLegendRow(sidebar, font, "Start", "Inicio del cazador", new Color(0.34f, 0.68f, 0.49f), 244f);
        CreateZoneLegendRow(sidebar, font, "HuntingGround", "Coto de caza (Boids)", new Color(0.84f, 0.65f, 0.30f), 264f);
        CreateZoneLegendRow(sidebar, font, "SafeZones", "Refugios 1-3 (sin lobos)", new Color(0.67f, 0.48f, 0.77f), 284f);
        CreateZoneLegendRow(sidebar, font, "Exit", "Salida", new Color(0.32f, 0.62f, 0.79f), 304f);
        CreateImage("WolfSymbol", sidebar, style.EvadeIcon, Cyan, 14f, 332f, 18f, 18f);
        CreateText("WolfLabel", sidebar, font, "Lobos L1-L4 (color del cuerpo)", 13f, Muted, 40f, 334f, 280f, 18f);
        CreateWolfLegendItem(sidebar, font, "Patrol", "Patrulla", new Color(0.38f, 0.53f, 0.68f), 16f, 360f, 90f);
        CreateWolfLegendItem(sidebar, font, "Chase", "Persigue", new Color(0.9f, 0.24f, 0.16f), 122f, 360f, 90f);
        CreateWolfLegendItem(sidebar, font, "Search", "Busca", new Color(0.92f, 0.63f, 0.2f), 228f, 360f, 90f);
        CreateWolfLegendItem(sidebar, font, "Howl", "Aullido", new Color(1f, 0.86f, 0.32f), 16f, 382f, 140f);
        CreateWolfLegendItem(sidebar, font, "Alert", "Alerta recibida", new Color(0.2f, 0.85f, 0.94f), 176f, 382f, 144f);
        CreateText("PathKey", sidebar, font, "Lineas: caminos calculados con A*", 13f, Muted, 16f, 418f, 304f, 18f);
        CreateText("MemoryKey", sidebar, font, "Rombo rojo/ambar: posicion recordada", 13f, Muted, 16f, 440f, 304f, 18f);
        CreateText("RefugeKey", sidebar, font, "Rombo azul: refugio elegido", 13f, Muted, 16f, 462f, 304f, 18f);
    }

    private static void CreateWolfLegendItem(RectTransform sidebar, TMP_FontAsset font, string name,
        string label, Color color, float left, float top, float width)
    {
        CreateImage(name + "Swatch", sidebar, null, color, left, top + 3f, 10f, 10f);
        CreateText(name + "Label", sidebar, font, label, 12f, Muted, left + 15f, top, width - 15f, 16f);
    }

    private static void CreateZoneLegendRow(RectTransform sidebar, TMP_FontAsset font, string name,
        string label, Color color, float top)
    {
        CreateImage(name + "Swatch", sidebar, null, color, 16f, top + 2f, 12f, 12f);
        CreateText(name + "Label", sidebar, font, label, 13f, Muted, 36f, top, 284f, 16f);
    }

    private static void CreateControls(DungeonSimulationUI ui, RectTransform root, TMP_FontAsset font)
    {
        RectTransform controls = CreatePanel("Controls", root, PanelColor);
        controls.anchorMin = controls.anchorMax = Vector2.zero;
        controls.pivot = Vector2.zero;
        controls.anchoredPosition = new Vector2(16f, 16f);
        controls.sizeDelta = new Vector2(146f, 40f);
        ui.VisionButton = CreateButton("Vision", controls, font, "Ocultar vision", 8f, out TMP_Text visionText);
        ui.VisionText = visionText;
    }

    private static Button CreateButton(string name, RectTransform parent, TMP_FontAsset font, string title, float left, out TMP_Text text)
    {
        RectTransform rect = CreatePanel(name, parent, Color.white);
        Place(rect, left, 5f, 130f, 30f);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = CardColor;
        colors.highlightedColor = colors.selectedColor = new Color(0.15f, 0.23f, 0.3f);
        colors.pressedColor = new Color(0.2f, 0.32f, 0.4f);
        button.colors = colors;
        text = CreateText("Text", rect, font, title, 14f, Color.white, 0f, 0f, 130f, 30f);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = LayerMask.NameToLayer("UI");
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform CreatePanel(string name, RectTransform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color,
        float left, float top, float width, float height)
    {
        RectTransform rect = CreatePanel(name, parent, color);
        Place(rect, left, top, width, height);
        Image image = rect.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        return image;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, TMP_FontAsset font, string value,
        float size, Color color, float left, float top, float width, float height)
    {
        RectTransform rect = CreateRect(name, parent);
        Place(rect, left, top, width, height);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static T[] GetComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) components.AddRange(root.GetComponentsInChildren<T>(true));
        return components.ToArray();
    }

    private static T GetSingle<T>(Scene scene) where T : Component
    {
        T[] components = GetComponents<T>(scene);
        if (components.Length != 1) throw new InvalidOperationException("Se requiere un unico " + typeof(T).Name + " en DungeonSimulation.");
        return components[0];
    }
}
