using UnityEngine;

public class HunterProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _hitRadius = 0.4f;
    [SerializeField] private float _lifeTime = 1.5f;

    private HunterAgent _owner;
    private BoidAgent _target;
    private Vector3 _direction;
    private float _timer;

    public void Initialize(HunterAgent owner, BoidAgent target)
    {
        _owner = owner;
        _target = target;
        _direction = (_target.transform.position - transform.position).normalized;
    }

    private void Update()
    {
        UpdateFlight();
    }

    private void UpdateFlight()
    {
        if (_target == null || !_target.isActiveAndEnabled || !_target.IsAlive)
        {
            Finish(false);
            return;
        }

        Vector3 start = transform.position;
        Vector3 end = start + _direction * _speed * Time.deltaTime;
        if (TryHitTarget(start, end)) return;

        transform.position = end;
        _timer += Time.deltaTime;
        if (_timer >= _lifeTime) Finish(false);
    }

    private bool TryHitTarget(Vector3 start, Vector3 end)
    {
        // Comprueba todo el tramo recorrido para no saltarse al objetivo entre frames.
        Vector3 segment = end - start;
        float progress = segment.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector3.Dot(_target.transform.position - start, segment) / segment.sqrMagnitude)
            : 0f;
        Vector3 closestPoint = start + segment * progress;
        if (Vector3.Distance(closestPoint, _target.transform.position) > _hitRadius) return false;

        transform.position = closestPoint;
        _target.TakeDamage(float.MaxValue);
        Finish(true);
        return true;
    }

    private void Finish(bool hit)
    {
        if (_owner != null) _owner.ResolveProjectile(hit);
        Destroy(gameObject);
    }
}
