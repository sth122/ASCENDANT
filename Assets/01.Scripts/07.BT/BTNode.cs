using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>
/// 단순 조건 판별 노드 (True: Success, False: Failure)
/// </summary>
public sealed class ConditionNode : INode
{
    private readonly Func<bool> _condition;

    public ConditionNode(Func<bool> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public INode.ENodeState Evaluate()
    {
        return _condition() ? INode.ENodeState.ENS_Success : INode.ENodeState.ENS_Failure;
    }
}

/// <summary>
/// UniTask 비동기 루틴을 INode 평가 틱과 연결하는 상태 유지형(Stateful) 액션 노드
/// 작업 진행 중에는 ENS_Running을 반환하며, 외부 인터럽트(피격/사망) 시 CancellationToken으로 작업을 안전 취소합니다.
/// </summary>
public class AsyncActionNode : INode
{
    private readonly Func<CancellationToken, UniTask<bool>> _taskFactory;
    private readonly Func<CancellationToken> _cancellationTokenProvider;

    private INode.ENodeState _currentState = INode.ENodeState.ENS_Failure;
    private bool _isRunning = false;

    public AsyncActionNode(Func<CancellationToken, UniTask<bool>> taskFactory, Func<CancellationToken> tokenProvider)
    {
        _taskFactory = taskFactory ?? throw new ArgumentNullException(nameof(taskFactory));
        _cancellationTokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    public INode.ENodeState Evaluate()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            _currentState = INode.ENodeState.ENS_Running;

            CancellationToken token = _cancellationTokenProvider();
            ExecuteTaskAsync(token).Forget();

            return INode.ENodeState.ENS_Running;
        }

        return _currentState;
    }

    private async UniTaskVoid ExecuteTaskAsync(CancellationToken token)
    {
        try
        {
            bool success = await _taskFactory(token);
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

public sealed class SelectorNode : INode
{
    List<INode> _children;

    public SelectorNode() { _children = new List<INode>(); }

    public void Add(INode node) { _children.Add(node); }

    public INode.ENodeState Evaluate()
    {
        if (_children == null) return INode.ENodeState.ENS_Failure;

        foreach (INode child in _children)
        {
            switch (child.Evaluate())
            {
                case INode.ENodeState.ENS_Running:
                    return INode.ENodeState.ENS_Running;
                case INode.ENodeState.ENS_Success:
                    return INode.ENodeState.ENS_Success;
            }
        }

        // 반복문이 끝났다면 해당 셀렉터의 자식노드들은 전부 FAILED 상태이므로 셀렉터는 FAILED 반환
        return INode.ENodeState.ENS_Failure;
    }
}

public sealed class SequenceNode : INode
{
    List<INode> _children;

    public SequenceNode() { _children = new List<INode>(); }

    public void Add(INode node) { _children.Add(node); }

    public INode.ENodeState Evaluate()
    {
        if (_children == null || _children.Count == 0) return INode.ENodeState.ENS_Failure;

        foreach (var child in _children)
        {
            switch (child.Evaluate())
            {
                case INode.ENodeState.ENS_Running:
                    return INode.ENodeState.ENS_Running;
                case INode.ENodeState.ENS_Success:
                    continue;
                case INode.ENodeState.ENS_Failure:
                    return INode.ENodeState.ENS_Failure;
            }
        }

        return INode.ENodeState.ENS_Success;
    }
}