using System.Collections.Generic;
using UnityEngine;

// Adquiere candidatos del entorno; cada agente decide qué información utilizar.
public sealed class ProximitySensor
{
    private Collider[] _colliders = new Collider[0];
    private readonly HashSet<Component> _detected = new HashSet<Component>();

    public void Scan(Vector3 origin, float radius, LayerMask layers)
    {
        _colliders = Physics.OverlapSphere(origin, Mathf.Max(0f, radius), layers,
            QueryTriggerInteraction.Collide);
    }

    public void GetDetected<T>(List<T> results) where T : Component
    {
        results.Clear();
        _detected.Clear();

        for (int i = 0; i < _colliders.Length; i++)
        {
            Collider candidate = _colliders[i];
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;

            T component = candidate.GetComponentInParent<T>();
            if (component != null && _detected.Add(component)) results.Add(component);
        }
    }
}
