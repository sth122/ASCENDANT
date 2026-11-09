using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class ActionNode : INode
{
    private readonly Func<INode.ENodeState> _action;
    public ActionNode(Func<INode.ENodeState> action) => _action = action;
    public INode.ENodeState Evaluate() => _action?.Invoke() ?? INode.ENodeState.ENS_Failure;
    public void Reset() { }
}

public sealed class SelectorNode : INode
{
    private readonly List<INode> _children = new List<INode>();
    private int _runningChildIndex = -1;

    public void Add(INode node) => _children.Add(node);

    public INode.ENodeState Evaluate()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            var state = _children[i].Evaluate();
            switch (state)
            {
                case INode.ENodeState.ENS_Running:
                    if (_runningChildIndex != -1 && _runningChildIndex != i)
                    {
                        _children[_runningChildIndex].Reset();
                    }
                    _runningChildIndex = i;
                    return INode.ENodeState.ENS_Running;

                case INode.ENodeState.ENS_Success:
                    if (_runningChildIndex != -1 && _runningChildIndex != i)
                    {
                        _children[_runningChildIndex].Reset();
                    }
                    _runningChildIndex = -1;
                    return INode.ENodeState.ENS_Success;
            }
        }

        Reset();
        // 반복문이 끝났다면 해당 셀렉터의 자식노드들은 전부 FAILED 상태이므로 셀렉터는 FAILED 반환
        return INode.ENodeState.ENS_Failure;
    }

    public void Reset()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Reset();
        }
        _runningChildIndex = -1;
    }
}

public sealed class SequenceNode : INode
{
    private readonly List<INode> _children = new List<INode>();
    private int _currentRunningIndex = 0;

    public void Add(INode node) => _children.Add(node);

    public INode.ENodeState Evaluate()
    {
        if (_children.Count == 0) return INode.ENodeState.ENS_Failure;
        for (int i = _currentRunningIndex; i < _children.Count; i++)
        {
            var state = _children[i].Evaluate();
            switch (state)
            {
                case INode.ENodeState.ENS_Running:
                    _currentRunningIndex = i;
                    return INode.ENodeState.ENS_Running;

                case INode.ENodeState.ENS_Failure:
                    Reset();
                    return INode.ENodeState.ENS_Failure;

                case INode.ENodeState.ENS_Success:
                    continue;
            }
        }

        Reset();
        return INode.ENodeState.ENS_Success;
    }

    public void Reset()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Reset();
        }
        _currentRunningIndex = 0;
    }
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
    public void Reset() { }
}

/// <summary>
/// UniTask 비동기 루틴을 Behavior Tree 동기 틱 루프와 연결하는 상태 유지형 액션 노드
/// </summary>
public class AsyncActionNode : INode
{
    private readonly Func<CancellationToken, UniTask<bool>> _taskFactory;
    private readonly Func<CancellationToken> _parentTokenProvider;
    private CancellationTokenSource _nodeCts;
    private INode.ENodeState _currentState = INode.ENodeState.ENS_Failure;
    private bool _isRunning = false;
    private bool _isCompleted = false;

    public AsyncActionNode(Func<CancellationToken, UniTask<bool>> taskFactory, Func<CancellationToken> parentTokenProvider = null)
    {
        _taskFactory = taskFactory ?? throw new ArgumentNullException(nameof(taskFactory));
        _parentTokenProvider = parentTokenProvider;
    }

    public INode.ENodeState Evaluate()
    {
        if (_isCompleted)
        {
            INode.ENodeState finalResult = _currentState;
            Reset();
            return finalResult;
        }

        if (!_isRunning)
        {
            _isRunning = true;
            _isCompleted = false;
            _currentState = INode.ENodeState.ENS_Running;

            AsyncUtil.CancelAndDispose(ref _nodeCts);
            if (_parentTokenProvider != null)
            {
                _nodeCts = CancellationTokenSource.CreateLinkedTokenSource(_parentTokenProvider());
            }
            else
            {
                _nodeCts = new CancellationTokenSource();
            }

            ExecuteAsync(_nodeCts.Token).Forget();
            return INode.ENodeState.ENS_Running;
        }

        return INode.ENodeState.ENS_Running;
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
            _isCompleted = true;
        }
    }

    public void Reset()
    {
        AsyncUtil.CancelAndDispose(ref _nodeCts);
        _isRunning = false;
        _isCompleted = false;
        _currentState = INode.ENodeState.ENS_Failure;
    }
}
