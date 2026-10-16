using System;
using System.Collections.Generic;
using Util = DebugLogger<StateMachine<System.Type>>;

// 모든 FSM 범용 제네릭 상태 머신
#region BaseStateMachine
public class StateMachine<T> where T : class
{
    protected IState currentState;
    public IState CurrentState => currentState;
    
    protected int currentStateKey = -1;
    public int  CurrentStateKey => currentStateKey;

    // (int)Enum 키를 저장하는 딕셔너리
    protected readonly Dictionary<int, IState> stateDictionary = new Dictionary<int, IState>();

    // 상태 등록
    public void AddState(int key, IState state)
    {
        if (!stateDictionary.ContainsKey(key))
        {
            stateDictionary.Add(key, state);
        }
        else
        {
            Util.LogError($"[StateMachine] 이미 등록된 상태 키 : {key}");
        }
    }
    /// <summary>
    /// 키 (int)를 통한 상태 전이 수행
    /// forceReenter가 true면 동일 상태 ( ex: Roll -> Roll)라도 Exit 후 Enter를 재실행 가능
    /// </summary>
    /// <param name="newKey">다음으로 전이할 상태</param>
    /// <param name="forceReenter">동일 상태에 대한 전이 재실행 여부</param>
    public void ChangeState(int newKey, bool forceReenter = false)
    {
        if (!forceReenter && CurrentState != null && currentStateKey == newKey)
            return;

        if(stateDictionary.TryGetValue(newKey, out IState newState))
        {
            currentState?.Exit();
            currentStateKey = newKey;
            currentState = newState;
            currentState?.Enter();
        }
        else
        {
            Util.LogError($"[StateMachine] 등록되지 않은 상태 키 : {newKey}");
        }
    }

    public void Initialize(int key)
    {
        if (currentState == null && stateDictionary.TryGetValue(key, out IState state))
        {
            currentStateKey = key;
            currentState = state;
            currentState?.Enter();
        }
        else
        {
            Util.LogError($"[StateMachine] 초기화 실패. 유요하지 않은 상태 키 : {key}");
        }
    }

    public void Update() => currentState?.Update();
    public void FixedUpdate() => currentState?.FixedUpdate();
}
#endregion

// Unit 전용 제네릭 StateMachine, UnitController<T>를 상속받는 타입만 사용 가능
#region UnitStateMachine
public abstract class UnitStateMachine<T> : StateMachine<T> where T : UnitController<T>
{
    public void AddState(UnitState stateKey, IState state)
    {
        base.AddState((int)stateKey, state);
    }
    public void ChangeState(UnitState newStateKey, bool _forceReenter = false)
    {
        base.ChangeState((int)newStateKey, _forceReenter);
    }
    public void Initialize(UnitState stateKey)
    {
        base.Initialize((int)stateKey);
    }
}
#endregion