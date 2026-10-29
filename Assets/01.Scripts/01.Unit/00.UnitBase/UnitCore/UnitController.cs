using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Player(HFSM)와 Enemy/Boss(BT)가 공통으로 상속받는 최상위 템플릿 베이스 컨트롤러
/// Update Guard 및 템플릿 메서드(TickController, FixedTickController)를 제공합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public abstract class UnitController : MonoBehaviour, IInitable
{
    #region SerializeField
    [Header("Unit Identity & Layer Masks")]
    [field: SerializeField] protected UnitId _unitId;
    [field: SerializeField] protected LayerMask _groundLayer;
    [field: SerializeField] protected LayerMask _targetLayer;

    [Header("Ground & Slope Detection Settings (공통 지면 및 경사면 설정)")]
    [SerializeField] protected GroundCheckConfig _groundCheckConfig = GroundCheckConfig.Default;
    #endregion

    #region Unity Components
    protected Rigidbody _rigidbody;
    protected Collider _collider;
    #endregion

    #region Component Properties
    public Rigidbody Rigidbody => _rigidbody;
    public Collider Collider => _collider;
    public UnitId UnitIdentifier => _unitId;
    #endregion

    #region Domain Abstract Properties
    public abstract UnitStatusController BaseStatusController { get; }
    public abstract UnitAnimController BaseAnimController { get; }
    #endregion

    protected bool _isControllerActive = true;
    public bool IsControllerActive => _isControllerActive;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        SetupComponents();
    }

    protected virtual void Start()
    {
        Init();
    }

    protected virtual void Update()
    {
        if (!_isControllerActive) return;
        TickController();
    }

    protected virtual void FixedUpdate()
    {
        if (!_isControllerActive) return;
        FixedTickController();
    }

    public virtual void Init() { }

    /// <summary>
    /// 컨트롤러의 틱 평가 루프 활성화/비활성화 (사망 또는 컷신 연출 시 사용)
    /// </summary>
    public void SetControllerActive(bool isActive) => _isControllerActive = isActive;

    protected abstract void SetupComponents();
    protected abstract void TickController();
    protected virtual void FixedTickController() { }

    #region 공통 애니메이션 재생 및 비동기 대기 헬퍼 API
    /// <summary>
    /// [공통 헬퍼] 지정된 상태를 재생하고, 클립의 실제 재생 길이(배속 반영)만큼 비동기 대기합니다.
    /// </summary>
    /// <param name="stateType">재생할 애니메이션 상태</param>
    /// <param name="ct">취소 토큰</param>
    /// <param name="force">동일 상태 재진입 강제 여부</param>
    /// <param name="speedMultiplier">재생 배속 계수</param>
    /// <param name="customDuration">기본 클립 길이 대신 명시적 시간 대기가 필요할 경우 전달</param>
    public async UniTask PlayStateAndWaitAsync(AnimStateType stateType, CancellationToken ct, bool force = true, float speedMultiplier = 1.0f, float customDuration = -1f)
    {
        if (BaseAnimController == null) return;

        // 1. 애니메이션 상태 재생
        BaseAnimController.PlayState(stateType, force);

        // 2. 대기 시간 산출 (커스텀 시간이 없을 경우 GetStateDuration을 통해 실제 클립 길이 조회)
        float clipDuration = (customDuration > 0f) ? customDuration : BaseAnimController.GetStateDuration(stateType);
        float actualDuration = clipDuration / Mathf.Max(0.01f, speedMultiplier);

        // 3. 배속이 반영된 실제 시간만큼 대기
        await UniTask.Delay(TimeSpan.FromSeconds(actualDuration), cancellationToken: ct);
    }
    #endregion

#if UNITY_EDITOR
    protected virtual void OnValidate() { }
#endif
}