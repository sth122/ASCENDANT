using System.Collections.Generic;

[UnityEngine.CreateAssetMenu(fileName = "ScenePoolConfigSO", menuName = "System/ScenePoolConfigSO")]
public class ScenePoolConfigSO : UnityEngine.ScriptableObject
{
    [System.Serializable]
    public struct PoolItem
    {
        public PoolType Type;
        public int PreloadCount;
        public string AddressableKeyOverride;
        public string GetAddressableKey()
        {
            return string.IsNullOrEmpty(AddressableKeyOverride) ? Type.ToString() : AddressableKeyOverride;
        }
    }

    [System.Serializable]
    public struct ScenePoolEntry
    {
        public SceneId TargetScene;
        public List<PoolItem> Items;
    }
    [UnityEngine.Header("인게임 전역 공용 풀 (TitleScene 제외, 씬 전환 시 파괴되지 않고 유지)")]
    [UnityEngine.SerializeField] private List<PoolItem> _globalGameplayItems = new List<PoolItem>();

    [UnityEngine.Header("씬 전용 풀 (해당 씬 이탈 시 메모리 완전 반환)")]
    [UnityEngine.SerializeField] private List<ScenePoolEntry> _entries = new List<ScenePoolEntry>();

    public IReadOnlyList<PoolItem> GlobalGameplayItems => _globalGameplayItems;

    public bool TryGetPoolItemsForScene(SceneId scene, out List<PoolItem> items)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].TargetScene == scene)
            {
                items = _entries[i].Items;
                return true;
            }
        }
        items = null;
        return false;
    }
}