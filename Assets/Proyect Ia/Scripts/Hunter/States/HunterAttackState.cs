using UnityEngine;

public class HunterAttackState : State
{
    private readonly HunterAgent _agent;

    public HunterAttackState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        Debug.Log("Hunter: Attack");
    }

    public override void Update()
    {
        if (TryFinishAttack()) return;
        AttackOrPursue();
    }

    private bool TryFinishAttack()
    {
        BoidAgent target = _agent.Target;
        if (!_agent.ProjectileHit && target != null && target.IsAlive && _agent.CanSee(target)) return false;

        _stateMachine.ChangeState(HunterStates.Patrol);
        return true;
    }

    private void AttackOrPursue()
    {
        // Espera el resultado del disparo actual antes de volver a atacar.
        if (_agent.HasActiveProjectile)
        {
            _agent.Stop();
            return;
        }

        BoidAgent target = _agent.Target;
        float distance = _agent.DistanceTo(target.transform.position);
        if (distance <= _agent.MeleeAttackRadius)
        {
            _agent.Stop();
            if (_agent.TryMeleeAttack()) _stateMachine.ChangeState(HunterStates.Patrol);
        }
        else if (distance <= _agent.RangeAttackRadius)
        {
            _agent.Stop();
            _agent.Shoot();
        }
        else
        {
            _agent.Pursue(target);
        }
    }

    public override void Exit()
    {
        _agent.Stop();
        _agent.ClearProjectile();
        _agent.SetTarget(null);
    }
}
