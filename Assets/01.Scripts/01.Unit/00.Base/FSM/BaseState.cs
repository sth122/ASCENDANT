/*
 모든 FSM 상태 클래스의 기본 클래스
 */
public class BaseState : IState
{
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void FixedUpdate() { }
    public virtual void Update() { }

}
