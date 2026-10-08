using UnityEngine;

// Unit에 해당하는 모든 객체의 Controller 제네릭 추상 클래스
[RequireComponent(typeof(Rigidbody), typeof(Animator), typeof(UnitStatController))]
public abstract class UnitController<T> : MonoBehaviour, IInitable where T : UnitController<T>
{
    #region SerializeField Variable
    [field: SerializeField] protected UnitId unitId;
    #endregion

    #region Variables
    protected StateMachine<T> _stateMachine;
    protected Rigidbody _rigidbody;
    protected Animator _animator;
    protected UnitStatController _statController;
    #endregion

    #region Property
    public Rigidbody Rigidbody => _rigidbody;
    public UnitStatController StatController => _statController;
    #endregion

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _statController = GetComponent<UnitStatController>();
    }

    protected virtual void Update() => _stateMachine.Update();
    protected virtual void FixedUpdate() => _stateMachine.FixedUpdate();

    protected abstract void InitializeStateMachine();

    public virtual void Init()
    {
        InitializeStateMachine();
    }
}
