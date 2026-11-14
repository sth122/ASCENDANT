/// <summary>
/// 액션 시퀀스를 구성하는 단일 애니메이션 단계 정의 구조체
/// </summary>
[System.Serializable]
public struct ActionTimelineStep
{
    [UnityEngine.Header("모션 설정")]
    public AnimStateType StateType;
    public float SpeedMultiplier;
    public bool FaceTargetOnEnter;

    [UnityEngine.Header("루트 모션 및 물리 이동")]
    public bool UseRootMotion;
    public bool IsAirborne;

    [UnityEngine.Header("물리 돌진 (BackstepCharge 전용)")]
    public bool EnablePhysicalDash;
    public float DashSpeed;
    [UnityEngine.Range(0f, 1f)] public float DashStartNormalized;
    [UnityEngine.Range(0f, 1f)] public float DashEndNormalized;

    [UnityEngine.Header("히트박스 타격 판정")]
    public bool HasHitbox;
    public HitboxSlot TargetHitboxSlot;
    [UnityEngine.Range(0f, 1f)] public float HitboxStartNormalized;
    [UnityEngine.Range(0f, 1f)] public float HitboxEndNormalized;

    [UnityEngine.Header("특수 이벤트 (명시적 이벤트 타입)")]
    public bool HasCustomEvent;
    [UnityEngine.Range(0f, 1f)] public float EventTriggerNormalized;

    [UnityEngine.Header("VFX / SFX 타임라인 연출 큐")]
    public TimelineCueEvent[] CueEvents;

    public static ActionTimelineStep CreateSingleAttack(AnimStateType state, HitboxSlot slot, float startNorm, float endNorm)
    {
        return new ActionTimelineStep
        {
            StateType = state,
            SpeedMultiplier = 1.0f,
            FaceTargetOnEnter = true,
            UseRootMotion = false,
            IsAirborne = false,
            EnablePhysicalDash = false,
            DashSpeed = 0f,
            DashStartNormalized = 0f,
            DashEndNormalized = 0f,
            HasHitbox = true,
            TargetHitboxSlot = slot,
            HitboxStartNormalized = startNorm,
            HitboxEndNormalized = endNorm,
            HasCustomEvent = false,
            EventTriggerNormalized = 0f,
            CueEvents = null
        };
    }
}

[System.Serializable]
public struct TimelineCueEvent
{
    [UnityEngine.Range(0f, 1f)] public float TriggerNormalized;

    [UnityEngine.Header("VFX 연출")]
    public PoolType VfxType;
    public HitboxSlot SpawnSocket;
    public UnityEngine.Vector3 LocalOffset;
    public bool AttachToSocket;
    public bool DespawnOnStepExit;

    [UnityEngine.Header("SFX 연출")]
    public SoundId SfxId;
    [UnityEngine.Range(0.1f, 2f)] public float VolumeMultiplier;
}