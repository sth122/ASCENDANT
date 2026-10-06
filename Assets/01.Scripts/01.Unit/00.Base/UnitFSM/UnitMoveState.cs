// 모든 유닛이 공유할 수 있는 제네릭 Move 상태 클래스
public class UnitMoveState<T> : UnitBaseState<T> where T : UnitController<T>
{
    public UnitMoveState(T owner, UnitStateMachine<T> stateMachine) 
        : base(owner, stateMachine)
    {
    }
}
