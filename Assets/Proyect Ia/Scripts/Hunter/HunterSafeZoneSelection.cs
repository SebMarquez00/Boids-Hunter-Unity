using UnityEngine;

public static class HunterSafeZoneSelection
{
    public static bool TrySelect(NavigationGraph graph, ScenarioReferences scenario, Vector3 origin,
        out ScenarioZone zone, out float pathLength)
    {
        zone = null;
        pathLength = float.PositiveInfinity;
        if (graph == null || scenario == null) return false;

        foreach (ScenarioZone candidate in scenario.SafeZones)
        {
            if (candidate == null || !candidate.isActiveAndEnabled) continue;
            // Comparamos caminos transitables completos, no distancias en linea recta.
            if (!graph.TryFindPath(origin, candidate.transform.position, NavigationAccess.Hunter,
                out _, out float length) || length >= pathLength) continue;
            zone = candidate;
            pathLength = length;
        }
        return zone != null;
    }
}
