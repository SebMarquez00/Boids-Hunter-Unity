using TMPro;
using UnityEngine;

public class WolfFeedback : MonoBehaviour
{
    [SerializeField] private WolfAgent _wolf;
    [SerializeField] private Renderer _bodyRenderer;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private LineRenderer _pathLine;
    [SerializeField] private LineRenderer _viewLine;
    [SerializeField] private LineRenderer _lastKnownLine;
    [SerializeField] private AudioSource _howlAudio;
    [SerializeField] private string _id = "L1";

    private const float EventDuration = 0.8f;
    private const int ViewSegments = 16;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private MaterialPropertyBlock _colors;
    private int _playedHowls;
    private int _shownRevision = -1;
    private int _shownWaypoint = -1;
    private float _viewTimer;
    private string _shownText;

    private void OnEnable()
    {
        if (_wolf == null) _wolf = GetComponent<WolfAgent>();
        if (_colors == null) _colors = new MaterialPropertyBlock();
        _playedHowls = _wolf != null ? _wolf.HowlCount : 0;
        _shownRevision = _shownWaypoint = -1;
        _viewTimer = 0f;
    }

    private void LateUpdate()
    {
        if (_wolf == null) return;
        string state = GetStateLabel();
        string action = GetRecentAction();
        Color color = GetStateColor();
        UpdateColor(color, action);
        UpdateLabel(state, action);
        UpdatePath(color);
        UpdateKnownPosition(color);
        UpdateView(color);
        PlayHowl();
    }

    private string GetStateLabel()
    {
        string state = _wolf.CurrentStateName ?? string.Empty;
        if (state.Contains("Chase")) return "Persigue";
        return state.Contains("Search") ? "Busca" : "Patrulla";
    }

    private Color GetStateColor()
    {
        if (_wolf.HasVisualContact) return new Color(0.9f, 0.24f, 0.16f);
        return _wolf.HasLastKnownPosition ? new Color(0.92f, 0.63f, 0.2f) : new Color(0.38f, 0.53f, 0.68f);
    }

    private string GetRecentAction()
    {
        if (_wolf.CaptureCount > 0 && Time.time - _wolf.LastCaptureTime < EventDuration) return "CAPTURA";
        if (_wolf.HowlCount > 0 && Time.time - _wolf.LastAlertTime < EventDuration) return "AULLIDO";
        if (_wolf.ReceivedAlertCount > 0 && Time.time - _wolf.LastReceivedAlertTime < EventDuration) return "ALERTA RECIBIDA";
        return string.Empty;
    }

    private void UpdateColor(Color stateColor, string action)
    {
        Color color = action == "AULLIDO" ? new Color(1f, 0.86f, 0.32f)
            : action == "ALERTA RECIBIDA" ? new Color(0.2f, 0.85f, 0.94f)
            : action == "CAPTURA" ? new Color(0.95f, 0.44f, 0.72f) : stateColor;
        SetColor(_bodyRenderer, color);
    }

    private void UpdateLabel(string state, string action)
    {
        if (_label == null) return;
        _label.transform.position = transform.position + new Vector3(0f, 1.05f, 1.1f);
        _label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        string text = _id + " | " + state + (action.Length > 0 ? "\n" + action : string.Empty);
        if (_shownText == text) return;
        _label.text = text;
        _shownText = text;
    }

    private void UpdatePath(Color color)
    {
        if (_pathLine == null) return;
        AgentNavigator navigation = _wolf.Navigation;
        PathFollower follower = navigation != null ? navigation.Follower : null;
        if (follower == null || !navigation.HasDestination || navigation.WaitingForPath || !follower.IsMoving)
        {
            _pathLine.positionCount = 0;
            _shownRevision = -1;
            return;
        }
        SetColor(_pathLine, Color.Lerp(color, Color.gray, 0.35f));
        if (_shownRevision != navigation.Revision || _shownWaypoint != follower.WaypointIndex)
        {
            _shownRevision = navigation.Revision;
            _shownWaypoint = follower.WaypointIndex;
            _pathLine.positionCount = follower.Path.Count - _shownWaypoint + 1;
            for (int i = _shownWaypoint; i < follower.Path.Count; i++)
                _pathLine.SetPosition(i - _shownWaypoint + 1, OnFloor(follower.Path[i], 0.1f));
        }
        _pathLine.SetPosition(0, OnFloor(transform.position, 0.1f));
    }

