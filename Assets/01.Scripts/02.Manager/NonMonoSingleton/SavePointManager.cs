using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Util = DebugLogger<SavePointManager>;

public class SavePointManager : NonMonoSingleton<SavePointManager>, ILoadable, ISpawnProvider
{
    public int LoadOrder => 3;

    private readonly Dictionary<BonfireId, Transform> _savePoints = new Dictionary<BonfireId, Transform>();

    public UniTask OnSceneLoadCreate(SceneId scene)
    {
        ClearSavePoints();
        return UniTask.CompletedTask;
    }

    public void Init(SceneId scene) => Util.Log($"[{scene}] SavePointManager 준비 완료");

    public void OnSceneDestroy(SceneId scene) => ClearSavePoints();

    public void RegisterSavePoint(BonfireId id, Transform spawnTransform)
    {
        if (id == BonfireId.None || spawnTransform == null) return;
        _savePoints[id] = spawnTransform;
        Util.Log($"[화톳불 스폰 포인트 등록] ID: {id}, Pos: {spawnTransform.position}");
    }

    public bool TryGetSpawnTransform(BonfireId id, out Vector3 position, out Quaternion rotation)
    {
        if (id != BonfireId.None && _savePoints.TryGetValue(id, out Transform tr) && tr != null)
        {
            position = tr.position;
            rotation = tr.rotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    public void ClearSavePoints() => _savePoints.Clear();
}