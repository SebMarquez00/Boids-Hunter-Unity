using UnityEngine;

public class HunterPlaceInterestState : State
{
    private HunterAgent _agent;
    private float _timer;
    public float Elapsed => _timer;

    public HunterPlaceInterestState(
        HunterAgent agent,
        StateMachine stateMachine
    ) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _timer = 0f;
        _agent.Stop();
        Debug.Log("Hunter: colocando objeto");
    }

    public override void Update()
    {
        _agent.Stop();
        _timer += Time.deltaTime;

        if (_timer >= _agent.PlacementDuration)
        {
            _agent.PlaceInterestObject();
            _stateMachine.ChangeState(HunterStates.Patrol);
            return;
        }
    }

    public override void Exit()
    {
        _agent.Stop();
    }
}
