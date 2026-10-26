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
/// 공격 및 타격 시 전달되는 순수 데이터 컨텍스트 구조체
/// </summary>
public readonly struct DamageContext
{
    public readonly UnityEngine.GameObject Attacker;
    public readonly float Damage;
    public readonly float PoiseDamage;
    public readonly UnityEngine.Vector3 HitPoint;
    public readonly UnityEngine.Vector3 HitNormal;
    public readonly bool IsParryable;

    public DamageContext(UnityEngine.GameObject attacker, float damage, float poiseDamage, UnityEngine.Vector3 hitPoint, UnityEngine.Vector3 hitNormal, bool isParryable = true)
    {
        Attacker = attacker;
        Damage = damage;
        PoiseDamage = poiseDamage;
        HitPoint = hitPoint;
        HitNormal = hitNormal;
        IsParryable = isParryable;
    }
}

/// <summary>
/// 공격 수신 인터페이스 (플레이어, 몬스터, 보스 공통)
/// </summary>
public interface IDamageable
{
    void ApplyDamage(in DamageContext context);
}

/// <summary>
/// Enemy 공격 실행 전략 인터페이스
/// </summary>
public interface IEnemyAttackStrategy
{
    bool CanExecute(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data);
    void Execute(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data);
    void Update(in EnemyAttackContext context, UnityEngine.Transform target, in EnemyAttackData data, float normalizedTime);
    void Stop(in EnemyAttackContext context);
}

/// <summary>
/// 씬 내 모든 에너미의 수명 주기 및 화톳불 리셋을 전담 제어하는 매니저 인터페이스
/// </summary>
public interface IEnemyManager
{
    void RegisterEnemy(EnemyController enemy);
    void UnregisterEnemy(EnemyController enemy);
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