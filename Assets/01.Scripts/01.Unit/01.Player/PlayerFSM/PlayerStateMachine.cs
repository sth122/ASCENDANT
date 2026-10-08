using System.Collections.Generic;

// PlayerController 전용 StateMachine
public class PlayerStateMachine : UnitStateMachine<PlayerController>
{
    public void SetUpPlayerState(PlayerController Player)
    {
        AddState(UnitState.Idle, new PlayerIdleState(Player, this));
        AddState(UnitState.Move, new PlayerMoveState(Player, this));
        AddState(UnitState.Sprint, new PlayerSprintState(Player, this));
        AddState(UnitState.Jump, new PlayerJumpState(Player, this));
        AddState(UnitState.Roll, new PlayerRollState(Player, this));
        //AddState(UnitState.Attack, new PlayerAttackState(Player, this));
        AddState(UnitState.Parry, new PlayerParryState(Player, this));
        //AddState(UnitState.Stun, new PlayerStunState(Player, this));
    }
}
