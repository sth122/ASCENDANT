// MonoBehaviour를 상속받지 않는 싱글톤 -> 메모리 절약 가능

/// <summary>
///  MonoBehaviour를 상속받지 않는 싱글톤
///  각 공통된 초기화가 필요할 시 Init에 작성. 그 외엔 base.Init()
/// </summary>
/// <typeparam name="T"></typeparam>
public class NonMonoSingleton<T> where T : class, IInitable, new()
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new T();
            }

            return _instance;
        }
    }

    public virtual void Init() { }
}