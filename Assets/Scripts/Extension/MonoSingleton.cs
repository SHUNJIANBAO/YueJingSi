using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // 实例化锁，防止多线程同时创建
    private static readonly object _syncRoot = new object();
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_syncRoot)
                {
                    _instance = (T)FindObjectOfType(typeof(T));
                    if (_instance == null)
                    {
                        _instance = new GameObject(typeof(T).Name).AddComponent<T>();
                    }
                }
            }
            return _instance;
        }
    }
    protected MonoSingleton() { }
    public static T GetInstance()
    {
        return _instance;
    }

    protected void Awake()
    {
        if (_instance != null && (object)_instance != this)
        {
            // 已有实例（可能是另一场景实例先执行了 Awake，或 Instance 访问已创建实例）：销毁重复实例
            DestroyImmediate(gameObject);
            return;
        }
        // 抢先认领实例：防止场景存在多个同类型组件时，在 Instance 首次被访问前全部并存
        _instance = this as T;
    }
    protected virtual void OnDestroy()
    {
        _instance = null;
    }
}