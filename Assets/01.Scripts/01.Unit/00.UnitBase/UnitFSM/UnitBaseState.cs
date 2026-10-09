// Unit 전용 베이스 상태 클래스 UnitController<T>를 상속받는 상태는 모두 UnitBaseState<T>를 상속받아야 함
public abstract class UnitBaseState<T> : BaseState<T> where T : UnitController<T>
{
    protected readonly UnitStateMachine<T> unitStateMachine;

#if UNITY_EDITOR
    private readonly string _ownerTypeName;
    private readonly string _stateTypeName;
#endif

    public UnitBaseState(T owner, UnitStateMachine<T> stateMachine)
        : base(owner, stateMachine)
    {
        this.unitStateMachine = stateMachine;

#if UNITY_EDITOR
        _ownerTypeName = typeof(T).Name;
        _stateTypeName = GetType().Name;
#endif
    }

    public override void Enter()
    {
#if UNITY_EDITOR
        DebugLogger<T>.Log($"[{_stateTypeName}] 진입");
#endif
    }
    public override void Exit()
    {
#if UNITY_EDITOR
        DebugLogger<T>.Log($"[{_ownerTypeName}] 퇴장");
#endif
    }
}