using System.Collections.Generic;
using UnityEngine;

public class ScenarioReferences : MonoBehaviour
{
    [SerializeField] private ScenarioZone _startZone;
    [SerializeField] private ScenarioZone _huntingZone;
    [SerializeField] private ScenarioZone[] _safeZones = new ScenarioZone[0];
    [SerializeField] private ScenarioZone _exitZone;
    [SerializeField] private Transform _hunterSpawn;
    [SerializeField] private WorldBounds _huntingBounds;
    [SerializeField] private BoidAgent[] _boids = new BoidAgent[0];

    public ScenarioZone StartZone => _startZone;
    public ScenarioZone HuntingZone => _huntingZone;
    public IReadOnlyList<ScenarioZone> SafeZones => _safeZones;
    public ScenarioZone ExitZone => _exitZone;
    public Transform HunterSpawn => _hunterSpawn;
    public WorldBounds HuntingBounds => _huntingBounds;
    public IReadOnlyList<BoidAgent> Boids => _boids;

    public bool ContainsSafeZone(Vector3 position)
    {
        foreach (ScenarioZone zone in _safeZones)
            if (zone != null && zone.Contains(position)) return true;
        return false;
    }
}
