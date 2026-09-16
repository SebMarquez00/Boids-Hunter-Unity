using System.Collections;
using UnityEngine;

public class WorldBounds : MonoBehaviour
{
    public static WorldBounds Instance { get; private set; }

    [SerializeField] private float _width = 16f;
    [SerializeField] private float _depth = 16f;
    [SerializeField] private bool _drawGizmos = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public Vector3 OutOfBounds(Vector3 position)
    {
        Vector3 newPosition = position;

        if (position.x > _width / 2f)
        {
            newPosition.x = -_width / 2f;
        }
        else if (position.x < -_width / 2f)
        {
            newPosition.x = _width / 2f;
        }

        if (position.z > _depth / 2f)
        {
            newPosition.z = -_depth / 2f;
        }
        else if (position.z < -_depth / 2f)
        {
            newPosition.z = _depth / 2f;
        }

        return newPosition;
    }

    public bool TryGetFreePosition(float height, out Vector3 position)
    {
        // Se usa solo al crear o reubicar objetos, no para dirigir el flocking.
        for (int attempt = 0; attempt < 30; attempt++)
        {
            position = new Vector3(
                Random.Range(-_width / 2f + 1f, _width / 2f - 1f),
                height,
                Random.Range(-_depth / 2f + 1f, _depth / 2f - 1f)
            );

            bool occupied = false;
            foreach (BoidAgent boid in BoidAgent.AllAgents)
            {
                if (boid == null) continue;
                Vector3 offset = boid.transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude < 1.5f * 1.5f)
                {
                    occupied = true;
                    break;
                }
            }

            if (occupied) continue;

            foreach (InterestObject interest in InterestObject.AllObjects)
            {
                if (interest == null) continue;
                Vector3 offset = interest.transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude < 1.5f * 1.5f)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied) return true;
        }

        position = Vector3.zero;
        return false;
    }

    public void RespawnAfter(BoidAgent boid, float delay)
    {
        StartCoroutine(RespawnRoutine(boid, delay));
    }

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

            // Si no hay un lugar libre, volver a intentar sin superponer agentes.
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(_width, 0f, _depth)
        );
    }
}