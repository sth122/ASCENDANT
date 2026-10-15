/// <summary>
/// 공격 선후딜레이 및 점프/회피 중 선입력된 커맨드를 큐 방식으로 적재하고
/// 지정된 버퍼 유효 시간(Window) 내의 입력만 소비하도록 통제하는 순수 C# 버퍼 관리자
/// </summary>
public class InputBufferQueue
{
    private readonly System.Collections.Generic.Queue<BufferedCommand> _commandQueue;
    private readonly float _bufferDuration;

    public InputBufferQueue(float bufferDuration = 0.25f)
    {
        this._commandQueue = new System.Collections.Generic.Queue<BufferedCommand>(8);
        this._bufferDuration = bufferDuration; 
    }

    /// <summary>
    /// 새로운 액션 커맨드를 타임 스탬프와 함께 큐에 등록
    /// </summary>
    public void EnqueueCommand(InputCommandType type)
    {
        CleanExpiredHead();
        _commandQueue.Enqueue(new BufferedCommand(type, UnityEngine.Time.time));
    }

    /// <summary>
    /// 특정 커맨드가 유효 시간 내에 버퍼링되어 있는지 확인하고 소비
    /// 만료된 이전 커맨드들은 자동으로 폐기
    /// </summary>
    public bool TryConsumeCommand(InputCommandType targetCommand)
    {
        CleanExpiredHead();
        int initialCount = _commandQueue.Count;
        if (initialCount == 0) return false;

        bool found = false;

        for (int i = 0; i < initialCount; i++)
        {
            BufferedCommand cmd = _commandQueue.Dequeue();

            if(!found && cmd.CommandType == targetCommand && !cmd.IsExpired(_bufferDuration))
            {
                // 대상 커맨드 소비
                found = true;
            }else if(!cmd.IsExpired(_bufferDuration))
            {
                // 만료되지 않은 다른 커맨드는 다시 Enqueue
                _commandQueue.Enqueue(cmd);
            }
        }
        return found;
    }

    /// <summary>
    /// 현재 유효한 버퍼링 커맨드 중 선입력된 커맨드를 산출
    /// </summary>
    public bool TryConsumeAnyCommand(out InputCommandType consumedCommand)
    {
        CleanExpiredHead();

        while (_commandQueue.Count > 0)
        {
            BufferedCommand cmd = _commandQueue.Dequeue();
            if (!cmd.IsExpired(_bufferDuration))
            {
                consumedCommand = cmd.CommandType;
                return true;
            }
        }

        consumedCommand = InputCommandType.None;
        return false;
    }

    /// <summary>
    /// 큐 내부의 유효 커맨드 중 소울라이크 우선순위(Roll > Parry > Attack > Jump)가 가장 높은 커맨드를 선별 소비
    /// 큐를 회전시켜 선택된 커맨드만 제거하고 나머지는 보존
    /// </summary>
    public bool TryConsumePriorityCommand(out InputCommandType consumedCommand)
    {
        CleanExpiredHead();
        int count = _commandQueue.Count;

        if (count == 0)
        {
            consumedCommand = InputCommandType.None;
            return false;
        }

        // 1. 최고 우선순위 커맨드 탐색
        InputCommandType highestPriorityType = InputCommandType.None;
        int highestPriorityValue = -1;

        foreach (BufferedCommand cmd in _commandQueue)
        {
            if (!cmd.IsExpired(_bufferDuration))
            {
                int priority = GetCommandPriority(cmd.CommandType);
                if (priority > highestPriorityValue)
                {
                    highestPriorityValue = priority;
                    highestPriorityType = cmd.CommandType;
                }
            }
        }

        // 2. 최고 우선순위 커맨드가 없으면 만료 처리
        if (highestPriorityType == InputCommandType.None)
        {
            _commandQueue.Clear();
            consumedCommand = InputCommandType.None;
            return false;
        }

        // 3. 큐를 회전하며 해당 커맨드 1회만 소비
        consumedCommand = highestPriorityType;
        bool consumed = false;

        for (int i = 0; i < count; i++)
        {
            BufferedCommand cmd = _commandQueue.Dequeue();

            if (!consumed && cmd.CommandType == highestPriorityType && !cmd.IsExpired(_bufferDuration))
            {
                consumed = true;
            }
            else if (!cmd.IsExpired(_bufferDuration))
            {
                _commandQueue.Enqueue(cmd);
            }
        }

        return consumed;
    }

    /// <summary>
    /// 특정 종류의 커맨드만 버퍼에서 제거
    /// ex) 점프 상태 진입 시 남아있는 Jump 커맨드 전량 폐기
    /// </summary>
    /// <param name="type"></param>
    public void ClearCommand(InputCommandType type)
    {
        int count = _commandQueue.Count;
        for (int i = 0; i < count; i++)
        {
            BufferedCommand cmd = _commandQueue.Dequeue();
            if (cmd.CommandType != type && !cmd.IsExpired(_bufferDuration))
            {
                _commandQueue.Enqueue(cmd);
            }
        }
    }
    
    public void ClearAll() => _commandQueue.Clear();
    private void CleanExpiredHead()
    {
        while(_commandQueue.Count > 0 && _commandQueue.Peek().IsExpired(_bufferDuration))
        {
            _commandQueue.Dequeue();
        }
    }

    /// <summary>
    /// 커맨드 우선순위 가중치: Roll(최우선) -> Parry -> Attack -> Jump
    /// </summary>
    private int GetCommandPriority(InputCommandType command)
    {
        switch (command)
        {
            case InputCommandType.Roll: return 4;
            case InputCommandType.Parry: return 3;
            case InputCommandType.Attack: return 2;
            case InputCommandType.Jump: return 1;
            default: return 0;
        }
    }
}
