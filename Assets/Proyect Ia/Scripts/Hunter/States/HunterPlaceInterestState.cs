public class HunterPlaceInterestState : State
{
    private readonly HunterAgent _agent;
    private float _timer;

    public float Elapsed => _timer;

    public HunterPlaceInterestState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _timer = 0f;
        _agent.Stop();
    }

    public override void Update()
    {
        UpdatePlacement();
    }

    private void UpdatePlacement()
    {
        _agent.Stop();
        _timer += _agent.DeltaTime;
        if (_timer < _agent.PlacementDuration) return;

        _agent.PlaceInterestObject();
        _stateMachine.ChangeState(_agent.ReturnState);
    }

    public override void Exit()
    {
        _agent.Stop();
    }
}
