using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class SimulationUI : MonoBehaviour
{
    [Header("Panel")]
    public TMP_Text StatusText, DetailText, ProgressText;
    public Image StateIcon, ProgressFill;
    public GameObject ProgressRoot;
    public Button IconsButton;
    public TMP_Text IconsText;
    [Header("Vista")]
    [Range(.6f, 1.5f)] public float CameraZoom = .8f;
    [Header("Indicadores")]
    public AgentStatusBadge BadgePrefab;
    public RectTransform BadgeLayer;
    [Header("Iconos Kenney")]
    public Sprite PatrolIcon, AttackIcon, GatherIcon, BaitIcon, FlockIcon, EvadeIcon, SickIcon, DeadIcon;

    private HunterAgent _hunter;
    private BoidAgent[] _boids;
    private AgentStatusBadge[] _badges;
    private AgentStatusBadge _hunterBadge;
    private Camera _camera;
    private RectTransform _canvas;
    private Rect _originalRect;
    private float _originalSize;
    private bool _showIcons = true;
    private readonly Color _orange = new Color(1f, .66f, .3f);
    private readonly Color _cyan = new Color(.35f, .89f, .92f);
    private readonly Color _green = new Color(.35f, .86f, .65f);
    private readonly Color _red = new Color(1f, .4f, .42f);

    // Se carga solo en la simulación. No es necesario modificar la escena abierta.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainSimulation" || FindFirstObjectByType<SimulationUI>() != null) return;
        GameObject prefab = Resources.Load<GameObject>("SimulationHUD");
        if (prefab != null) Instantiate(prefab);
    }

    private void Start()
    {
        _hunter = FindFirstObjectByType<HunterAgent>();
        _boids = FindObjectsByType<BoidAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _camera = Camera.main;
        _canvas = (RectTransform)transform;
        if (_camera == null || _hunter == null) { enabled = false; return; }
        _originalRect = _camera.rect;
        _originalSize = _camera.orthographicSize;
        // Limpia toda la ventana, también fuera del rectángulo de la cámara del mundo.
        Camera background = new GameObject("UI Background Camera", typeof(Camera)).GetComponent<Camera>();
        background.transform.SetParent(transform, false);
        background.depth = _camera.depth - 1;
        background.cullingMask = 0;
        background.clearFlags = CameraClearFlags.SolidColor;
        background.backgroundColor = new Color(.025f, .04f, .055f);
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        _hunterBadge = CreateBadge(_hunter.transform);
        _badges = new AgentStatusBadge[_boids.Length];
        for (int i = 0; i < _boids.Length; i++) _badges[i] = CreateBadge(_boids[i].transform);
        IconsButton.onClick.AddListener(ToggleIcons);
    }

    private AgentStatusBadge CreateBadge(Transform target)
    {
        AgentStatusBadge badge = Instantiate(BadgePrefab, BadgeLayer);
        badge.Initialize(target);
        return badge;
    }

    private void LateUpdate()
    {
        // Reservar espacio para el panel, sin tapar la simulación.
        float width = _canvas.rect.width;
        float height = _canvas.rect.height;
        _camera.rect = new Rect(16f / width, 58f / height,
            (width - 372f) / width, (height - 152f) / height);
        if (_camera.orthographic)
            _camera.orthographicSize = Mathf.Max(_originalSize * CameraZoom, 10f / _camera.aspect);
        RefreshHunter();
        for (int i = 0; i < _boids.Length; i++)
        {
            BoidAgent boid = _boids[i];
            if (boid == null) continue;
            bool active = boid.gameObject.activeInHierarchy;
            Sprite icon = FlockIcon;
            string label = "";
            Color color = _cyan;
            if (!boid.IsAlive) { icon = DeadIcon; color = _red; }
            else if (boid.ThreatDetected) { icon = EvadeIcon; label = ""; }
            else if (boid.InterestTarget != null) { icon = BaitIcon; color = new Color(1f, .83f, .38f); }
            if (boid.IsAlive && boid.IsSick) { color = _green; if (!boid.ThreatDetected) icon = SickIcon; }
            _badges[i].Refresh(_camera, _canvas, icon, label, color, _showIcons && active);
        }
    }

    private void RefreshHunter()
    {
        State state = _hunter.CurrentState;
        Sprite icon = PatrolIcon;
        string title = "Patrullando";
        string detail = "Recorre los puntos de su ruta.";
        float progress = 0f;
        bool showProgress = false;
        if (state is HunterAttackState)
        {
            icon = AttackIcon;
            title = _hunter.Velocity.sqrMagnitude > .01f ? "Persiguiendo" : "Disparando";
            detail = _hunter.HasActiveProjectile ? "Espera el resultado del proyectil." : "Busca una oportunidad de ataque.";
        }
        else if (state is HunterGatherState)
        {
            icon = GatherIcon;
            bool collecting = _hunter.GatherProgress > 0f;
            title = collecting ? "Recogiendo" : "Buscando al caído";
            detail = collecting ? "Se detiene hasta completar la recolección." : "Se acerca al boid eliminado.";
            showProgress = collecting;
            progress = _hunter.GatherProgress / Mathf.Max(.001f, _hunter.GatherDuration);
        }
        else if (state is HunterPlaceInterestState placement)
        {
            icon = BaitIcon;
            title = "Colocando cebo";
            detail = "Se detiene y deja un cebo en su posición.";
            showProgress = true;
            progress = placement.Elapsed / Mathf.Max(.001f, _hunter.PlacementDuration);
        }
        StatusText.text = title;
        DetailText.text = detail;
        StateIcon.sprite = icon;
        ProgressRoot.SetActive(showProgress);
        ProgressFill.fillAmount = Mathf.Clamp01(progress);
        ProgressText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100)} %";
        _hunterBadge.Refresh(_camera, _canvas, icon, title, _orange, _showIcons);
    }

    public void ToggleIcons()
    {
        _showIcons = !_showIcons;
        IconsText.text = _showIcons ? "Ocultar iconos" : "Mostrar iconos";
    }

    private void OnDestroy()
    {
        if (_camera != null) { _camera.rect = _originalRect; _camera.orthographicSize = _originalSize; }
    }
}
