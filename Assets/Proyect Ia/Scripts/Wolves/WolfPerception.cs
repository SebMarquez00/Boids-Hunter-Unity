using UnityEngine;

public class WolfPerception
{
    public bool HasVisualContact { get; private set; }
    public bool HasLastKnownPosition { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }

    public static bool CanSee(Vector3 origin, Vector3 forward, Vector3 target,
        float radius, float angle, LayerMask obstacleMask)
    {
        Vector3 direction = target - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude > Mathf.Max(0f, radius) * Mathf.Max(0f, radius)) return false;
        forward.y = 0f;
        if (direction.sqrMagnitude > 0.000001f
            && Vector3.Angle(forward, direction) > Mathf.Clamp(angle, 0f, 360f) * 0.5f) return false;
        return !Physics.Linecast(origin, target, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    public void Observe(Vector3 position)
    {
        HasVisualContact = true;
        Remember(position);
    }

    public void Remember(Vector3 position)
    {
        LastKnownPosition = position;
        HasLastKnownPosition = true;
    }

    public void LoseVisualContact() => HasVisualContact = false;

    public void Forget()
    {
        HasVisualContact = false;
        HasLastKnownPosition = false;
        LastKnownPosition = default;
    }
}
