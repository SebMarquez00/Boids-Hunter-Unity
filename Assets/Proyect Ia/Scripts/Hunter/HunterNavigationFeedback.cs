using TMPro;
using UnityEngine;

public class HunterNavigationFeedback : MonoBehaviour
{
    [SerializeField] private HunterAgent _hunter;
    [SerializeField] private AgentNavigator _navigator;
    [SerializeField] private LineRenderer _pathLine;
    [SerializeField] private TMP_Text _statusLabel;
    [SerializeField] private LineRenderer _threatLine;
    [SerializeField] private LineRenderer _safeZoneMarker;
    private int _shownRevision = -1;
    private int _shownWaypoint = -1;
    private bool _pathsVisible = true;
    private bool _statusVisible = true;
    private WolfAgent _shownThreat;
    private float _threatUntil;
    private ScenarioZone _markedSafeZone;

    public bool PathsVisible => _pathsVisible;
    public bool StatusVisible => _statusVisible;

    public void SetVisibility(bool paths, bool status)
    {
        _pathsVisible = paths;
        _statusVisible = status;
        ApplyVisibility();
    }

    private void OnEnable()
    {
        _shownRevision = _shownWaypoint = -1;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        bool active = isActiveAndEnabled;
        bool visibleInWorld = active && (_hunter == null || (!_hunter.IsDead && !_hunter.IsCompletingExit));
        if (_pathLine != null) _pathLine.enabled = visibleInWorld && _pathsVisible;
        if (_statusLabel != null) _statusLabel.enabled = active && _statusVisible;
        if (_threatLine != null) _threatLine.enabled = visibleInWorld;
        if (_safeZoneMarker != null) _safeZoneMarker.enabled = visibleInWorld;
    }

    private void LateUpdate()
    {
        ApplyVisibility();
        RefreshPath();
        RefreshStatus();
        RefreshThreat();
        RefreshSafeZone();
    }

    private void RefreshPath()
    {
        if (_pathLine == null) return;
        PathFollower follower = _navigator != null ? _navigator.Follower : null;
        if (follower == null || !_navigator.HasDestination || _navigator.WaitingForPath
            || follower.IsBlocked || !follower.IsMoving || follower.HasArrived
            || follower.WaypointIndex >= follower.Path.Count)
        {
            _pathLine.positionCount = 0;
            _shownRevision = _shownWaypoint = -1;
            return;
        }
        if (_shownRevision != _navigator.Revision || _shownWaypoint != follower.WaypointIndex)
        {
            _shownRevision = _navigator.Revision;
            _shownWaypoint = follower.WaypointIndex;
            _pathLine.positionCount = follower.Path.Count - _shownWaypoint + 1;
            for (int i = _shownWaypoint; i < follower.Path.Count; i++)
                _pathLine.SetPosition(i - _shownWaypoint + 1, OnFloor(follower.Path[i]));
        }
        _pathLine.SetPosition(0, OnFloor(follower.transform.position));
    }

    private static Vector3 OnFloor(Vector3 point)
    {
        point.y = 0.12f;
        return point;
    }

    private void RefreshStatus()
    {
        if (_hunter == null || _navigator == null || _statusLabel == null) return;
        string state = _hunter.CurrentState?.GetType().Name.Replace("Hunter", "").Replace("State", "") ?? "Search";
        string movement = _navigator.WaitingForPath ? "Sin camino: reintentando"
            : _navigator.HasArrived ? "Destino alcanzado"
            : _navigator.HasDestination ? "Siguiendo A*" : "";
        if (_hunter.IsDead) movement = "Reaparece en " + _hunter.RespawnRemaining.ToString("0.0") + " s";
        else if (_hunter.IsWaitingInSafeZone)
            movement = SafeZoneName(_hunter.SelectedSafeZone) + ": espera " + _hunter.SafeZoneWaitRemaining.ToString("0.0") + " s";
        else if (_hunter.SelectedSafeZone != null)
            movement = "Hacia " + SafeZoneName(_hunter.SelectedSafeZone) + " | " + movement;
        _statusLabel.text = "CAZADOR | " + state
            + " | Recolecciones: " + _hunter.GatherCount + "/" + _hunter.RequiredGathers
            + " | Ciclos: " + _hunter.CompletedCycles
            + "\n" + (movement.Length > 0 ? movement + " | " : "")
            + "Calculos A*: " + _navigator.CalculationCount;
    }

    private string SafeZoneName(ScenarioZone zone)
    {
        if (zone == null || _hunter.Scenario == null) return "Refugio";
        for (int i = 0; i < _hunter.Scenario.SafeZones.Count; i++)
            if (_hunter.Scenario.SafeZones[i] == zone) return "Refugio " + (i + 1);
        return "Refugio";
    }

    private void RefreshThreat()
    {
        if (_threatLine == null) return;
        WolfAgent wolf = _hunter != null && !_hunter.IsDead ? _hunter.DetectedWolf : null;
        if (!IsStillVisible(wolf)) wolf = null;
        if (_shownThreat != wolf)
        {
            _shownThreat = wolf;
            _threatUntil = Time.time + 0.35f;
        }
        bool visible = wolf != null && Time.time < _threatUntil;
        _threatLine.positionCount = visible ? 2 : 0;
        if (!visible) return;
        _threatLine.SetPosition(0, _hunter.transform.position + Vector3.up * 0.15f);
        _threatLine.SetPosition(1, wolf.transform.position + Vector3.up * 0.15f);
    }

    private bool IsStillVisible(WolfAgent wolf)
    {
        if (wolf == null || !wolf.isActiveAndEnabled) return false;
        Vector3 difference = wolf.transform.position - _hunter.transform.position;
        difference.y = 0f;
        return difference.sqrMagnitude <= _hunter.VisionRadius * _hunter.VisionRadius
            && !Physics.Linecast(_hunter.transform.position, wolf.transform.position,
                _hunter.ObstacleMask, QueryTriggerInteraction.Ignore);
    }

    private void RefreshSafeZone()
    {
        if (_safeZoneMarker == null) return;
        ScenarioZone zone = _hunter != null && !_hunter.IsDead ? _hunter.SelectedSafeZone : null;
        if (zone == _markedSafeZone) return;
        _markedSafeZone = zone;
        _safeZoneMarker.positionCount = zone != null ? 4 : 0;
        if (zone == null) return;
        Vector3 center = zone.transform.position;
        center.y = 0.18f;
        _safeZoneMarker.SetPosition(0, center + Vector3.forward * 0.75f);
        _safeZoneMarker.SetPosition(1, center + Vector3.right * 0.75f);
        _safeZoneMarker.SetPosition(2, center + Vector3.back * 0.75f);
        _safeZoneMarker.SetPosition(3, center + Vector3.left * 0.75f);
    }

    private void OnDisable()
    {
        if (_pathLine != null) _pathLine.positionCount = 0;
        if (_threatLine != null) _threatLine.positionCount = 0;
        if (_safeZoneMarker != null) _safeZoneMarker.positionCount = 0;
        _shownRevision = _shownWaypoint = -1;
        _shownThreat = null;
        _markedSafeZone = null;
        ApplyVisibility();
    }
}
