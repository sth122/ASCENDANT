using UnityEngine;

public interface IState
{
    public void Enter();
    public void Exit();
    public void Update();
    public void FixedUpdate();
}

/// <summary>
/// StateMachine
/// </summary>
/// <typeparam name="T"></typeparam>
public class StateMachine<T>
{
    protected IState currentState;
    public IState CurrentState => currentState;
    public T obj;

    public StateMachine(T owner)
    {
        this.obj = owner;
    }

    public void ChangeState(IState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    public void Update() => currentState?.Update();
    public void FixedUpdate() => currentState?.FixedUpdate();
}