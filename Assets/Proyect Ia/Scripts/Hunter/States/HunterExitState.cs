using UnityEngine;

public class HunterExitState : State
{
    private readonly HunterAgent _agent;

    public HunterExitState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _agent.Stop();
        _agent.ClearProjectile();
        _agent.SetTarget(null);
        _agent.SetGatherProgress(0f);
        Debug.Log("Hunter: Exit");
    }

    public override void Update()
    {
        if (_agent.IsCompletingExit)
        {
            _agent.CompleteExit();
            _stateMachine.ChangeState(_agent.InitialState);
            return;
        }
        ScenarioZone exit = _agent.Scenario != null ? _agent.Scenario.ExitZone : null;
        if (exit == null || !exit.isActiveAndEnabled)
        {
            _agent.Stop();
            return;
        }
        _agent.NavigateTo(exit.transform.position);
        if (_agent.Navigation != null && _agent.Navigation.HasArrived)
            _agent.BeginExitCompletion();
    }

    public override void Exit() => _agent.Stop();
}
