public class WolfChaseState : State
{
    private readonly WolfAgent _agent;

    public WolfChaseState(WolfAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter() => _agent.Stop();

    public override void Update()
    {
        if (TryChangeState()) return;
        if (_agent.TryCaptureHunter())
        {
            _agent.ForgetHunter();
            _stateMachine.ChangeState(WolfStates.Patrol);
            return;
        }
        _agent.NavigateTo(_agent.LastKnownPosition, _agent.ChaseSpeed);
    }

    private bool TryChangeState()
    {
        if (!_agent.HasLastKnownPosition)
        {
            _stateMachine.ChangeState(WolfStates.Patrol);
            return true;
        }
        if (_agent.HasVisualContact) return false;
        _stateMachine.ChangeState(WolfStates.Search);
        return true;
    }

    public override void Exit() => _agent.Stop();
}
