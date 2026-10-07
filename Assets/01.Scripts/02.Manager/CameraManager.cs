using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoSingleton<CameraManager>
{
    [Header("Virtual Cameras")]
    [SerializeField] private CinemachineCamera _freeLookCamera; // 일반 탐험/이동용
    [SerializeField] private CinemachineCamera _lockOnCamera;   // 타겟 락온용
    [SerializeField] private CinemachineCamera _bossCutsceneCamera; // 보스 컷신용

    private const int ActivePriority = 20;
    private const int InactivePriority = 10;

    public bool IsLockOnMode {  get; private set; }

    protected override void Awake()
    {
        base.Awake();
        ResetToFreeLook();
    }

    /// <summary>
    /// 기본 자유 시점으로 복귀
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
        if(targetEnemy == null)
        {
            ResetToFreeLook();
            return;
        }

        IsLockOnMode = true;

        _lockOnCamera.LookAt = targetEnemy;
        SetCameraPriority(_lockOnCamera, ActivePriority);
        SetCameraPriority(_freeLookCamera, InactivePriority);
    }

    public void PlayBossCutscene(Transform bossTransform)
    {
        if (bossTransform != null)
        {
            _bossCutsceneCamera.LookAt = bossTransform;
        }

        SetCameraPriority(_bossCutsceneCamera, ActivePriority + 10);
    }

    private void SetCameraPriority(CinemachineCamera cam, int priority)
    {
        if(cam !=null)
        {
            cam.Priority = priority;
        }
    }
}
