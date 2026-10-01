using Mirror;
using UnityEngine;

/// <summary>
/// 对局断线监听组件，挂在网络管理器物体上，把客户端与服务器的连接、断开事件转交给对局断线处理。
/// </summary>
public class MatchDisconnectWatcher : MonoBehaviour
{
    // 本客户端本次会话是否成功连上过服务器
    private bool _hasConnected;

    // 当前是否已订阅 NetworkClient 的静态事件
    private bool _subscribed;

    /// <summary>
    /// 在指定网络管理器所在物体上获取或补挂监听组件。
    /// </summary>
    /// <param name="networkManager">要监听断线的网络管理器。</param>
    /// <returns>挂在该网络管理器物体上的监听组件，网络管理器为空时返回空。</returns>
    public static MatchDisconnectWatcher EnsureOn(NetworkManager networkManager)
    {
        if (networkManager == null)
            return null;

        MatchDisconnectWatcher watcher = networkManager.GetComponent<MatchDisconnectWatcher>();
        if (watcher == null)
            watcher = networkManager.gameObject.AddComponent<MatchDisconnectWatcher>();

        return watcher;
    }

    /// <summary>
    /// 订阅客户端的连接与断开事件。
    /// </summary>
    public void Subscribe()
    {
        // 网络管理器的 StartClient 会用整体赋值重写这两个静态事件，订阅只能排在其后；
        // 断开时 NetworkClient 又会把事件清空，因此每次连接都要重新订阅一次
        Unsubscribe();

        NetworkClient.OnConnectedEvent += OnClientConnected;
        NetworkClient.OnDisconnectedEvent += OnClientDisconnected;
        _subscribed = true;
    }

    /// <summary>
    /// 解除对客户端连接与断开事件的订阅。
    /// </summary>
    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        NetworkClient.OnConnectedEvent -= OnClientConnected;
        NetworkClient.OnDisconnectedEvent -= OnClientDisconnected;
        _subscribed = false;
    }

    /// <summary>
    /// 处理客户端连接成功事件。
    /// </summary>
    private void OnClientConnected()
    {
        _hasConnected = true;
    }

    /// <summary>
    /// 处理客户端断开连接事件。
    /// </summary>
    private void OnClientDisconnected()
    {
        // 从未连上过的连接失败不属于对局掉线，不应弹出退出面板
        bool wasConnected = _hasConnected;
        _hasConnected = false;

        if (!wasConnected)
            return;

        MatchDisconnectHandler.HandleDisconnected();
    }

    /// <summary>
    /// 组件销毁时解除事件订阅。
    /// </summary>
    private void OnDestroy()
    {
        Unsubscribe();
    }
}
