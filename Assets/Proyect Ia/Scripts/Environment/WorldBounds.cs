using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldBounds : MonoBehaviour
{
    public static WorldBounds Instance { get; private set; }

    [SerializeField, Min(2f)] private float _width = 16f;
    [SerializeField, Min(2f)] private float _depth = 16f;
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private LayerMask _spawnLayers = ~0;

    private const float Clearance = 1.5f;
    private readonly ProximitySensor _spawnSensor = new ProximitySensor();
    private readonly List<BoidAgent> _nearbyBoids = new List<BoidAgent>();
    private readonly List<InterestObject> _nearbyObjects = new List<InterestObject>();

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Vector3 OutOfBounds(Vector3 position)
    {
        Vector3 center = transform.position;
        if (position.x > center.x + _width / 2f) position.x = center.x - _width / 2f;
        else if (position.x < center.x - _width / 2f) position.x = center.x + _width / 2f;
        if (position.z > center.z + _depth / 2f) position.z = center.z - _depth / 2f;
        else if (position.z < center.z - _depth / 2f) position.z = center.z + _depth / 2f;
        return position;
    }

    public bool TryGetFreePosition(float height, out Vector3 position)
    {
        // Consulta física local solo al reubicar; no entrega datos al flocking.
        Physics.SyncTransforms();
        for (int attempt = 0; attempt < 30; attempt++)
        {
            position = RandomPosition(height);
            if (!IsOccupied(position)) return true;
        }
        position = default;
        return false;
    }

    private Vector3 RandomPosition(float height)
    {
        Vector3 center = transform.position;
        float halfWidth = Mathf.Max(0f, _width / 2f - 1f);
        float halfDepth = Mathf.Max(0f, _depth / 2f - 1f);
        return new Vector3(center.x + Random.Range(-halfWidth, halfWidth), height,
            center.z + Random.Range(-halfDepth, halfDepth));
    }

    private bool IsOccupied(Vector3 position)
    {
        _spawnSensor.Scan(position, Clearance, _spawnLayers);
        _spawnSensor.GetDetected(_nearbyBoids);
        _spawnSensor.GetDetected(_nearbyObjects);
        foreach (BoidAgent boid in _nearbyBoids)
            if (TooClose(position, boid.transform.position)) return true;
        foreach (InterestObject interest in _nearbyObjects)
            if (TooClose(position, interest.transform.position)) return true;
        return false;
    }

    private bool TooClose(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;
        return offset.sqrMagnitude < Clearance * Clearance;
    }

    public void RespawnAfter(BoidAgent boid, float delay) => StartCoroutine(RespawnRoutine(boid, delay));

    private IEnumerator RespawnRoutine(BoidAgent boid, float delay)
    {
        float height = boid.transform.position.y;
        boid.gameObject.SetActive(false);
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        while (boid != null)
        {
            if (TryGetFreePosition(height, out Vector3 position))
            {
                boid.transform.position = position;
                boid.gameObject.SetActive(true);
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(_width, 0f, _depth));
    }
}
