using UnityEngine;

public class HunterCombat
{
    private readonly HunterAgent _owner;
    private HunterProjectile _activeProjectile;

    public bool HasActiveProjectile => _activeProjectile != null;
    public bool ProjectileHit { get; private set; }

    public HunterCombat(HunterAgent owner)
    {
        _owner = owner;
    }

    public bool TryMeleeAttack(float damage)
    {
        if (damage <= 0f || !CanAttackTarget(_owner.MeleeAttackRadius)) return false;

        _owner.Target.TakeDamage(damage);
        return true;
    }

    public void Shoot(HunterProjectile prefab)
    {
        if (prefab == null || HasActiveProjectile || !CanAttackTarget(_owner.RangeAttackRadius)) return;

        ProjectileHit = false;
        _activeProjectile = Object.Instantiate(prefab, _owner.transform.position, Quaternion.identity);
        _activeProjectile.Initialize(_owner, _owner.Target);
    }

    private bool CanAttackTarget(float attackRadius)
    {
        BoidAgent target = _owner.Target;
        if (!_owner.CanAttack || target == null || !target.IsAlive || !_owner.CanSee(target)) return false;

        return _owner.DistanceTo(target.transform.position) <= attackRadius;
    }

    public void ResolveProjectile(bool hit)
    {
        _activeProjectile = null;
        ProjectileHit = hit;
    }

    public void ClearProjectile()
    {
        if (_activeProjectile != null) _activeProjectile.Cancel();
        _activeProjectile = null;
        ProjectileHit = false;
    }
}
