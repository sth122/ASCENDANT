/// <summary>
/// PlayerController 전용 StateMachine
/// </summary>
public class PlayerStateMachine : UnitStateMachine<PlayerController>
{
    public void SetUpPlayerState(PlayerController player)
    {
        AddState(UnitState.Idle, new PlayerIdleState(player, this));
        AddState(UnitState.Move, new PlayerMoveState(player, this));
        AddState(UnitState.Sprint, new PlayerSprintState(player, this));
        AddState(UnitState.Jump, new PlayerJumpState(player, this));
        AddState(UnitState.Attack, new PlayerAttackState(player, this));
        AddState(UnitState.Parry, new PlayerParryState(player, this));
        AddState(UnitState.Riposte, new PlayerRiposteState(player, this));
        AddState(UnitState.Stun, new PlayerStunState(player, this));
        AddState(UnitState.Roll, new PlayerRollState(player, this));
        AddState(UnitState.GetHit, new PlayerGetHitState(player, this));
        AddState(UnitState.Die, new PlayerDieState(player, this));
    }
}
