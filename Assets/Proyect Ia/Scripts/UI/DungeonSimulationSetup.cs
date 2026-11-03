using UnityEngine;

// Las referencias de presentacion se configuran en la escena; no son sensores.
public class DungeonSimulationSetup : MonoBehaviour
{
    [SerializeField] private DungeonSimulationUI _uiPrefab;
    [SerializeField] private DungeonSimulationUI _ui;
    [SerializeField] private HunterAgent _hunter;
    [SerializeField] private ScenarioReferences _scenario;
    [SerializeField] private WolfAgent[] _wolves = new WolfAgent[0];
    [SerializeField] private Camera _camera;

    public DungeonSimulationUI UI => _ui;
    public HunterAgent Hunter => _hunter;
    public ScenarioReferences Scenario => _scenario;
    public WolfAgent[] Wolves => _wolves;
    public Camera SceneCamera => _camera;

    private void Start() => CreateInterface();

    private void CreateInterface()
    {
        if ((_ui == null && _uiPrefab == null) || _hunter == null || _scenario == null || _camera == null)
        {
            Debug.LogError("Faltan referencias en DungeonSimulationSetup.", this);
            return;
        }
        if (_ui == null) _ui = Instantiate(_uiPrefab);
        _ui.Initialize(_hunter, _scenario, _wolves, _camera);
    }
}
