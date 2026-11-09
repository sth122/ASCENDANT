/// <summary>
/// PlayerController 전용 StateMachine
/// </summary>
public class PlayerStateMachine : StateMachine<PlayerController>
{
    public void AddState(UnitState stateKey, IState state) => base.AddState((int)stateKey, state);
    public void ChangeState(UnitState newStateKey, bool forceReenter = false) => base.ChangeState((int)newStateKey, forceReenter);
    public void Initialize(UnitState stateKey) => base.Initialize((int)stateKey);
    public IState GetState(UnitState stateKey) => base.GetState((int)stateKey);

    public void SetUpPlayerState(PlayerController player)
    {
        // 1. 기본 로코모션 상태군
        AddState(UnitState.Idle, new PlayerIdleState(player, this));
        AddState(UnitState.Move, new PlayerMoveState(player, this));
        AddState(UnitState.Sprint, new PlayerSprintState(player, this));
        AddState(UnitState.Jump, new PlayerJumpState(player, this));
        AddState(UnitState.Fall, new PlayerFallState(player, this));
        AddState(UnitState.HardLanding, new PlayerHardLandingState(player, this));

        // 2. 공격 및 전투 액션 상태군
        AddState(UnitState.Attack, new PlayerAttackState(player, this));
        AddState(UnitState.Guard, new PlayerGuardState(player, this));
        AddState(UnitState.Parry, new PlayerParryState(player, this));
        AddState(UnitState.Riposte, new PlayerRiposteState(player, this));
        AddState(UnitState.UseItem, new PlayerUseItemState(player, this));
        AddState(UnitState.Roll, new PlayerRollState(player, this));

        // 3. 피격 및 무력화 반응 상태군
        AddState(UnitState.GetHit, new PlayerGetHitState(player, this));
        AddState(UnitState.GuardBreak, new PlayerGuardBreakState(player, this));
        AddState(UnitState.Knockback, new PlayerKnockbackState(player, this));
        AddState(UnitState.Die, new PlayerDieState(player, this));
    }
}