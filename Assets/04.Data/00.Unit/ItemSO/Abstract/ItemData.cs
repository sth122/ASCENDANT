/// <summary>
/// 모든 아이템의 데이터 원형 ScriptableObject 베이스 클래스
/// </summary>
public abstract class ItemData : UnityEngine.ScriptableObject
{
    [UnityEngine.Header("기본 정보 (Common Info)")]
    [UnityEngine.SerializeField] private int _id;
    [UnityEngine.SerializeField] private string _itemName;
    [UnityEngine.SerializeField] private UnityEngine.Sprite _icon;
    [UnityEngine.TextArea(2, 4)]
    [UnityEngine.SerializeField] private string _description;
    [UnityEngine.SerializeField] private float _weight = 0f;
    [UnityEngine.SerializeField] private ItemCategory _category = ItemCategory.Consumable;
    [UnityEngine.SerializeField] private int _maxStorageCount = 99;


    public int Id => _id;
    public string ItemName => _itemName;
    public UnityEngine.Sprite Icon => _icon;
    public string Description => _description;
    public float Weight => _weight;
    public ItemCategory Category => _category;
    public int MaxStorageCount => _maxStorageCount;
    public abstract ItemType Type { get; }

    /// <summary>
    /// 인벤토리 슬롯에 담길 런타임 인스턴스를 생성하는 팩토리 메서드
    /// </summary>
    public abstract Item CreateItem();
    public virtual string GetEffectSummaryText() => string.Empty;
    public virtual StatScalingGrade StrScalingGrade => StatScalingGrade.None;
    public virtual StatScalingGrade DexScalingGrade => StatScalingGrade.None;
}