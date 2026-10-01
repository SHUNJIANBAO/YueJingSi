using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 资源加载器契约（替代原双委托设计，便于热切换与依赖注入）。
/// 与 IAudioAssetLoader 模式对齐：path 为 Resources 相对路径（不含扩展名，如 "Prefabs/UI/UIRoomPanel/UIRoomPanel"）。
/// </summary>
public interface IUIAssetLoader
{
    /// <summary>异步实例化 UI 预制体，parent 为挂载父节点（可 null）</summary>
    void Instantiate(string path, Transform parent, Action<GameObject> onLoaded);

    /// <summary>释放 UI 实例（Resources 实现为空操作；Addressables 实现请换成 ReleaseInstance）</summary>
    void Release(GameObject instance);
}

/// <summary>
/// 可选：需要逐帧驱动的加载器（无 MonoBehaviour、无协程的加载器实现此接口，
/// 由 UIManager.Update 每帧调用 Tick() 轮询内部异步状态）。
/// </summary>
public interface IUpdatableUIAssetLoader : IUIAssetLoader
{
    /// <summary>每帧驱动一次（主线程调用）</summary>
    void Tick();
}

/// <summary>
/// UI 服务公共接口（面向业务层，隐藏内部面板字典/加载/Item 列表细节）。
/// 签名与 UIManager 既有公开 API 完全一致，便于替换实现与单测。
/// </summary>
public interface IUIService
{
    /// <summary>初始化服务（可重复调用热切换加载器）；loader 为 null 时自动创建默认 Resources 加载器</summary>
    void Init(IUIAssetLoader loader = null);

    /// <summary>服务是否已初始化（已注入加载器）</summary>
    bool IsInitialized { get; }

    Canvas Canvas { get; }

    // ================= 打开/关闭 =================
    void OpenPanel<T>(bool useAnim = true, Action callback = null, params object[] args) where T : UIPanelBase;
    void OpenPanel(Type type, bool useAnim = true, Action callback = null, params object[] args);
    void ClosePanel<T>(bool useAnim = true, Action callback = null, params object[] args) where T : UIPanelBase;
    void ClosePanel(UIPanelBase panel, bool useAnim = true, Action callback = null, params object[] args);
    void ClosePanel(Type type, bool useAnim = true, Action callback = null, params object[] args);
    void CloseAllNormalPanel(bool useAnim = true);

    // ================= 查询 =================
    bool IsOpen<T>() where T : UIPanelBase;
    T GetPanel<T>() where T : UIPanelBase;

    // ================= Item 列表 =================
    void ShowItemList<T1, T2, T3>(RectTransform parent, List<T3> dataList) where T1 : UIPanelBase where T2 : UIItemBase;
    void ReleseItemList(UIPanelBase panel);
    void ReleseItemList<T>() where T : UIPanelBase;
    void ReleseItemList<T>(RectTransform root) where T : UIPanelBase;
    List<T2> GetItemList<T1, T2>(RectTransform parent) where T1 : UIPanelBase where T2 : UIItemBase;
}

/// <summary>
/// 基于 Resources.LoadAsync 的默认 UI 加载器实现（纯 C# 类，不继承 MonoBehaviour）。
/// 不依赖协程：通过 IUpdatableUIAssetLoader.Tick() 由外部（UIManager.Update）逐帧轮询
/// 异步加载请求的完成状态，回调在主线程安全触发。
/// 同一路径并发请求合并回调，避免重复加载。
/// </summary>
public sealed class ResourcesUILoader : IUIAssetLoader, IUpdatableUIAssetLoader
{
    private readonly Dictionary<string, ResourceRequest> _pendingRequests = new Dictionary<string, ResourceRequest>();
    private readonly Dictionary<string, List<Action<GameObject>>> _pendingCallbacks = new Dictionary<string, List<Action<GameObject>>>();

    /// <summary>供 UIManager.Init(loader=null) 自动创建</summary>
    public static ResourcesUILoader Create()
    {
        return new ResourcesUILoader();
    }

    public void Instantiate(string path, Transform parent, Action<GameObject> callback)
    {
        // 同一路径并发请求：合并回调，避免重复加载
        if (_pendingCallbacks.TryGetValue(path, out var callbacks))
        {
            callbacks.Add(callback);
            return;
        }

        // 同步缓存命中（Resources 自动缓存已加载 Asset）
        var cached = Resources.Load<GameObject>(path);
        if (cached != null)
        {
            var go = UnityEngine.Object.Instantiate(cached, parent);
            callback?.Invoke(go);
            return;
        }

        // 发起异步加载，登记 pending；完成状态由 Tick() 统一轮询
        var request = Resources.LoadAsync<GameObject>(path);
        _pendingRequests[path] = request;
        _pendingCallbacks[path] = new List<Action<GameObject>> { callback };
    }

    /// <summary>
    /// 每帧由宿主（UIManager）调用：轮询所有异步请求，完成后实例化并分发回调、清理。
    /// 主线程安全：Resources/Unity API 仅在 Tick 调用线程（主线程）访问。
    /// </summary>
    public void Tick()
    {
        if (_pendingRequests.Count == 0) return;

        // 先收集已完成的 path，避免边遍历边改字典
        List<string> done = null;
        foreach (var kvp in _pendingRequests)
        {
            if (kvp.Value.isDone)
            {
                if (done == null) done = new List<string>();
                done.Add(kvp.Key);
            }
        }
        if (done == null) return;

        for (int i = 0; i < done.Count; i++)
        {
            string path = done[i];
            var request = _pendingRequests[path];
            _pendingRequests.Remove(path);
            if (_pendingCallbacks.TryGetValue(path, out var callbacks))
            {
                _pendingCallbacks.Remove(path);
                var prefab = request.asset as GameObject;
                foreach (var cb in callbacks)
                {
                    if (cb == null) continue;
                    if (prefab == null)
                    {
                        cb.Invoke(null);
                        continue;
                    }
                    var go = UnityEngine.Object.Instantiate(prefab);
                    cb.Invoke(go);
                }
            }
        }
    }

    public void Release(GameObject instance)
    {
        // Resources 无单实例显式卸载接口；Addressables 实现请换成 Addressables.ReleaseInstance
        if (instance != null)
            UnityEngine.Object.Destroy(instance);
    }
}