/// <summary>
/// 유효 시간(Window) 기반으로 액션 선입력을 저장하고 소비하는 순수 C# 버퍼 관리자
/// </summary>
public class InputBufferQueue
{
    private readonly System.Collections.Generic.Queue<BufferedCommand> _commnadQueue = new System.Collections.Generic.Queue<BufferedCommand>();
    private readonly float _bufferDuration;

    public InputBufferQueue(float bufferDuration = 0.25f)
    {
        this._bufferDuration = bufferDuration; 
    }

    public void EnqueueCommnad(InputCommandType type)
    {
        _commnadQueue.Enqueue(new BufferedCommand(type, UnityEngine.Time.time));
    }

    public bool TryConsumeCommnad(InputCommandType targetCommand)
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

    public void ClearCommnad(InputCommandType type)
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
