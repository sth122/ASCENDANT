using Unity.Cinemachine;
using UnityEngine;
using Util = DebugLogger<CameraManager>;

/// <summary>
/// Cinemachine 가상 카메라 Player의 시선 및 우선순위, 타깃을 중앙 통제하는 매니저 클래스
/// </summary>
public class CameraManager : MonoSingleton<CameraManager>
{
    private CinemachineCamera _freeLookCamera; // 일반 탐험/이동용
    private CinemachineCamera _lockOnCamera;   // 타겟 락온용
    private CinemachineCamera _bossCutsceneCamera; // 보스 컷신용

    private const int ActivePriority = 20;
    private const int InactivePriority = 10;

    public bool IsLockOnMode { get; private set; }

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
        ResetToFreeLook();
    }

    /// <summary>
    /// 씬 부트스트래퍼가 로드 완료 시점에 호출하여 씬 가상 카메라들을 등록
    /// </summary>
    public void RegisterSceneCameras(CinemachineCamera freeLookCam, CinemachineCamera lockOnCam, CinemachineCamera bossCam = null)
    {
        _freeLookCamera = freeLookCam;
        _lockOnCamera = lockOnCam;
        _bossCutsceneCamera = bossCam;

        ResetToFreeLook();
        Util.Log("[씬 가상 카메라 등록 및 기본 시점 리셋 완료]");
    }

    /// <summary>
    /// 씬 전환 시 가상 카메라 참조를 해제하여 유령 참조를 방지
    /// </summary>
    public void UnregisterSceneCameras()
    {
        _freeLookCamera = null;
        _lockOnCamera = null;
        _bossCutsceneCamera = null;
        IsLockOnMode = false;
        Util.Log("[씬 가상 카메라 등록 해제]");
    }

    /// <summary>
    /// 스폰된 플레이어 트랜스폼을 가상 카메라들의 추적 대상으로 등록
    /// </summary>
    /// <param name="playerTransform">스폰된 플레이어의 트랜스폼</param>
    public void SetPlayerTarget(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            Util.LogError("전달된 플레이어 트랜스폼이 Null");
            return;
        }

        // 1. FreeLook 가상 카메라 타깃 바인딩 (추적 및 주시)
        if (_freeLookCamera != null)
        {
            _freeLookCamera.Target.TrackingTarget = playerTransform;
            _freeLookCamera.Target.LookAtTarget = playerTransform;
        }

        // 2. LockOn 가상 카메라 기본 추적 타깃 바인딩
        if (_lockOnCamera != null)
        {
            _lockOnCamera.Target.TrackingTarget = playerTransform;
        }

        Util.Log($"[Cinemachine 타깃 주입 완료] {playerTransform.name}");
    }

    /// <summary>
    /// 일반 3인칭 자유 시점으로 복귀
    /// </summary>
    public void ResetToFreeLook()
    {
        IsLockOnMode = false;
        SetCameraPriority(_freeLookCamera, ActivePriority);
        SetCameraPriority(_lockOnCamera, InactivePriority);
        SetCameraPriority(_bossCutsceneCamera, InactivePriority);
    }

    /// <summary>
    /// 적 타겟팅(Lock-On) 모드 활성화
    /// </summary>
    public void EnableLockOn(Transform targetEnemy)
    {
        if (targetEnemy == null)
        {
            ResetToFreeLook();
            return;
        }

        IsLockOnMode = true;

        if (_lockOnCamera != null)
        {
            _lockOnCamera.Target.LookAtTarget = targetEnemy;
        }

        SetCameraPriority(_lockOnCamera, ActivePriority);
        SetCameraPriority(_freeLookCamera, InactivePriority);
    }

    /// <summary>
    /// 보스 컷신 연출용 카메라를 최우선 순위로 활성화합니다.
    /// </summary>
    public void PlayBossCutscene(Transform bossTransform)
    {
        if (_bossCutsceneCamera != null && bossTransform != null)
        {
            _bossCutsceneCamera.Target.LookAtTarget = bossTransform;
            SetCameraPriority(_bossCutsceneCamera, ActivePriority + 10);
        }
    }

    private void SetCameraPriority(CinemachineCamera cam, int priority)
    {
        if (cam != null)
        {
            cam.Priority = priority;
        }
    }
}
