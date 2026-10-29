/// <summary>
/// PlayerController 전용 StateMachine
/// </summary>
public class PlayerStateMachine : StateMachine<PlayerController>
{
    public void AddState(UnitState stateKey, IState state) => base.AddState((int)stateKey, state);
    public void ChangeState(UnitState newStateKey, bool forceReenter = false) => base.ChangeState((int)newStateKey, forceReenter);
    public void Initialize(UnitState stateKey) => base.Initialize((int)stateKey);

    public void SetUpPlayerState(PlayerController player)
    {
        AddState(UnitState.Idle, new PlayerIdleState(player, this));
        AddState(UnitState.Move, new PlayerMoveState(player, this));
        AddState(UnitState.Sprint, new PlayerSprintState(player, this));
        AddState(UnitState.Jump, new PlayerJumpState(player, this));
        AddState(UnitState.Attack, new PlayerAttackState(player, this));
        AddState(UnitState.Parry, new PlayerParryState(player, this));
        AddState(UnitState.Riposte, new PlayerRiposteState(player, this));
        AddState(UnitState.Stun, new PlayerKnockdownState(player, this)); // 보스 강공격 넉백 다운 상태 바인딩
        AddState(UnitState.Roll, new PlayerRollState(player, this));
        AddState(UnitState.GetHit, new PlayerGetHitState(player, this));
        AddState(UnitState.Die, new PlayerDieState(player, this));
    }
}