using UnityEngine;

public class HunterGatherState : State
{
    private HunterAgent _agent;
    private float _timer;

    public HunterGatherState(
        HunterAgent agent,
        StateMachine stateMachine
    ) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _timer = 0f;
        _agent.SetGatherProgress(0f);
        Debug.Log("Hunter: Gather");
    }

    public override void Update()
    {
        BoidAgent target = _agent.Target;

        if (target == null || target.IsAlive || !_agent.CanSee(target))
        {
            _stateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        float distance = _agent.DistanceTo(target.transform.position);

        if (distance > _agent.GatherRadius)
        {
            _timer = 0f;
            _agent.SetGatherProgress(0f);
            _agent.MoveTowards(target.transform.position);
            return;
        }

        _agent.Stop();
        _timer += Time.deltaTime;
        _agent.SetGatherProgress(_timer);

        if (_timer >= _agent.GatherDuration)
        {
            if (target.Collect())
            {
                Debug.Log("Hunter: recoleccion completada");
            }

            _stateMachine.ChangeState(HunterStates.Patrol);
            return;
        }
    }

    public override void Exit()
    {
        _timer = 0f;
        _agent.SetGatherProgress(0f);
        _agent.Stop();
        _agent.SetTarget(null);
    }
}
