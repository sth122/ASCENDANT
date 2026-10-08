/// <summary>
/// 버퍼 큐에 적재될 커맨드 데이터 구조체
/// </summary>
public readonly struct BufferedCommand
{
    public readonly InputCommandType CommnadType;
    public readonly float Timestamp;

    public BufferedCommand(InputCommandType type, float timestamp)
    {
        CommnadType = type;
        Timestamp = timestamp;
    }

    public bool IsExpired(float bufferDuration) => (UnityEngine.Time.time - Timestamp) > bufferDuration;
}
