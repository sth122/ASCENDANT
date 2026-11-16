//using TMPro;
//using UnityEngine;

///// <summary>
///// 슬롯 호버 시 아이템의 상세 사양 및 요구 스탯을 동적으로 출력하는 툴팁 UI
///// </summary>
//public class ItemTooltipUI : MonoBehaviour
//{
//    [Header("Text Components")]
//    [SerializeField] private TextMeshProUGUI _nameText;
//    [SerializeField] private TextMeshProUGUI _categoryText;
//    [SerializeField] private TextMeshProUGUI _statText;
//    [SerializeField] private TextMeshProUGUI _requirementsText;
//    [SerializeField] private TextMeshProUGUI _weightText;
//    [SerializeField] private TextMeshProUGUI _descriptionText;

//    private RectTransform _rectTransform;

//    private void Awake()
//    {
//        _rectTransform = GetComponent<RectTransform>();
//        Hide();
//    }

//    public void Show(Item item, Vector2 screenPosition)
//    {
//        if (item == null || item.Data == null)
//        {
//            Hide();
//            return;
//        }

//        ItemData data = item.Data;
//        gameObject.SetActive(true);

//        if (_nameText != null) _nameText.text = data.ItemName;
//        if (_descriptionText != null) _descriptionText.text = data.Description;
//        if (_weightText != null) _weightText.text = $"무게: {data.Weight:F1}";

//        if (data is WeaponItemData weapon)
//        {
//            if (_categoryText != null) _categoryText.text = "한손검 (One-Handed Sword)";
//            if (_statText != null)
//            {
//                _statText.text = $"물리 공격력: {weapon.BaseDamage}\n" +
//                                 $"강인도 감쇄력: {weapon.BasePoiseDamage}\n" +
//                                 $"보정치: STR {weapon.StrScaling:F1} / DEX {weapon.DexScaling:F1}";
//            }
//            if (_requirementsText != null)
//            {
//                _requirementsText.text = $"필요 능력치: 근력 {weapon.RequiredStrength} / 기량 {weapon.RequiredDexterity}";
//            }
//        }
//        else if (data is ArmorItemData armor)
//        {
//            if (_categoryText != null) _categoryText.text = armor.ShieldClass == ShieldClassification.Small ? "소형 방패" : "중형 방패";
//            if (_statText != null)
//            {
//                _statText.text = $"물리 커트율: {armor.PhysicalAbsorption * 100f:F0}%\n" +
//                                 $"받아치기 안정성: {armor.Stability:F0}\n" +
//                                 $"패링 유효창: {armor.ParryWindowSeconds:F2}초";
//            }
//            if (_requirementsText != null)
//            {
//                _requirementsText.text = $"필요 능력치: 근력 {armor.RequiredStrength}";
//            }
//        }
//        else if (data is PortionItemData portion)
//        {
//            if (_categoryText != null) _categoryText.text = "소모품 (Consumable)";
//            if (_statText != null) _statText.text = $"쿨타임: {portion.Cooldown:F1}초";
//            if (_requirementsText != null) _requirementsText.text = string.Empty;
//        }

//        UpdatePosition(screenPosition);
//    }

//    public void Hide()
//    {
//        gameObject.SetActive(false);
//    }

//    private void UpdatePosition(Vector2 screenPosition)
//    {
//        if (_rectTransform == null) return;

//        Vector2 offset = new Vector2(20f, -20f);
//        _rectTransform.position = screenPosition + offset;
//    }
//}