using UnityEngine;

public sealed class BoidHealth
{
    private bool _collected;

    public float CurrentHealth { get; private set; }
    public float SickTime { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;
    public bool IsSick => SickTime > 0f;

    public void Reset(float maxHealth)
    {
        CurrentHealth = maxHealth;
        SickTime = 0f;
        _collected = false;
    }

    public void Tick(float deltaTime)
    {
        SickTime = Mathf.Max(0f, SickTime - deltaTime);
    }

    public bool TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0f) return false;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        if (IsAlive) return false;
        SickTime = 0f;
        return true;
    }

    public void BecomeSick(float duration)
    {
        if (IsAlive) SickTime = Mathf.Max(0f, duration);
    }

    public float GetMaxSpeed(float normalSpeed, float sickMultiplier)
    {
        return normalSpeed * (IsSick ? sickMultiplier : 1f);
    }

    public bool TryCollect(BoidAgent owner, WorldBounds bounds, float respawnDelay)
    {
        if (IsAlive || _collected || !owner.gameObject.activeInHierarchy || bounds == null) return false;
        _collected = true;
        bounds.RespawnAfter(owner, respawnDelay);
        return true;
    }
}
