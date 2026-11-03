public class HunterDeadState : State
{
    private readonly HunterAgent _agent;
    public float Elapsed { get; private set; }

    public HunterDeadState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        Elapsed = 0f;
        _agent.EnterDead();
    }

    public override void Update()
    {
        Elapsed += _agent.DeltaTime;
        if (Elapsed < _agent.RespawnTime) return;
        _agent.Respawn();
        _stateMachine.ChangeState(_agent.ReturnState);
    }

    public override void Exit() { }
}
