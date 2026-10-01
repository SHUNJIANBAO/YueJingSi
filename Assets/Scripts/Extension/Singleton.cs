
public class Singleton<T> where T : Singleton<T>, new()
{
    private static T _instance;
    private static readonly object _lockObj = new object();
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObj)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                        _instance.OnInit();
                    }
                }
            }
            return _instance;
        }
    }
    public static T GetInstance()
    {
        return _instance;
    }
    public virtual void OnInit() { }
}