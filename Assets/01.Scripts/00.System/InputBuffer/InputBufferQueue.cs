/// <summary>
/// 공격 선후딜레이 및 점프/회피 중 선입력된 커맨드를 큐 방식으로 적재하고
/// 지정된 버퍼 유효 시간(Window) 내의 입력만 소비하도록 통제하는 순수 C# 버퍼 관리자
/// </summary>
public class InputBufferQueue
{
    private readonly System.Collections.Generic.Queue<BufferedCommand> _commnadQueue = new System.Collections.Generic.Queue<BufferedCommand>();
    private readonly float _bufferDuration;

    public InputBufferQueue(float bufferDuration = 0.25f)
    {
        this._bufferDuration = bufferDuration; 
    }

    /// <summary>
    /// 새로운 액션 커맨드를 타임 스탬프와 함께 큐에 등록
    /// </summary>
    public void EnqueueCommand(InputCommandType type)
    {
        _commnadQueue.Enqueue(new BufferedCommand(type, UnityEngine.Time.time));
    }

    /// <summary>
    /// 특정 커맨드가 유효 시간 내에 버퍼링되어 있는지 확인하고 소비
    /// 만료된 이전 커맨드들은 자동으로 폐기
    /// </summary>
    public bool TryConsumeCommand(InputCommandType targetCommand)
    {
        CleanExpiredCommands();
        if (_commnadQueue.Count == 0) return false;

        if(_commnadQueue.Peek().CommnadType == targetCommand)
        {
            _commnadQueue.Dequeue();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 현재 유효한 버퍼링 커맨드 중 선입력된 커맨드를 산출
    /// </summary>
    public bool TryConsumeAnyCommand(out InputCommandType consumedCommand)
    {
        CleanExpiredCommands();
        if(_commnadQueue.Count > 0)
        {
            consumedCommand = _commnadQueue.Dequeue().CommnadType;
            return true;
        }

        consumedCommand = InputCommandType.None;
        return false;
    }

    /// <summary>
    /// 특정 종류의 커맨드만 버퍼에서 제거
    /// ex) 점프 상태 진입 시 남아있는 Jump 커맨드 전량 폐기
    /// </summary>
    /// <param name="type"></param>
    public void ClearCommand(InputCommandType type)
    {
        int count = _commnadQueue.Count;
        for (int i = 0; i < count; i++)
        {
            BufferedCommand cmd = _commnadQueue.Dequeue();
            if (cmd.CommnadType != type && !cmd.IsExpired(_bufferDuration))
            {
                _commnadQueue.Enqueue(cmd);
            }
        }
    }
    
    public void ClearAll() => _commnadQueue.Clear();
    private void CleanExpiredCommands()
    {
        while(_commnadQueue.Count > 0 && _commnadQueue.Peek().IsExpired(_bufferDuration))
        {
            _commnadQueue.Dequeue();
        }
    }
}
