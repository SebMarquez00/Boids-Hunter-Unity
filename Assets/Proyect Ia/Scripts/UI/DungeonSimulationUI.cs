using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class DungeonSimulationUI : MonoBehaviour
{
    public DungeonViewport Viewport;
    public DungeonAgentIndicators Indicators;
    public HunterHUDPanel HunterPanel;
    public Button VisionButton;
    public TMP_Text VisionText;

    public bool ShowVision { get; private set; } = true;

    private HunterNavigationFeedback _hunterFeedback;
    private bool _hunterPaths, _hunterStatus;
    private WolfVisibility[] _wolfVisibility;
    private float _refreshTimer;
    private bool _initialized;
    private bool _preview;

    private struct WolfVisibility
    {
        public WolfFeedback Feedback;
        public bool Paths, Vision, Labels;
    }

    public void Initialize(HunterAgent hunter, ScenarioReferences scenario, WolfAgent[] wolves, Camera sceneCamera)
    {
        if (_preview) Release();
        if (_initialized) return;
        if (hunter == null || scenario == null || sceneCamera == null || Viewport == null
            || Indicators == null || HunterPanel == null)
        {
            Debug.LogError("Faltan referencias en DungeonSimulationUI.", this);
            enabled = false;
            return;
        }
        wolves = wolves ?? System.Array.Empty<WolfAgent>();
        RectTransform canvas = (RectTransform)transform;
        Canvas.ForceUpdateCanvases();
        Viewport.Initialize(sceneCamera, canvas);
        Indicators.Initialize(hunter, scenario.Boids, wolves, sceneCamera, canvas);
        HunterPanel.Initialize(hunter, scenario);
        SaveFeedbackVisibility(hunter, wolves);
        CreateEventSystem();
        if (VisionButton != null) VisionButton.onClick.AddListener(ToggleVision);
        _initialized = true;
        ApplyVisibility();
        Refresh();
    }

    public void InitializePreview(HunterAgent hunter, ScenarioReferences scenario, WolfAgent[] wolves,
        Camera previewCamera, Transform previewRoot)
    {
        if (_initialized) return;
        _preview = true;
        RectTransform canvas = (RectTransform)transform;
        Canvas.ForceUpdateCanvases();
        Viewport.Initialize(previewCamera, canvas, previewRoot);
        Indicators.Initialize(hunter, scenario.Boids, wolves, previewCamera, canvas, true);
        _initialized = true;
        Refresh();
    }

    private void CreateEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject events = new GameObject("Dungeon UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.transform.SetParent(transform, false);
    }

    private void SaveFeedbackVisibility(HunterAgent hunter, WolfAgent[] wolves)
    {
        _hunterFeedback = hunter.GetComponent<HunterNavigationFeedback>();
        if (_hunterFeedback != null)
        {
            _hunterPaths = _hunterFeedback.PathsVisible;
            _hunterStatus = _hunterFeedback.StatusVisible;
        }
        _wolfVisibility = new WolfVisibility[wolves.Length];
        for (int i = 0; i < wolves.Length; i++)
        {
            WolfFeedback feedback = wolves[i] != null ? wolves[i].GetComponent<WolfFeedback>() : null;
            if (feedback == null) continue;
            _wolfVisibility[i] = new WolfVisibility
            {
                Feedback = feedback,
                Paths = feedback.PathsVisible,
                Vision = feedback.VisionVisible,
                Labels = feedback.LabelsVisible
            };
        }
    }

    private void LateUpdate()
    {
        if (!_initialized || _preview) return;
        Viewport.Refresh();
        Indicators.Refresh(true);
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f) RefreshPanels();
    }

    public void Refresh()
    {
        if (!_initialized) return;
        Viewport.Refresh();
        Indicators.Refresh(true);
        if (!_preview) RefreshPanels();
    }

    private void RefreshPanels()
    {
        _refreshTimer = .1f;
        HunterPanel.Refresh();
    }

    public void ToggleVision()
    {
        ShowVision = !ShowVision;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (_hunterFeedback != null) _hunterFeedback.SetVisibility(true, false);
        if (_wolfVisibility != null)
            foreach (WolfVisibility saved in _wolfVisibility)
                if (saved.Feedback != null) saved.Feedback.SetVisibility(true, ShowVision, false);
        if (VisionText != null) VisionText.text = ShowVision ? "Ocultar vision" : "Mostrar vision";
    }

    public void Release()
    {
        if (!_initialized) return;
        if (VisionButton != null) VisionButton.onClick.RemoveListener(ToggleVision);
        if (_hunterFeedback != null) _hunterFeedback.SetVisibility(_hunterPaths, _hunterStatus);
        if (_wolfVisibility != null)
            foreach (WolfVisibility saved in _wolfVisibility)
                if (saved.Feedback != null) saved.Feedback.SetVisibility(saved.Paths, saved.Vision, saved.Labels);
        if (Viewport != null) Viewport.Restore();
        if (Indicators != null) Indicators.Clear();
        _hunterFeedback = null;
        _wolfVisibility = null;
        _initialized = _preview = false;
    }

    private void OnDestroy() => Release();
}
