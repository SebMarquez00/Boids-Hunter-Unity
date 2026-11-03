using UnityEngine;

public class InterestObject : MonoBehaviour
{
    // Solo cuenta objetos activos para el límite de generación; no expone posiciones.
    public static int ActiveCount { get; private set; }

    [Header("Health")]
    [SerializeField] private float _maxHealth = 10f;
    [SerializeField] private float _currentHealth;

    [Header("Reservation")]
    [SerializeField] private BoidAgent _reservedBy;

    public bool IsAlive => _currentHealth > 0f;
    public BoidAgent ReservedBy => _reservedBy;

    private void OnEnable()
    {
        Initialize();
        ActiveCount++;
    }

    private void Initialize()
    {
        _currentHealth = _maxHealth;
        _reservedBy = null;
    }

    private void OnDisable()
    {
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
        _reservedBy = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() => ActiveCount = 0;

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
