// 모든 유닛이 공유할 수 있는 제네릭 Idle 상태 클래스
public class UnitIdleState<T> : UnitBaseState<T> where T : UnitController<T>
{
    public UnitIdleState(T owner, UnitStateMachine<T> stateMachine)
        : base(owner, stateMachine) { }
    public override void Enter()
    {
        base.Enter();
        // 공통적인 물리적 상태(이동, 관성 등)를 초기화하거나, 애니메이션을 재생하는 등의 작업을 수행
    }

    public override void Exit()
    {
        base.Exit();
    }
    public override void Update()
    {
        base.Update();
        // Idle 상태에서의 공통된 로직을 처리
    }
    public override void FixedUpdate()
    {
        base.FixedUpdate();
        // Idle 상태에서의 공통된 물리적 업데이트를 처리
    }
}