    private void UpdateKnownPosition(Color color)
    {
        if (_lastKnownLine == null) return;
        _lastKnownLine.positionCount = _wolf.HasLastKnownPosition ? 4 : 0;
        if (!_wolf.HasLastKnownPosition) return;
        // El marcador muestra la memoria del lobo, no consulta la posicion actual del Cazador.
        Vector3 point = OnFloor(_wolf.LastKnownPosition, 0.14f);
        _lastKnownLine.SetPosition(0, point + Vector3.forward * 0.3f);
        _lastKnownLine.SetPosition(1, point + Vector3.right * 0.3f);
        _lastKnownLine.SetPosition(2, point + Vector3.back * 0.3f);
        _lastKnownLine.SetPosition(3, point + Vector3.left * 0.3f);
        SetColor(_lastKnownLine, color);
    }

    private void UpdateView(Color color)
    {
        if (_viewLine == null) return;
        _viewTimer -= Time.deltaTime;
        if (_viewTimer > 0f) return;
        _viewTimer = 0.1f;
        NavigationGraph graph = _wolf.Navigation != null && _wolf.Navigation.Follower != null
            ? _wolf.Navigation.Follower.Graph : null;
        Vector3 eye = transform.position;
        if (graph != null) eye.y = graph.SamplingHeight;
        _viewLine.positionCount = ViewSegments + 3;
        _viewLine.SetPosition(0, OnFloor(eye, 0.06f));
        for (int i = 0; i <= ViewSegments; i++)
        {
            float angle = -_wolf.ViewAngle * 0.5f + _wolf.ViewAngle * i / ViewSegments;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
            float distance = _wolf.ViewRadius;
            if (graph != null && Physics.Raycast(eye, direction, out RaycastHit hit, distance,
                graph.ObstacleMask, QueryTriggerInteraction.Ignore)) distance = Mathf.Max(0f, hit.distance - 0.03f);
            _viewLine.SetPosition(i + 1, OnFloor(eye + direction * distance, 0.06f));
        }
        _viewLine.SetPosition(ViewSegments + 2, OnFloor(eye, 0.06f));
        SetColor(_viewLine, Color.Lerp(color, new Color(0.45f, 0.48f, 0.5f), 0.6f));
    }

    private void PlayHowl()
    {
        int count = _wolf.HowlCount;
        if (count <= _playedHowls) return;
        _playedHowls = count;
        if (_howlAudio != null && _howlAudio.clip != null) _howlAudio.PlayOneShot(_howlAudio.clip);
    }

    private void SetColor(Renderer target, Color color)
    {
        if (target == null) return;
        _colors.SetColor(BaseColor, color);
        target.SetPropertyBlock(_colors);
    }

    private void OnDisable()
    {
        if (_pathLine != null) _pathLine.positionCount = 0;
        if (_viewLine != null) _viewLine.positionCount = 0;
        if (_lastKnownLine != null) _lastKnownLine.positionCount = 0;
        if (_howlAudio != null) _howlAudio.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        if (_wolf == null) return;
        Color previous = Gizmos.color;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _wolf.ViewRadius);
        Vector3 previousPoint = transform.position;
        for (int i = 0; i <= ViewSegments; i++)
        {
            float angle = -_wolf.ViewAngle * 0.5f + _wolf.ViewAngle * i / ViewSegments;
            Vector3 point = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * transform.forward * _wolf.ViewRadius;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
        Gizmos.DrawLine(previousPoint, transform.position);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _wolf.CaptureRadius);
        Gizmos.color = previous;
    }

    private static Vector3 OnFloor(Vector3 point, float height)
    {
        point.y = height;
        return point;
    }
}
