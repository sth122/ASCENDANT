using Unity.Cinemachine;
using UnityEngine;
using Util = DebugLogger<CameraManager>;

/// <summary>
/// Cinemachine 가상 카메라 Player의 시선 및 우선순위, 타깃을 중앙 통제하는 매니저 클래스
/// </summary>
public class CameraManager : MonoSingleton<CameraManager>
{
    private CinemachineCamera _freeLookCamera;      // 일반 탐험/이동용
    private CinemachineCamera _lockOnCamera;        // 타겟 락온용
    private CinemachineCamera _customCutsceneCamera; // 개별 기믹 전용 컷신용

    private Transform _playerTarget;
    private Transform _lockOnTarget;
    private Transform _lockOnPivot;

    private const int ActivePriority = 20;
    private const int InactivePriority = 10;
    private const float LockOnPivotTurnSharpness = 14f;
    private const float MinLockOnHorizontalSqrDist = 0.25f;

    public bool IsLockOnMode { get; private set; }

    protected override void Awake()
    {
        _isDDOL = true;
        base.Awake();
        EnsureLockOnPivot();
        ResetToFreeLook();
    }

    private void EnsureLockOnPivot()
    {
        if (_lockOnPivot != null) return;

        GameObject pivotGo = new GameObject("LockOnCameraPivot");
        pivotGo.transform.SetParent(transform, false);
        _lockOnPivot = pivotGo.transform;
    }

    private void LateUpdate()
    {
        if (_playerTarget == null) return;

        EnsureLockOnPivot();
        _lockOnPivot.position = _playerTarget.position;

        if (!IsLockOnMode || _lockOnTarget == null) return;

        Vector3 toTarget = _lockOnTarget.position - _playerTarget.position;
        toTarget.y = 0f;

        // 플레이어와 적이 너무 겹쳤을 때 카메라가 180도 급회전하는 현상 방지
        if (toTarget.sqrMagnitude > MinLockOnHorizontalSqrDist)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized);
            float t = 1f - Mathf.Exp(-LockOnPivotTurnSharpness * Time.deltaTime);
            _lockOnPivot.rotation = Quaternion.Slerp(_lockOnPivot.rotation, desiredRotation, t);
        }
    }

    /// <summary>
    /// 씬 부트스트래퍼가 로드 완료 시점에 호출하여 씬 가상 카메라들을 등록
    /// </summary>
    public void RegisterSceneCameras(CinemachineCamera freeLookCam, CinemachineCamera lockOnCam, CinemachineCamera cutsceneCam)
    {
        _freeLookCamera = freeLookCam;
        _lockOnCamera = lockOnCam;
        _customCutsceneCamera = cutsceneCam;

        EnsureLockOnPivot();
        ConfigureMainCameraBrain();
        ResetToFreeLook();
    }

    private void ConfigureMainCameraBrain()
    {
        if (Camera.main != null && Camera.main.TryGetComponent(out CinemachineBrain brain))
        {
            // Rigidbody.interpolation(Interpolate)과 동기화되는 SmartUpdate 강제
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            brain.DefaultBlend.Time = 0.28f;
        }
    }

    /// <summary>
    /// 씬 전환 시 가상 카메라 참조를 해제하여 유령 참조를 방지
    /// </summary>
    public void UnregisterSceneCameras()
    {
        _freeLookCamera = null;
        _lockOnCamera = null;
        _customCutsceneCamera = null;
        _playerTarget = null;
        _lockOnTarget = null;
        IsLockOnMode = false;
    }

    /// <summary>
    /// 스폰된 플레이어 트랜스폼을 가상 카메라들의 추적 대상으로 등록
    /// </summary>
    /// <param name="playerTransform">스폰된 플레이어의 트랜스폼</param>
    public void SetPlayerTarget(Transform playerTransform)
    {
        if (playerTransform == null) return;

        _playerTarget = playerTransform;
        EnsureLockOnPivot();
        _lockOnPivot.SetPositionAndRotation(_playerTarget.position, _playerTarget.rotation);

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
    }

    /// <summary>
    /// 일반 3인칭 자유 시점으로 복귀
    /// </summary>
    public void ResetToFreeLook()
    {
        IsLockOnMode = false;
        _lockOnTarget = null;

        SetCameraPriority(_freeLookCamera, ActivePriority);
        SetCameraPriority(_lockOnCamera, InactivePriority);

        if (_lockOnCamera != null)
        {
            _lockOnCamera.Target.LookAtTarget = null;
        }

        if (_customCutsceneCamera != null)
        {
            SetCameraPriority(_customCutsceneCamera, InactivePriority);
        }

        SetCameraInputEnabled(true);

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Cinematic)
        {
            GameManager.Instance.SetState(GameState.Playing);
        }
        InputSystemHandler.SetMode(InputMode.Gameplay);
    }

    /// <summary>
    /// UI 또는 컷신 중 마우스 카메라 회전 차단/해제
    /// </summary>
    public void SetCameraInputEnabled(bool isEnabled)
    {
        SetInputAxisEnabled(_freeLookCamera, isEnabled);
        SetInputAxisEnabled(_lockOnCamera, isEnabled);
    }

    private void SetInputAxisEnabled(CinemachineCamera cam, bool isEnabled)
    {
        if (cam == null) return;
        if (cam.TryGetComponent(out CinemachineInputAxisController axisController))
        {
            axisController.enabled = isEnabled;
        }
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
        _lockOnTarget = targetEnemy;
        
        if (_playerTarget != null)
        {
            EnsureLockOnPivot();
            _lockOnPivot.position = _playerTarget.position;

            Vector3 toTarget = _lockOnTarget.position - _playerTarget.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                _lockOnPivot.rotation = Quaternion.LookRotation(toTarget.normalized);
            }
        }

        if (_lockOnCamera != null)
        {
            _lockOnCamera.Target.TrackingTarget = _lockOnPivot;
            _lockOnCamera.Target.LookAtTarget = targetEnemy;
        }

        SetCameraPriority(_lockOnCamera, ActivePriority);
        SetCameraPriority(_freeLookCamera, InactivePriority);
    }
    /// <summary>
    /// 컷신 카메라를 지정된 앵커로 이동시키고 최우선 순위로 활성화하며 입력 차단
    /// </summary>
    public void PlayCutscene(Transform cutsceneAnchor, Transform lookAtTarget = null)
    {
        if (_customCutsceneCamera == null) return;

        SetCameraInputEnabled(false);

        if (cutsceneAnchor != null)
        {
            _customCutsceneCamera.transform.SetPositionAndRotation(cutsceneAnchor.position, cutsceneAnchor.rotation);
        }

        if (lookAtTarget != null)
        {
            _customCutsceneCamera.Target.LookAtTarget = lookAtTarget;
        }

        SetCameraPriority(_customCutsceneCamera, ActivePriority + 10);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Cinematic);
        }
        InputSystemHandler.SetMode(InputMode.Cutscene);
    }

    private void SetCameraPriority(CinemachineCamera cam, int priority)
    {
        if (cam != null)
        {
            cam.Priority = priority;
        }
    }

    public void SnapToFreeLook()
    {
        ConfigureMainCameraBrain();
        ResetToFreeLook();

        if (Camera.main != null && Camera.main.TryGetComponent(out CinemachineBrain brain))
        {
            // 다음 1프레임 동안 블렌드를 건너뛰고 위치 강제 스냅
            brain.ActiveBlend = null;
        }
    }
}
