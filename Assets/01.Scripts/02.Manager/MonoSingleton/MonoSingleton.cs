// Singleon 패턴. 어디서든 단 1개의 인스터스를 생성하고 접근할 수 있도록 하는 클래스

using UnityEngine;
public class MonoSingleton<T> : MonoBehaviour, IInitable where T : MonoBehaviour
{
    [SerializeField] protected bool isDDOL = false;
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<T>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    _instance = obj.AddComponent<T>();

                    var singleton = _instance as MonoSingleton<T>;

                    if (singleton != null && singleton.isDDOL)
                    {
                        DontDestroyOnLoad(obj);
                    }
                }
            }
            else
            {
                var singleton = _instance as MonoSingleton<T>;
                if (singleton != null && singleton.isDDOL)
                {
                    DontDestroyOnLoad(singleton);
                }
            }

            return _instance;
        }
    }


    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            if (isDDOL)
            {
                DontDestroyOnLoad(this.gameObject);
            }
            Init();
        }
        else if (_instance != this)
        {
            Destroy(this.gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public virtual void Init() { }
}