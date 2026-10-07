// Unit 전용 베이스 상태 클래스 UnitController<T>를 상속받는 상태는 모두 UnitBaseState<T>를 상속받아야 함
public abstract class UnitBaseState<T> : BaseState<T> where T : UnitController<T>
{
    protected readonly UnitStateMachine<T> unitStateMachine;

    public UnitBaseState(T owner, UnitStateMachine<T> stateMachine) 
        : base(owner, stateMachine)    
    {
        this.unitStateMachine = stateMachine;
    }
}