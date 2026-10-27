using UnityEngine;
/// <summary>
/// Unit에 해당하는 모든 객체의 Controller 최상위 제네릭 추상 클래스
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Animator), typeof(UnitStatusController))]
public abstract class UnitController<T> : MonoBehaviour, IInitable where T : UnitController<T>
{
    #region SerializeField Variable
    [Header("Unit ID")]
    [field: SerializeField] protected UnitId _unitId;

    [Header("Layer Mask")]
    [field: SerializeField] protected LayerMask _groundLayer;
    [field: SerializeField] protected LayerMask _targetLayer;
    #endregion

    #region Variables
    protected UnitStateMachine<T> _stateMachine;
    protected Collider _collider;
    protected Rigidbody _rigidbody;
    protected UnitStatusController _unitStatusController;
    #endregion

    #region Property
    public Collider Collider => _collider;
    public Rigidbody Rigidbody => _rigidbody;
    public UnitStatusController UnitStatusController => _unitStatusController;
    public UnitId UnitIdentifier => _unitId;
    public UnitStateMachine<T> StateMachineInstance => _stateMachine;
    #endregion

    protected virtual void Awake()
    {
        _collider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
        _unitStatusController = GetComponent<UnitStatusController>();
    }

    protected virtual void Update() => _stateMachine?.Update();
    protected virtual void FixedUpdate() => _stateMachine?.FixedUpdate();

    protected abstract void InitializeStateMachine();

    public virtual void Init() => InitializeStateMachine();
}
