/// <summary>
/// 액션 시퀀스를 구성하는 단일 애니메이션 단계 정의 구조체
/// </summary>
[System.Serializable]
public struct ActionTimelineStep
{
    [UnityEngine.Header("모션 및 재생 설정")]
    [UnityEngine.Tooltip("재생할 애니메이션 상태")]
    public AnimStateType StateType;

    [UnityEngine.Tooltip("모션 개별 재생 배속 (기본 1.0)")]
    public float SpeedMultiplier;

    [UnityEngine.Tooltip("단계 진입 시 타깃 방향으로 회전할지 여부")]
    public bool FaceTargetOnEnter;

    [UnityEngine.Header("히트박스 타격 판정 (해당 단계에 타격이 없으면 체크 해제)")]
    public bool HasHitbox;
    public HitboxSlot TargetHitboxSlot;
    [UnityEngine.Range(0f, 1f)] public float HitboxStartNormalized;
    [UnityEngine.Range(0f, 1f)] public float HitboxEndNormalized;

    [UnityEngine.Header("특수 이벤트 트리거 (메테오 소환, 장판 생성, 사운드 등)")]
    public bool HasCustomEvent;
    [UnityEngine.Range(0f, 1f)] public float EventTriggerNormalized;

    public static ActionTimelineStep CreateSingleAttack(AnimStateType state, HitboxSlot slot, float startNorm, float endNorm)
    {
        return new ActionTimelineStep
        {
            StateType = state,
            SpeedMultiplier = 1.0f,
            FaceTargetOnEnter = true,
            HasHitbox = true,
            TargetHitboxSlot = slot,
            HitboxStartNormalized = startNorm,
            HitboxEndNormalized = endNorm,
            HasCustomEvent = false,
            EventTriggerNormalized = 0f
        };
    }
}