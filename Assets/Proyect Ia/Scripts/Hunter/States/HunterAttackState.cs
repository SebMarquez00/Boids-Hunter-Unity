using UnityEngine;

public class HunterAttackState : State
{
    private HunterAgent _agent;

    public HunterAttackState(
        HunterAgent agent,
        StateMachine stateMachine
    ) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        Debug.Log("Hunter: Attack");
    }

    public override void Update()
    {
        // Si la bala impactó, el ataque terminó.
        if (_agent.ProjectileHit)
        {
            _stateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        BoidAgent target = _agent.Target;

        if (target == null || !target.IsAlive || !_agent.CanSee(target))
        {
            _stateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        // Espera el resultado del disparo actual.
        if (_agent.HasActiveProjectile)
        {
            _agent.Stop();
            return;
        }

        float distance =
            _agent.DistanceTo(target.transform.position);

        if (distance <= _agent.MeleeAttackRadius)
        {
            _agent.Stop();

            if (_agent.TryMeleeAttack())
            {
                _stateMachine.ChangeState(HunterStates.Patrol);
                return;
            }
        }
        else if (distance <= _agent.RangeAttackRadius)
        {
            _agent.Stop();
            _agent.Shoot();
        }
        else
        {
            _agent.MoveTowards(target.transform.position);
        }
    }

    public override void Exit()
    {
        _agent.Stop();
        _agent.ClearProjectile();
        _agent.SetTarget(null);
    }
}