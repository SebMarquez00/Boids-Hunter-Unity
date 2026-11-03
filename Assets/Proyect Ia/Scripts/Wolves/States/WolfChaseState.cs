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
        if (_agent.TryCaptureHunter())
        {
            _agent.ForgetHunter();
            _stateMachine.ChangeState(WolfStates.Patrol);
            return;
        }
        _agent.NavigateTo(_agent.LastKnownPosition, _agent.ChaseSpeed);
    }

    public override void Exit() => _agent.Stop();
}
