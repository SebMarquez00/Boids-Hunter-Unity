using TMPro;
using UnityEngine;

public class HunterNavigationFeedback : MonoBehaviour
{
    [SerializeField] private HunterAgent _hunter;
    [SerializeField] private AgentNavigator _navigator;
    [SerializeField] private LineRenderer _pathLine;
    [SerializeField] private TMP_Text _statusLabel;
    private int _shownRevision = -1;

    private void Update()
    {
        RefreshPath();
        RefreshStatus();
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
        _statusLabel.text = "CAZADOR | " + state
            + (movement.Length > 0 ? " | " + movement : "")
            + " | Recolecciones: " + _hunter.GatherCount
            + " | Calculos A*: " + _navigator.CalculationCount;
    }

    private void OnDisable()
    {
        if (_pathLine != null) _pathLine.positionCount = 0;
    }
}
