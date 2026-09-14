using UnityEngine;

public class WorldBounds : MonoBehaviour
{
    public static WorldBounds Instance { get; private set; }

    [SerializeField] private float _width = 28f;
    [SerializeField] private float _depth = 28f;
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