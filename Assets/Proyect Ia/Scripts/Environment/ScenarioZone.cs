using UnityEngine;

public enum ScenarioZoneType
{
    Start,
    HuntingGround,
    Safe,
    Exit
}

public class ScenarioZone : MonoBehaviour
{
    [SerializeField] private ScenarioZoneType _type;
    [SerializeField] private Vector2 _size = Vector2.one;

    public ScenarioZoneType Type => _type;
    public Vector2 Size => _size;

    public bool Contains(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(localPosition.x) <= Mathf.Abs(_size.x) * 0.5f
            && Mathf.Abs(localPosition.z) <= Mathf.Abs(_size.y) * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = _type switch
        {
            ScenarioZoneType.Start => Color.green,
            ScenarioZoneType.HuntingGround => Color.yellow,
            ScenarioZoneType.Safe => Color.magenta,
            ScenarioZoneType.Exit => Color.cyan,
            _ => Color.white
        };
        Vector3 size = new Vector3(Mathf.Abs(_size.x), 0.2f, Mathf.Abs(_size.y));
        Gizmos.DrawWireCube(Vector3.up * 0.1f, size);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
