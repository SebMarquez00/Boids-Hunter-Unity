using System.Collections.Generic;
using UnityEngine;

public class InterestObject : MonoBehaviour
{
    public static List<InterestObject> AllObjects = new List<InterestObject>();

    [Header("Health")]
    [SerializeField] private float _maxHealth = 10f;
    [SerializeField] private float _currentHealth;

    [Header("Reservation")]
    [SerializeField] private BoidAgent _reservedBy;

    public bool IsAlive => _currentHealth > 0f;
    public BoidAgent ReservedBy => _reservedBy;

    private void OnEnable()
    {
        _currentHealth = _maxHealth;
        _reservedBy = null;
        AllObjects.Add(this);
    }

    private void OnDisable()
    {
        AllObjects.Remove(this);
        _reservedBy = null;
    }

    public bool IsAvailableFor(BoidAgent boid)
    {
        if (!isActiveAndEnabled || !IsAlive || boid == null || !boid.IsAlive)
        {
            return false;
        }

        if (_reservedBy != null && (!_reservedBy.IsAlive || !_reservedBy.isActiveAndEnabled))
        {
            _reservedBy = null;
        }

        return _reservedBy == null || _reservedBy == boid;
    }

    public bool TryReserve(BoidAgent boid)
    {
        if (!IsAvailableFor(boid)) return false;
        _reservedBy = boid;
        return true;
    }

    public void Release(BoidAgent boid)
    {
        if (_reservedBy == boid) _reservedBy = null;
    }

    // Devuelve true solo cuando este Boid termina de consumir el objeto.
    public bool Consume(BoidAgent boid, float damage)
    {
        if (!IsAlive || _reservedBy != boid || damage <= 0f) return false;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        if (IsAlive) return false;

        Destroy(gameObject);
        return true;
    }
}
