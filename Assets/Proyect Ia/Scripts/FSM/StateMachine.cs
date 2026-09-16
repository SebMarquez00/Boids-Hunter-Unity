using System;
using System.Collections.Generic;

public class StateMachine
{
    public State CurrentState { get; private set; }

    private Dictionary<Enum, State> _states =
        new Dictionary<Enum, State>();

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

    public void Update()
    {
        CurrentState?.Update();
    }
}