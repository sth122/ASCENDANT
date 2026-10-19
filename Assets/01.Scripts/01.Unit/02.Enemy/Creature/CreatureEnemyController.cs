using UnityEngine;

/// <summary>
/// Nightmare Creature 전용 파생 컨트롤러
/// 포복 순찰(Crawl), 기상 수색(Alert), 표효(Roar)를 전용 상태 머신으로 구동
/// </summary>
public class CreatureEnemyController : EnemyController
{
    [Header("Creature Specific Settings")]
    [SerializeField] private float _callAlliesRadius = 15.0f;

    private readonly Collider[] _allyBuffer = new Collider[16];
    public float CallAlliesRadius => _callAlliesRadius;

    protected override void Awake()
    {
        _unitId = UnitId.NightmareCreature;
        base.Awake();
    }

    protected override void InitializeStateMachine()
    {
        _stateMachine = new CreatureEnemyStateMachine(this);
        _stateMachine.Initialize((int)UnitState.Crawl);
    }

    #region MyRegion
    protected override void HandleTargetSpotted(Transform target)
    {
        if(_currentTarget == null)
        {
            SetTarget(target);
            _stateMachine.ChangeState((int)UnitState.Roar);
        }
    }

    protected override void HandleNoiseHeard(Vector3 noisePosition)
    {
        // 포복 순찰 중에만 기상 수색(Alert) 상태로 전환
        if (_currentTarget == null && _stateMachine.CurrentState is CreatureCrawlState)
        {
            _stateMachine.ChangeState((int)UnitState.Alert);
        }
    }
    #endregion

    /// <summary>
    /// Roar 상태에서 실행되는 무리 호출 브로드캐스트
    /// </summary>
    public void BroadcastRoarToAllies()
    {
        if (_currentTarget == null) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, _callAlliesRadius, _allyBuffer, _targetLayer);
        for (int i = 0; i < count; i++)
        {
            Collider col = _allyBuffer[i];
            if (col.gameObject == gameObject) continue;

            CreatureEnemyController ally = col.GetComponentInParent<CreatureEnemyController>();
            if (ally != null)
            {
                ally.ReceiveAlertSignal(_currentTarget);
            }
        }
        System.Array.Clear(_allyBuffer, 0, count);
    }

    public void ReceiveAlertSignal(Transform sharedTarget)
    {
        if (_currentTarget != null) return;
        SetTarget(sharedTarget);
        _stateMachine.ChangeState((int)UnitState.Chase);
    }
}
