using System;
using System.Collections.Generic;

public class StateMachine
{
    public State CurrentState { get; private set; }

    private Dictionary<Enum, State> _states = new Dictionary<Enum, State>();
    private readonly List<KeyValuePair<Enum, Func<bool>>> _globalTransitions = new List<KeyValuePair<Enum, Func<bool>>>();

    public void RegisterState(Enum key, State state)
    {
        _states[key] = state;
    }

    public void ChangeState(Enum key)
    {
        State newState = _states[key];

        if (newState == CurrentState)
        {
            return;
        }

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    // Se evalúan en orden de prioridad dentro de la propia FSM.
    public void RegisterGlobalTransition(Enum key, Func<bool> condition)
    {
        if (condition == null) throw new ArgumentNullException(nameof(condition));
        _globalTransitions.Add(new KeyValuePair<Enum, Func<bool>>(key, condition));
    }

    public void Update()
    {
        foreach (KeyValuePair<Enum, Func<bool>> transition in _globalTransitions)
        {
            if (!transition.Value()) continue;
            ChangeState(transition.Key);
            break;
        }
        CurrentState?.Update();
    }
}
