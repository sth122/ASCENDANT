using System.Collections.Generic;

[UnityEngine.CreateAssetMenu(fileName = "ScenePoolConfigSO", menuName = "System/ScenePoolConfigSO")]
public class ScenePoolConfigSO : UnityEngine.ScriptableObject
{
    [System.Serializable]
    public struct PoolItem
    {
        public PoolType Type;
        public int PreloadCount;
    }

    [System.Serializable]
    public struct ScenePoolEntry
    {
        public SceneId TargetScene;
        public List<PoolItem> Items;
    }

    [UnityEngine.SerializeField] private List<ScenePoolEntry> _entries = new List<ScenePoolEntry>();

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