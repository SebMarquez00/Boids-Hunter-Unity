using UnityEngine;

public class HunterGatherState : State
{
    private readonly HunterAgent _agent;
    private float _timer;

    public HunterGatherState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        if (_agent.UsesNavigation) _agent.SetTarget(_agent.FindClosestDeadBoid());
        ResetProgress();
        Debug.Log("Hunter: Gather");
    }

    public override void Update()
    {
        BoidAgent target = _agent.Target;
        if (target == null || target.IsAlive || !_agent.CanSee(target))
        {
            _stateMachine.ChangeState(_agent.ReturnState);
            return;
        }

        ApproachOrGather(target);
    }

    private void ApproachOrGather(BoidAgent target)
    {
        if (_agent.DistanceTo(target.transform.position) > _agent.GatherRadius)
        {
            ResetProgress();
            _agent.ApproachTarget(target);
            return;
        }

        _agent.Stop();
        _timer += _agent.DeltaTime;
        _agent.SetGatherProgress(_timer);
        if (_timer < _agent.GatherDuration) return;

        if (target.Collect())
        {
            _agent.RegisterGather();
            Debug.Log("Hunter: recoleccion completada");
        }
        _stateMachine.ChangeState(_agent.ReturnWorkState);
    }

    private void ResetProgress()
    {
        _timer = 0f;
        _agent.SetGatherProgress(0f);
    }

    public override void Exit()
    {
        ResetProgress();
        _agent.Stop();
        _agent.SetTarget(null);
    }
}
