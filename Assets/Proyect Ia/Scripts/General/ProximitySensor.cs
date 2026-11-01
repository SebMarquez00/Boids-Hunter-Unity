using System;
using System.Collections.Generic;
using UnityEngine;

// Adquiere candidatos del entorno; cada agente decide qué información utilizar.
public sealed class ProximitySensor
{
    private Collider[] _colliders = new Collider[16];
    private readonly HashSet<Component> _detected = new HashSet<Component>();
    private int _count;

    public void Scan(Vector3 origin, float radius, LayerMask layers)
    {
        int previousCount = _count;
        do
        {
            _count = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(0f, radius), _colliders,
                layers, QueryTriggerInteraction.Collide);
            if (_count < _colliders.Length) break;
            Array.Resize(ref _colliders, _colliders.Length * 2);
        }
        while (true);

        if (_count < previousCount)
            Array.Clear(_colliders, _count, previousCount - _count);
    }

    public void GetDetected<T>(List<T> results) where T : Component
    {
        results.Clear();
        _detected.Clear();

        for (int i = 0; i < _count; i++)
        {
            Collider candidate = _colliders[i];
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;

            T component = candidate.GetComponentInParent<T>();
            if (component != null && _detected.Add(component)) results.Add(component);
        }
    }
}
