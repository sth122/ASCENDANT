public interface IInitable
{
    public void Init();
}

// FSM  상태 인터페이스
public interface IState
{
    public void Enter();
    public void Exit();
    public void Update();
    public void FixedUpdate();
}