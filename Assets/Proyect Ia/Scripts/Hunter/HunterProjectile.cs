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

    public void Initialize(
        HunterAgent owner,
        BoidAgent target
    )
    {
        _owner = owner;
        _target = target;

        _direction =
            (_target.transform.position - transform.position).normalized;
    }

    private void Update()
    {
        if (_target == null || !_target.IsAlive)
        {
            Finish(false);
            return;
        }

        Vector3 start = transform.position;

        Vector3 end = start
            + _direction * _speed * Time.deltaTime;

        // Busca el punto del recorrido más cercano al objetivo.
        Vector3 segment = end - start;
        float segmentLengthSquared = segment.sqrMagnitude;

        float progress = 0f;

        if (segmentLengthSquared > 0f)
        {
            progress = Mathf.Clamp01(
                Vector3.Dot(
                    _target.transform.position - start,
                    segment
                ) / segmentLengthSquared
            );
        }

        Vector3 closestPoint = start + segment * progress;

        if (Vector3.Distance(
            closestPoint,
            _target.transform.position
        ) <= _hitRadius)
        {
            transform.position = closestPoint;

            _target.TakeDamage(float.MaxValue);

            Finish(true);
            return;
        }

        transform.position = end;

        _timer += Time.deltaTime;

        if (_timer >= _lifeTime)
        {
            Finish(false);
        }
    }

    private void Finish(bool hit)
    {
        if (_owner != null)
        {
            _owner.ResolveProjectile(hit);
        }

        Destroy(gameObject);
    }
}