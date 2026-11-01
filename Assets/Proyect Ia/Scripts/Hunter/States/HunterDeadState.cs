using UnityEngine;

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
        Debug.Log("Hunter: Dead");
    }

    public override void Update()
    {
        Elapsed += Time.deltaTime;
        if (Elapsed < _agent.RespawnTime) return;
        _agent.Respawn();
        _stateMachine.ChangeState(_agent.ReturnState);
        Debug.Log("Hunter: reaparicion en el inicio");
    }

    public override void Exit() { }
}
