using UnityEngine;

public class HunterFleeState : State
{
    private readonly HunterAgent _agent;
    private const float SelectionRetryInterval = 1f;
    private float _selectionRetry;

    public ScenarioZone SelectedZone { get; private set; }
    public float SelectedPathLength { get; private set; }
    public bool IsWaiting { get; private set; }
    public float WaitElapsed { get; private set; }

    public HunterFleeState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _agent.Stop();
        _agent.ClearProjectile();
        _agent.SetTarget(null);
        _agent.SetGatherProgress(0f);
        IsWaiting = false;
        WaitElapsed = 0f;
        SelectSafeZone();
    }

    public override void Update()
    {
        if (IsWaiting)
        {
            WaitInSafeZone();
            return;
        }
        TravelToSafeZone();
    }

    private void TravelToSafeZone()
    {
        _selectionRetry = Mathf.Max(0f, _selectionRetry - _agent.DeltaTime);
        bool invalidZone = SelectedZone == null || !SelectedZone.isActiveAndEnabled;
        bool blockedPath = _agent.Navigation != null && _agent.Navigation.WaitingForPath;
        if ((invalidZone || blockedPath) && _selectionRetry <= 0f) SelectSafeZone();
        if (SelectedZone == null || !SelectedZone.isActiveAndEnabled)
        {
            _agent.Stop();
            return;
        }
        _agent.NavigateTo(SelectedZone.transform.position);
        if (_agent.Navigation != null && _agent.Navigation.HasArrived
            && SelectedZone.Contains(_agent.transform.position)) BeginWait();
    }

    private void SelectSafeZone()
    {
        _selectionRetry = SelectionRetryInterval;
        NavigationGraph graph = _agent.Navigation != null && _agent.Navigation.Follower != null
            ? _agent.Navigation.Follower.Graph : null;
        HunterSafeZoneSelection.TrySelect(graph, _agent.Scenario, _agent.transform.position,
            out ScenarioZone zone, out float length);
        SelectedZone = zone;
        SelectedPathLength = length;
        _agent.Stop();
    }

    private void BeginWait()
    {
        _agent.Stop();
        IsWaiting = true;
        WaitElapsed = 0f;
    }

    private void WaitInSafeZone()
    {
        _agent.Stop();
        WaitElapsed += _agent.DeltaTime;
        if (WaitElapsed >= _agent.SafeZoneWaitTime)
            _stateMachine.ChangeState(_agent.ReturnWorkState);
    }

    public override void Exit() => _agent.Stop();
}
