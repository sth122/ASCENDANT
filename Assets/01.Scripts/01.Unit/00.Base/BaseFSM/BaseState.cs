// 모든 FSM 범용 제네릭 클래스
public abstract class BaseState<T> : IState where T : class
{
    protected readonly T owner;
    protected readonly StateMachine<T> stateMachine;

    public BaseState(T owner, StateMachine<T> stateMachine)
    {
        this.owner = owner;
        this.stateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void FixedUpdate() { }
    public virtual void Update() { }
}
