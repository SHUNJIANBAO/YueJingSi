using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

public class AppEntry : MonoSingleton<AppEntry>
{
    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        Init();

        UIManager.Instance.OpenPanel<UILogoPanel>(false);
    }

    void Init()
    {
        UIManager.Instance.Init();
    }
}
