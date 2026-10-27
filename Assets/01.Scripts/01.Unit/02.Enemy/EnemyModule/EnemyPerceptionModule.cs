using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// UniTask 기반의 비동기 주기적 틱(200ms)으로 동작하는 지각 센서 모듈
/// </summary>
public class EnemyPerceptionModule : UnitBaseModule, System.IDisposable
{
    private readonly Transform _ownerTransform;
    private readonly LayerMask _targetLayer;
    private readonly LayerMask _obstacleLayer;
    private readonly Collider[] _overlapBuffer = new Collider[8];

    private System.Threading.CancellationTokenSource _sensorCts;
    private const int SensorIntervalMilliseconds = 200;     // 0.2초마다 감지 연산 수행

    public float SightRadius { get; set; }
    public float SightAngle { get; set; }
    public float HearingRadius { get; set; }

    public event System.Action<Transform> OnTargetSpotted;
    public event System.Action<Vector3> OnNoiseHeard;

    public EnemyPerceptionModule(Transform ownerTransform, LayerMask targetLayer, LayerMask obstacleLayer)
    {
        _ownerTransform = ownerTransform;
        _targetLayer = targetLayer;
        _obstacleLayer = obstacleLayer;
    }

    public void StartSensorLoop()
    {
        StopSensorLoop();
        _sensorCts = new System.Threading.CancellationTokenSource();
        ReunSensorLoopAsync(_sensorCts.Token).Forget();
    }
    public void StopSensorLoop()
    {
        AsyncUtil.CancelAndDispose(ref _sensorCts);
    }

    private async UniTaskVoid ReunSensorLoopAsync(System.Threading.CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            bool isCanceled = await UniTask.Delay(SensorIntervalMilliseconds, 
                cancellationToken: token).SuppressCancellationThrow();
            if (isCanceled) return;

            EvaluateSensors();
        }
    }

    private void EvaluateSensors()
    {
        if (_ownerTransform == null) return;

        int hitCount = Physics.OverlapSphereNonAlloc(_ownerTransform.position, SightRadius, _overlapBuffer, _targetLayer);

        for(int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];
            PlayerController player = col.GetComponentInParent<PlayerController>();

            if (player == null || player.UnitStatusController.IsDead) continue;

            Vector3 eyePos = _ownerTransform.position + Vector3.up * 1.5f;
            Vector3 targetHeadPos = player.transform.position + Vector3.up * 1.2f;
            Vector3 dirToTarget = targetHeadPos - eyePos;
            float distanceToTarget = dirToTarget.magnitude;

            // 1. 시야각 및 차폐 검사
            float angle = Vector3.Angle(_ownerTransform.forward, dirToTarget.normalized);
            if (angle <= SightAngle * 0.5f)
            {
                if (!Physics.Raycast(eyePos, dirToTarget.normalized, distanceToTarget, _obstacleLayer))
                {
                    OnTargetSpotted?.Invoke(player.transform);
                    break;
                }
            }

            // 2. 청각(Hearing) 검사: 플레이어 달리기 감지 - 추후 Player 발소리 이벤트 추가 예정
            if (distanceToTarget <= HearingRadius && player.InputReader != null && player.InputReader.IsSprinting)
            {
                OnNoiseHeard?.Invoke(player.transform.position);
                break;
            }
        }

        System.Array.Clear(_overlapBuffer, 0, hitCount);
    }

    public void Dispose()
    {
        StopSensorLoop();
    }

    public void DrawGizmos(Transform owner)
    {
#if UNITY_EDITOR
        if (owner == null) return;

        Vector3 eyePos = owner.position + Vector3.up * 1.5f;
        Vector3 forward = owner.forward;

        // 1. 청각 감지 반경 렌더링 (하늘색 반투명 와이어 구체)
        Gizmos.color = new Color(0.2f, 0.7f, 1.0f, 0.4f);
        Gizmos.DrawWireSphere(owner.position, HearingRadius);

        // 2. 시야각 좌/우 경계 벡터 연산
        float halfAngle = SightAngle * 0.5f;
        Vector3 leftBoundaryDir = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward;
        Vector3 rightBoundaryDir = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward;

        // 3. 시야각 경계선 렌더링 (노란색 선)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(eyePos, leftBoundaryDir * SightRadius);
        Gizmos.DrawRay(eyePos, rightBoundaryDir * SightRadius);
        Gizmos.DrawRay(eyePos, forward * SightRadius);

        // 4. 시야각 부채꼴 호(Arc) 렌더링
        Handles.color = new Color(1.0f, 0.92f, 0.016f, 0.15f);
        Handles.DrawSolidArc(eyePos, Vector3.up, leftBoundaryDir, SightAngle, SightRadius);

        Handles.color = Color.yellow;
        Handles.DrawWireArc(eyePos, Vector3.up, leftBoundaryDir, SightAngle, SightRadius);
#endif
    }
}
