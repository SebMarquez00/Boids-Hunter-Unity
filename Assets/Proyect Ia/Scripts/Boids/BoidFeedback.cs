using UnityEngine;

public sealed class BoidFeedback
{
    private readonly Material _material;
    private readonly Color _originalColor;
    private Color _lastColor;
    private bool _hasColor;

    public BoidFeedback(Material material)
    {
        _material = material;
        if (_material != null) _originalColor = _material.color;
    }

    public void Refresh(bool alive, bool sick, Color sickColor)
    {
        if (_material == null) return;
        Color color = !alive ? Color.red : sick ? sickColor : _originalColor;
        if (_hasColor && color == _lastColor) return;
        _material.color = color;
        _lastColor = color;
        _hasColor = true;
    }

    public static void DrawGizmos(Vector3 position, Vector3 velocity, bool threatened, float viewRadius, float separationRadius)
    {
        Gizmos.color = Application.isPlaying && threatened ? Color.magenta : Color.cyan;
        Gizmos.DrawWireSphere(position, viewRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, separationRadius);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(position + Vector3.forward * viewRadius, "Visión / Alignment / Cohesion");
        UnityEditor.Handles.Label(position + Vector3.right * separationRadius, "Separation");
#endif
        if (!Application.isPlaying) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(position, position + velocity);
    }
}
