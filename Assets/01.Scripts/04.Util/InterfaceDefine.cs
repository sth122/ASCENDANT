/// <summary>
/// 풀링 객체 인터페이스
/// </summary>
public interface IPoolable
{
    void OnSpawn();
    void OnDespawn();
}

public interface IInitable
{
    public void Init();
}

/// <summary>
/// FSM  상태 인터페이스
/// </summary>
public interface IState
{
    public void Enter();
    public void Exit();
    public void Update();
    public void FixedUpdate();
}

/// <summary>
/// 공격 수신 인터페이스
/// </summary>
public interface IDamageable
{
    void ApplyDamage(in DamageContext context);
}

/// <summary>
/// 그로기 및 앞잡(Riposte / 처형) 피격 대상 인터페이스
/// </summary>
public interface IRiposteable
{
    bool CanBeRiposted { get; }
    bool IsDead { get; }
    UnityEngine.Transform RiposteSocket { get; }
    void OnRiposted(UnityEngine.Transform attacker);
}

/// <summary>
/// 강인도 고갈(Poise Break) 가능 개체 인터페이스
/// </summary>
public interface IPoiseBreakable
{
    bool IsPoiseBroken { get; }
    void OnPoiseBroken();
}

/// <summary>
/// 씬 내 모든 에너미의 수명 주기 및 화톳불 리셋을 전담 제어하는 매니저 인터페이스
/// </summary>
public interface IEnemyManager
{
    void RegisterEnemy(EnemyBTController enemy);
    void UnregisterEnemy(EnemyBTController enemy);
    void RegisterDefeatedPermanentEnemy(string permanentId);
    bool IsPermanentEnemyDefeated(string permanentId);
    void ResetAllEnemies();
}

#region UI 인터페이스
public interface IUIModel
{
    void Reset();
}
public interface IUIPresenter
{
    void Bind(UIBase view, IUIModel model);
    void Unbind();
}
#endregion

#region BootStrap
/// <summary>
/// 씬 전환 시 로딩 파이프라인에서 순차적으로 초기화 및 파괴를 제어하는 규약
/// </summary>
public interface ILoadable
{
    int LoadOrder { get; }
    Cysharp.Threading.Tasks.UniTask OnSceneLoadCreate(SceneId scene);
    void Init(SceneId scene);
    void OnSceneDestroy(SceneId scene);
}

/// <summary>
/// 각 씬의 하이어라키 루트에서 인게임 액터 및 로컬 서비스를 셋업하는 규약
/// </summary>
public interface ISceneBootstrap
{
    Cysharp.Threading.Tasks.UniTask OnSceneReadyAsync();
}

/// <summary>
/// 씬에 스폰된 플레이어 컨트롤러를 몬스터 AI나 카메라가 안전하게 획득하도록 중계하는 로컬 서비스 인터페이스
/// </summary>
public interface IPlayerProvider
{
    PlayerController Player { get; }
    UnityEngine.Transform PlayerTransform { get; }
    bool IsPlayerSpawned { get; }
}
#endregion

#region BT Interface
public interface INode
{
    public enum ENodeState
    {
        ENS_Running, ENS_Success, ENS_Failure
    }

    public INode.ENodeState Evaluate();
}
#endregion

#region Skill Interface
public interface ISkillable
{
    bool CanExecuteSkill(SkillId skillId);
    Cysharp.Threading.Tasks.UniTask<bool> ExecuteSkillAsync(SkillId skillId, System.Threading.CancellationToken ct);
    void CancelSkill();
}
#endregion

#region Interact Interface
/// <summary>
/// 인게임 상호작용 규약
/// </summary>
public interface IInteractable
{
    void OnInteract(PlayerController player);
}
#endregion