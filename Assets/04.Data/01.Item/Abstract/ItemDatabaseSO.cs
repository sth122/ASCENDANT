using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabaseSO", menuName = "Inventory/ItemDatabaseSO")]
public class ItemDatabaseSO : ScriptableObject, IInitable
{
    [SerializeField] private List<ItemData> _items = new List<ItemData>();

    private readonly Dictionary<int, ItemData> _itemMap = new Dictionary<int, ItemData>();

    public IReadOnlyList<ItemData> Items => _items;

    public void Init()
    {
        _itemMap.Clear();
        if (_items == null) return;

        for (int i = 0; i < _items.Count; i++)
        {
            ItemData item = _items[i];
            if (item == null) continue;

            if (_itemMap.ContainsKey(item.Id))
            {
                DebugLogger.LogWarningWithTag("ItemDatabaseSO", $"중복된 아이템 ID 감지: {item.Id} ({item.ItemName})");
                continue;
            }

            _itemMap[item.Id] = item;

            if (DataManager.Instance != null)
            {
                DataManager.Instance.RegisterItemData(item);
            }
        }
    }

    public bool TryGetItem(int id, out ItemData itemData)
    {
        if (_itemMap.Count == 0 && _items.Count > 0)
        {
            Init();
        }
        return _itemMap.TryGetValue(id, out itemData);
    }

#if UNITY_EDITOR
    [ContextMenu("프로젝트 내 모든 ItemData 자동 수집")]
    private void CollectAllItemDataInProject()
    {
        _items.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null && !_items.Contains(item))
            {
                _items.Add(item);
            }
        }

        _items.Sort((a, b) => a.Id.CompareTo(b.Id));
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ItemDatabaseSO] 총 {_items.Count}개의 ItemData 수집 완료");
    }
#endif
}