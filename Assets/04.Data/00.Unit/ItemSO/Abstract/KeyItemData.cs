using UnityEngine;

[CreateAssetMenu(fileName = "KeyItem_", menuName = "GameData/Item/KeyItemData")]
public class KeyItemData : ItemData
{
    [Header("Key Gimmick Settings")]
    [Tooltip("이 열쇠로 열 수 있는 문 또는 기믹의 고유 ID (예: GameScene_BossGateDoor)")]
    [SerializeField] private string _targetDoorId = "GameScene_BossGateDoor";

    [Tooltip("상호작용 시 열쇠를 소모할지 여부 (false면 영구 소지)")]
    [SerializeField] private bool _consumeOnUse = true;

    public string TargetDoorId => _targetDoorId;
    public bool ConsumeOnUse => _consumeOnUse;

    public override Item CreateItem()
    {
        return new KeyItem(this);
    }

    public override string GetEffectSummaryText()
    {
        return $"[기믹 아이템] 특정 장소나 잠긴 문을 여는 데 사용됩니다.";
    }
}