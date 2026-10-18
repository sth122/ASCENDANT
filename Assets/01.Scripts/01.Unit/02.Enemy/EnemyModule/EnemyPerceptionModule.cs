using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

/// <summary>
/// UniTask 기반의 비동기 주기적 틱(200ms)으로 동작하는 지각 센서 모듈
/// </summary>
public class EnemyPerceptionModule : IDisposable
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
    public event Action<Transform> OnTargetSpotted;
    public event Action<Vector3> OnNoiseHeard;

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

        int hitCount = Physics.OverlapSphereNonAlloc(_ownerTransform.position, SightRadius, _overlapBuffer);

        for(int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];
            PlayerController player = col.GetComponentInParent<PlayerController>();
            if (player == null) return;

            Vector3 eyePos = _ownerTransform.position + Vector3.up * 1.5f;
            Vector3 targetHeadPos = player.transform.position + Vector3.up * 1.2f;
            Vector3 dirToTarget = targetHeadPos - eyePos;
            float distanceToTarget = dirToTarget.magnitude;

            // 1. 시야각 및 차폐 검사
            float angle = Vector3.Angle(targetHeadPos, dirToTarget.normalized);
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

        Array.Clear(_overlapBuffer, 0, hitCount);
    }

    public void Dispose()
    {
        StopSensorLoop();
    }

}
