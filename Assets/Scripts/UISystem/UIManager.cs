using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class UIManager : MonoSingleton<UIManager>, IUIService
{
    Canvas _mainCanvas;
    public Canvas Canvas => _mainCanvas;
    public RectTransform TempRoot;
    public RectTransform PanelRoot;
    public RectTransform TopRoot;

    Dictionary<string, UIPanelBase> _openPanelDict = new Dictionary<string, UIPanelBase>(); //打开的panel
    Dictionary<string, UIPanelBase> _closePanelDict = new Dictionary<string, UIPanelBase>();
    /// <summary>
    /// 正在播放关闭动画的面板（登记后，在其被重新打开时可取消关闭流程，防止旧回调破坏状态）
    /// </summary>
    Dictionary<string, UIPanelBase> _closingPanelDict = new Dictionary<string, UIPanelBase>();
    /// <summary>
    /// 加载中的面板（合并并发请求，避免同一面板重复实例化）
    /// </summary>
    Dictionary<string, List<Action<GameObject>>> _loadingPanelDict = new Dictionary<string, List<Action<GameObject>>>();
    /// <summary>
    /// 动态item列表
    /// </summary>
    Dictionary<string, Dictionary<RectTransform, List<UIItemBase>>> _uiItemDict = new Dictionary<string, Dictionary<RectTransform, List<UIItemBase>>>();

    public delegate void InstantiateUiAsset(string path, Transform parent, Action<GameObject> callback);
    public delegate void DestroyUiAsset(GameObject ui);

    private IUIAssetLoader _loader;
    private bool _initialized;

    /// <summary>服务是否已初始化（已注入加载器）</summary>
    public bool IsInitialized => _initialized && _loader != null;

    /// <summary>
    /// 初始化服务（可重复调用热切换加载器）；loader 为 null 时自动创建默认 Resources 加载器。
    /// </summary>
    public void Init(IUIAssetLoader loader = null)
    {
        if (loader == null) loader = ResourcesUILoader.Create();
        _loader = loader;
        _initialized = true;
        Debug.Log("[UIManager] 初始化完成（默认 Resources 加载器）");
    }

    /// <summary>
    /// 兼容旧双委托注入：包装为 IUIAssetLoader。
    /// </summary>
    public void Init(InstantiateUiAsset createUiHandler, DestroyUiAsset destroyUiHandler)
    {
        _loader = new DelegateUIAssetLoader(createUiHandler, destroyUiHandler);
        _initialized = true;
        Debug.Log("[UIManager] 初始化完成（委托加载器）");
    }

    /// <summary>
    /// 兼容旧双委托注入（单委托版，供旧代码调用）。
    /// </summary>
    public void Init(InstantiateUiAsset createUiHandler)
    {
        Init(createUiHandler, null);
    }

    private void Start()
    {
        _mainCanvas = GetComponent<Canvas>();
        // 兜底：若外部未显式 Init，自动使用默认 Resources 加载器
        if (!_initialized) Init();
    }

    private void Update()
    {
        // 驱动纯 C# 加载器（无协程实现）逐帧轮询异步加载状态；UIManager 本身仍是 MonoBehaviour 宿主
        (_loader as IUpdatableUIAssetLoader)?.Tick();
    }

    /// <summary>
    /// 面板销毁时清理各级缓存字典。
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();

        _openPanelDict.Clear();
        _closingPanelDict.Clear();
        _loadingPanelDict.Clear();
        _closePanelDict.Clear();
        _uiItemDict.Clear();
    }

    void LoadPanel(string uiName, Action<GameObject> callback = null)
    {
        if (!IsInitialized)
        {
            Debug.LogError($"[UIManager] 未初始化资源加载器，请先调用 Init(loader)。");
            callback?.Invoke(null);
            return;
        }

        // 合并并发请求：同一面板加载中再次请求时只追加回调，不重复 Instantiate
        if (_loadingPanelDict.TryGetValue(uiName, out var waiters))
        {
            if (callback != null) waiters.Add(callback);
            return;
        }
        waiters = new List<Action<GameObject>>();
        if (callback != null) waiters.Add(callback);
        _loadingPanelDict[uiName] = waiters;

        _loader.Instantiate(GetPanelPath(uiName), TempRoot, go =>
        {
            if (go != null) OnLoadPanel(go);
            if (_loadingPanelDict.TryGetValue(uiName, out var list))
            {
                _loadingPanelDict.Remove(uiName);
                for (int i = 0; i < list.Count; i++) list[i]?.Invoke(go);
            }
        });
    }

    string GetPanelPath(string uiName)
    {
        return $"Prefabs/UI/{uiName}/{uiName}";
    }
    string GetItemPath(string uiName)
    {
        return $"Prefabs/UI/UIItem/{uiName}";
    }
    /// <summary>
    /// 面板实例加载完成后的登记与挂点处理。
    /// </summary>
    /// <param name="uiGo">加载出来的面板实例。</param>
    void OnLoadPanel(GameObject uiGo)
    {
        if (uiGo == null) return;

        var panel = uiGo.GetComponent<UIPanelBase>();
        if (panel == null)
        {
            // 预制体漏挂面板基类时直接释放，避免后续取类型名时空引用
            Debug.LogError($"[UIManager] 面板预制体缺少 UIPanelBase，实例名:{uiGo.name}");
            _loader?.Release(uiGo);
            return;
        }

        var panelName = panel.GetType().Name;
        _closePanelDict[panelName] = panel; // 索引器赋值，避免重复Key异常
        switch (panel.PanelType)
        {
            case E_PanelType.Normal:
                panel.transform.SetParent(PanelRoot);
                break;
            case E_PanelType.Top:
                panel.transform.SetParent(TopRoot);
                break;
        }
    }
    void UnloadPanel(UIPanelBase panel)
    {
        ReleseItemList(panel);
        if (panel != null && _loader != null)
            _loader.Release(panel.gameObject);
    }

    #region 打开界面
    public void OpenPanel<T>(bool useAnim = true, Action callback = null, params object[] args) where T : UIPanelBase
    {
        OpenPanel(typeof(T), useAnim, callback, args);
    }

    public void OpenPanel(Type type, bool useAnim = true, Action callback = null, params object[] args)
    {
        OpenPanel(type.Name, useAnim, callback, args);
    }
    void OpenPanel(string panelName, bool useAnim, Action callback, params object[] args)
    {
        var panel = GetPanel(panelName);
        if (panel == null)
        {
            LoadPanel(panelName, go =>
            {
                if (go == null) return;
                OpenPanel(panelName, useAnim, callback, args);
            });
            return;
        }
        // 若该面板正在播放关闭动画，取消其关闭流程（杀掉动画，阻断旧关闭回调执行）
        CancelClosingPanel(panelName);
        _closePanelDict.Remove(panelName);
        _openPanelDict[panelName] = panel;
        panel.Open(args);
        switch (panel.PanelType)
        {
            case E_PanelType.Normal:
                panel.transform.SetParent(PanelRoot);
                break;
            case E_PanelType.Top:
                panel.transform.SetParent(TopRoot);
                break;
        }
        if (useAnim)
        {
            panel.PlayOpenAnim(callback);
        }
        else
        {
            panel.QuickShow();
            callback?.Invoke();
        }
    }
    #endregion

    #region 关闭界面
    public void ClosePanel<T>(bool useAnim = true, Action callback = null, params object[] args) where T : UIPanelBase
    {
        ClosePanel(typeof(T).Name, useAnim, callback, args);
    }

    public void ClosePanel(UIPanelBase panel, bool useAnim = true, Action callback = null, params object[] args)
    {
        ClosePanel(panel.GetType().Name, useAnim, callback, args);
    }

    public void ClosePanel(Type type, bool useAnim = true, Action callback = null, params object[] args)
    {
        ClosePanel(type.Name, useAnim, callback, args);
    }

    void ClosePanel(string panelName, bool useAnim, Action callback, params object[] args)
    {
        var panel = GetOpenPanel(panelName);
        if (panel == null)
        {
            return;
        }

        _openPanelDict.Remove(panelName);
        _closingPanelDict[panelName] = panel; // 登记为"正在关闭"，供 OpenPanel 取消
        callback += () =>
        {
            // 校验：若面板在关闭动画期间被重新打开，则旧关闭回调不再执行
            if (!_closingPanelDict.TryGetValue(panelName, out var curPanel) ||
                !ReferenceEquals(curPanel, panel))
            {
                return;
            }
            _closingPanelDict.Remove(panelName);
            panel.Close(args);
            if (panel.PanelMemoryType == E_PanelMemoryType.Forever)
            {
                panel.transform.SetParent(TempRoot);
                _closePanelDict[panelName] = panel; // 索引器赋值，避免重复Key异常
            }
            else
                UnloadPanel(panel);
        };
        if (useAnim)
        {
            panel.PlayCloseAnim(callback);
        }
        else
        {
            panel.QuickHide();
            callback?.Invoke();
        }

    }
    #endregion

    public void CloseAllNormalPanel(bool useAnim = true)
    {
        List<UIPanelBase> panelList = new List<UIPanelBase>();
        foreach (var panel in _openPanelDict.Values)
        {
            if (panel.PanelType == E_PanelType.Normal)
            {
                panelList.Add(panel);
            }
        }

        foreach (var panel in panelList)
        {
            ClosePanel(panel, useAnim);
        }
    }

    public bool IsOpen<T>() where T : UIPanelBase
    {
        return GetOpenPanel<T>() != null;
    }

    public T GetPanel<T>() where T : UIPanelBase
    {
        var panel = GetOpenPanel<T>();
        if (panel == null)
            panel = GetClosePanel<T>();
        if (panel == null)
            panel = GetClosingPanel<T>();
        return panel;
    }
    UIPanelBase GetPanel(string panelName)
    {
        var panel = GetOpenPanel(panelName);
        if (panel == null)
            panel = GetClosePanel(panelName);
        if (panel == null)
            panel = GetClosingPanel(panelName);
        return panel;
    }
    T GetClosingPanel<T>() where T : UIPanelBase
    {
        return GetClosingPanel(typeof(T).Name) as T;
    }
    UIPanelBase GetClosingPanel(string panelName)
    {
        _closingPanelDict.TryGetValue(panelName, out UIPanelBase tempPanel);
        return tempPanel;
    }

    /// <summary>
    /// 取消面板的关闭流程。面板正在播放关闭动画时被重新打开，应杀掉动画并阻断旧关闭回调，
    /// 避免旧回调在动画结束后把面板放回关闭字典，导致状态不一致。
    /// </summary>
    void CancelClosingPanel(string panelName)
    {
        if (_closingPanelDict.TryGetValue(panelName, out UIPanelBase panel))
        {
            _closingPanelDict.Remove(panelName);
            panel.CancelAnim(); // 杀掉关闭动画，其 OnComplete 不再触发，旧回调不会执行
        }
    }
    T GetClosePanel<T>() where T : UIPanelBase
    {
        var panelName = typeof(T).Name;
        return GetClosePanel(panelName) as T;
    }
    UIPanelBase GetClosePanel(string panelName)
    {
        _closePanelDict.TryGetValue(panelName, out UIPanelBase tempPanel);
        return tempPanel;
    }

    T GetOpenPanel<T>() where T : UIPanelBase
    {
        return GetOpenPanel(typeof(T)) as T;
    }

    UIPanelBase GetOpenPanel(Type type)
    {
        return GetOpenPanel(type.Name);
    }

    UIPanelBase GetOpenPanel(string panelName)
    {
        _openPanelDict.TryGetValue(panelName, out UIPanelBase tempPanel);
        return tempPanel;
    }
    void LoadItem(string uiName, Action<GameObject> callback = null)
    {
        string path = GetItemPath(uiName);
        if (_loader != null)
            _loader.Instantiate(path, TempRoot, callback);
        else
            callback?.Invoke(null);
    }
    void ReleseItem(UIItemBase item)
    {
        if (item == null) return; // 异步占位项：未加载完成直接忽略
        if (_loader != null)
            _loader.Release(item.gameObject);
    }


    public void ShowItemList<T1, T2, T3>(RectTransform parent, List<T3> dataList) where T1 : UIPanelBase where T2 : UIItemBase
    {
        var panelName = typeof(T1).Name;
        if (!_uiItemDict.TryGetValue(panelName, out var dict))
        {
            dict = new Dictionary<RectTransform, List<UIItemBase>>();
            _uiItemDict[panelName] = dict;
        }
        if (!dict.TryGetValue(parent, out List<UIItemBase> itemList))
        {
            itemList = new List<UIItemBase>();
            dict[parent] = itemList;
        }

        for (int i = 0; i < Mathf.Max(itemList.Count, dataList.Count); i++)
        {
            //超过数据长度的隐藏
            if (i >= dataList.Count)
            {
                if (itemList[i] != null)
                    itemList[i].gameObject.SetActive(false);
                continue;
            }

            if (i >= itemList.Count)
            {
                // 先占位（null），保证 itemList 长度与数据索引对齐；
                // 异步回调里按 index 赋值，不依赖回调完成顺序，避免顺序错乱
                itemList.Add(null);
                int index = i;
                LoadItem(typeof(T2).Name, itemGo =>
                {
                    if (itemGo == null) return;
                    var item = itemGo.GetComponent<T2>();
                    if (item == null)
                    {
                        Debug.LogError($"[UIManager] 列表项预制体缺少组件，预制体名:{typeof(T2).Name}");
                        _loader?.Release(itemGo);
                        return;
                    }
                    item.transform.SetParent(parent, false);
                    item.SetData(index, dataList[index]);
                    itemList[index] = item;
                    item.gameObject.SetActive(true);
                });
            }
            else
            {
                if (itemList[i] is T2)
                {
                    itemList[i].SetData(i, dataList[i]);
                    itemList[i].gameObject.SetActive(true);
                }
                else if (itemList[i] == null)
                {
                    // 该位置仍在异步加载中，等待回调填充
                }
                else
                {
                    Debug.LogError("[UIManager] 同一节点下只能生成一种类型item");
                }
            }
        }
    }
    public void ReleseItemList(UIPanelBase panel)
    {
        if (panel == null)
            return;
        var panelName = panel.GetType().Name;
        ReleseItemList(panelName);
    }
    public void ReleseItemList<T>() where T : UIPanelBase
    {
        var panelName = typeof(T).Name;
        ReleseItemList(panelName);
    }
    void ReleseItemList(string panelName)
    {
        if (!_uiItemDict.TryGetValue(panelName, out var dict))
        {
            return;
        }
        foreach (var itemList in dict.Values)
        {
            foreach (var item in itemList)
            {
                ReleseItem(item);
            }
        }
        _uiItemDict.Remove(panelName);
    }
    public void ReleseItemList<T>(RectTransform root) where T : UIPanelBase
    {
        ReleseItemList(typeof(T).Name, root);
    }
    void ReleseItemList(string panelName, RectTransform root)
    {
        if (!_uiItemDict.TryGetValue(panelName, out var dict))
        {
            return;
        }
        if (!dict.TryGetValue(root, out var itemList))
        {
            return;
        }
        foreach (var item in itemList)
        {
            ReleseItem(item);
        }

        dict.Remove(root);
    }
    public List<T2> GetItemList<T1, T2>(RectTransform parent) where T1 : UIPanelBase where T2 : UIItemBase
    {
        var panelName = typeof(T1).Name;
        if (!_uiItemDict.TryGetValue(panelName, out var dict))
        {
            return new List<T2>();
        }
        if (!dict.TryGetValue(parent, out var itemList))
        {
            return new List<T2>();
        }
        var tmpList = new List<T2>(itemList.Count);
        foreach (var item in itemList)
        {
            tmpList.Add(item as T2);
        }
        return tmpList;
    }
}

/// <summary>
/// 兼容旧双委托的加载器包装。
/// </summary>
public sealed class DelegateUIAssetLoader : IUIAssetLoader
{
    private readonly UIManager.InstantiateUiAsset _create;
    private readonly UIManager.DestroyUiAsset _destroy;

    public DelegateUIAssetLoader(UIManager.InstantiateUiAsset create, UIManager.DestroyUiAsset destroy)
    {
        _create = create;
        _destroy = destroy;
    }

    public void Instantiate(string path, Transform parent, Action<GameObject> onLoaded)
    {
        if (_create != null) _create(path, parent, onLoaded);
        else onLoaded?.Invoke(null);
    }

    public void Release(GameObject instance)
    {
        if (_destroy != null) _destroy(instance);
        else if (instance != null) UnityEngine.Object.Destroy(instance);
    }
}