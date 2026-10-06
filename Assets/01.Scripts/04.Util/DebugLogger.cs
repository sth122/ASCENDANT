/* 담담자 - 송태훈
유니티 에디터에서만 콘솔 Debug를 출력하는 static class 
사용 방법 : using Debug = Debugger<Generic Class>;
*/
using System.Diagnostics;

public static class DebugLogger<T>
{
    #region 제네릭 클래스 Dubug
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        UnityEngine.Debug.Log($"[{typeof(T).Name}] {message}");
#endif
    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        UnityEngine.Debug.LogWarning($"[{typeof(T).Name}] {message}");
#endif
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        UnityEngine.Debug.LogError($"[{typeof(T).Name}] {message}");
#endif
    }
    #endregion
}
public static class DebugLogger
{
    #region 일반 클래스 Debug
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWithTag(string tag, object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[{tag}] {message}");
#endif
    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarningWithTag(string tag, object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogWarning($"[{tag}] {message}");
#endif
    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogErrorWithTag(string tag, object message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.LogError($"[{tag}] {message}");
#endif
    }
    #endregion
}