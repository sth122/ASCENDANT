using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class ActionNode : INode
{
    private readonly Func<INode.ENodeState> _action;
    public ActionNode(Func<INode.ENodeState> action) => _action = action;
    public INode.ENodeState Evaluate() => _action?.Invoke() ?? INode.ENodeState.ENS_Failure;
}

public sealed class SelectorNode : INode
{
    private readonly List<INode> _children = new List<INode>();

    public void Add(INode node) => _children.Add(node);

    public INode.ENodeState Evaluate()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            switch (_children[i].Evaluate())
            {
                case INode.ENodeState.ENS_Running: return INode.ENodeState.ENS_Running;
                case INode.ENodeState.ENS_Success: return INode.ENodeState.ENS_Success;
            }
        }

        // 반복문이 끝났다면 해당 셀렉터의 자식노드들은 전부 FAILED 상태이므로 셀렉터는 FAILED 반환
        return INode.ENodeState.ENS_Failure;
    }
}

public sealed class SequenceNode : INode
{
    private readonly List<INode> _children = new List<INode>();
    public void Add(INode node) => _children.Add(node);

    public INode.ENodeState Evaluate()
    {
        if (_children.Count == 0) return INode.ENodeState.ENS_Failure;
        for (int i = 0; i < _children.Count; i++)
        {
            switch (_children[i].Evaluate())
            {
                case INode.ENodeState.ENS_Running: return INode.ENodeState.ENS_Running;
                case INode.ENodeState.ENS_Failure: return INode.ENodeState.ENS_Failure;
                case INode.ENodeState.ENS_Success: continue;
            }
        }
        return INode.ENodeState.ENS_Success;
    }
}
/// <summary>
/// 단순 조건 판별 노드 (True: Success, False: Failure)
/// </summary>
public sealed class ConditionNode : INode
{
    private readonly Func<bool> _condition;
    public ConditionNode(Func<bool> condition) => _condition = condition;
    public INode.ENodeState Evaluate() => _condition() ? INode.ENodeState.ENS_Success : INode.ENodeState.ENS_Failure;
}

/// <summary>
/// 람다 클로저 객체(DisplayClass) 힙 할당을 원천 차단하는 정적 람다 전용 조건 노드
/// </summary>
public sealed class PredicateNode<TContext> : INode
{
    private readonly TContext _context;
    private readonly Func<TContext, bool> _predicate;

    public PredicateNode(TContext context, Func<TContext, bool> predicate)
    {
        _context = context;
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public INode.ENodeState Evaluate() => _predicate(_context) ? INode.ENodeState.ENS_Success : INode.ENodeState.ENS_Failure;
}

/// <summary>
/// UniTask 비동기 루틴을 Behavior Tree 동기 틱 루프와 연결하는 상태 유지형 액션 노드
/// </summary>
public class AsyncActionNode : INode
{
    private readonly Func<CancellationToken, UniTask<bool>> _taskFactory;
    private readonly Func<CancellationToken> _tokenProvider;
    private INode.ENodeState _currentState = INode.ENodeState.ENS_Failure;
    private bool _isRunning = false;

    public AsyncActionNode(Func<CancellationToken, UniTask<bool>> taskFactory, Func<CancellationToken> tokenProvider)
    {
        _taskFactory = taskFactory ?? throw new ArgumentNullException(nameof(taskFactory));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    public INode.ENodeState Evaluate()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            _currentState = INode.ENodeState.ENS_Running;
            ExecuteAsync(_tokenProvider()).Forget();
            return INode.ENodeState.ENS_Running;
        }
        return _currentState;
    }

    private async UniTaskVoid ExecuteAsync(CancellationToken ct)
    {
        try
        {
            bool success = await _taskFactory(ct);
            _currentState = success ? INode.ENodeState.ENS_Success : INode.ENodeState.ENS_Failure;
        }
        catch (OperationCanceledException)
        {
            _currentState = INode.ENodeState.ENS_Failure;
        }
        catch (Exception ex)
        {
            DebugLogger.LogErrorWithTag("AsyncActionNode", $"비동기 액션 예외 발생: {ex}");
            _currentState = INode.ENodeState.ENS_Failure;
        }
        finally
        {
            _isRunning = false;
        }
    }

    public void Reset()
    {
        _isRunning = false;
        _currentState = INode.ENodeState.ENS_Failure;
    }
}
