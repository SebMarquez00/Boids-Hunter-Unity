using UnityEngine;

// Referencias de presentación configuradas en la escena, nunca usadas como sensores.
public class SimulationSetup : MonoBehaviour
{
    [SerializeField] private SimulationUI _uiPrefab;
    [SerializeField] private HunterAgent _hunter;
    [SerializeField] private BoidAgent[] _boids;
    [SerializeField] private Camera _camera;

    private void Start() => CreateInterface();

    private void CreateInterface()
    {
        if (_uiPrefab == null || _hunter == null || _camera == null)
        {
            Debug.LogError("Faltan referencias en SimulationSetup.", this);
            return;
        }

        SimulationUI ui = Instantiate(_uiPrefab);
        ui.Initialize(_hunter, _boids, _camera);
    }
}
