using UnityEngine;

public class HunterSearchState : State
{
    private readonly HunterAgent _agent;
    private Vector3 _destination;
    private bool _hasDestination;
    private float _selectionRetry;

    public HunterSearchState(HunterAgent agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _agent.Stop();
        _agent.SetTarget(null);
        _hasDestination = false;
        _selectionRetry = 0f;
    }

    public override void Update()
    {
        if (_agent.Scenario == null || _agent.Scenario.HuntingZone == null)
        {
            _agent.Stop();
            return;
        }

        if (!_agent.IsInHuntingGround)
        {
            _hasDestination = false;
            _agent.NavigateTo(_agent.Scenario.HuntingZone.transform.position);
            return;
        }

        _agent.PlaceInterestObject();
        if (TryChangeState()) return;
        SearchHuntingGround();
    }

    private bool TryChangeState()
    {
        BoidAgent eliminated = _agent.FindClosestDeadBoid();
        if (eliminated != null)
        {
            _agent.SetTarget(eliminated);
            _stateMachine.ChangeState(HunterStates.Gather);
            return true;
        }

        if (!_agent.CanAttack) return false;
        BoidAgent target = _agent.FindClosestAliveBoid();
        if (target == null) return false;
        _agent.SetTarget(target);
        _stateMachine.ChangeState(HunterStates.Attack);
        return true;
    }

    private void SearchHuntingGround()
    {
        if (_agent.Navigation == null || _agent.Navigation.Follower == null) return;
        _selectionRetry = Mathf.Max(0f, _selectionRetry - _agent.DeltaTime);
        if (!_hasDestination || _agent.Navigation.HasArrived)
        {
            if (_selectionRetry > 0f) return;
            _hasDestination = SelectDestination();
            if (!_hasDestination)
            {
                _agent.Stop();
                _selectionRetry = 1f;
                return;
            }
        }
        _agent.NavigateTo(_destination);
    }

    private bool SelectDestination()
    {
        NavigationGraph graph = _agent.Navigation.Follower.Graph;
        if (graph == null) return false;
        int selected = -1;
        int candidates = 0;
        float minimumDistance = Mathf.Max(1f, _agent.WaypointCheckDistance * 2f);
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            NavigationNode node = graph.Nodes[i];
            if (!graph.CanUseNode(i, NavigationAccess.Hunter)
                || !_agent.Scenario.HuntingZone.Contains(node.Position)
                || _agent.DistanceTo(node.Position) <= minimumDistance) continue;

            candidates++;
            if (Random.Range(0, candidates) == 0) selected = i;
        }

        if (selected < 0) return false;
        _destination = graph.Nodes[selected].Position;
        return true;
    }

    public override void Exit()
    {
        _agent.Stop();
    }
}
