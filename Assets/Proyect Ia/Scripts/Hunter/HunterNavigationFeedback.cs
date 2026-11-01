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
    private WolfAgent _shownThreat;
    private float _threatUntil;
    private ScenarioZone _markedSafeZone;

    private void LateUpdate()
    {
        RefreshPath();
        RefreshStatus();
        RefreshThreat();
        RefreshSafeZone();
    }

    private void RefreshPath()
    {
        if (_navigator == null || _pathLine == null || _shownRevision == _navigator.Revision) return;
        _shownRevision = _navigator.Revision;
        PathFollower follower = _navigator.Follower;
        _pathLine.positionCount = follower != null && !follower.IsBlocked ? follower.Path.Count : 0;
        for (int i = 0; i < _pathLine.positionCount; i++)
        {
            Vector3 point = follower.Path[i];
            point.y = 0.12f;
            _pathLine.SetPosition(i, point);
        }
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
        _shownRevision = -1;
        _shownThreat = null;
        _markedSafeZone = null;
    }
}
