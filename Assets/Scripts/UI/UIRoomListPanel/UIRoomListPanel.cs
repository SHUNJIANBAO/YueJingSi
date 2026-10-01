using System.Collections;
using System.Collections.Generic;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 房间列表面板，负责展示、筛选与搜索可加入的 Steam 房间。
/// </summary>
public class UIRoomListPanel : UIPanelBase
{
    // 本面板所属的 Steam 联机子场景名
    private const string STEAM_SCENE = "SteamScene";

    // 房间列表容器
    private RectTransform Root_Room;

    // 刷新按钮
    private Button Btn_Refresh;


    // 创建房间按钮
    private Button Btn_CreateRoom;

    // 返回主界面按钮
    private Button Btn_Back;


    /// <summary>
    /// 获取并缓存面板内 UI 组件引用。
    /// </summary>
    protected override void GetUIComponents()
    {
        Root_Room = GetUI<RectTransform>("Root_Room");
        Btn_Refresh = GetUI<Button>("Btn_Refresh");
        Btn_CreateRoom = GetUI<Button>("Btn_CreateRoom");
        Btn_Back = GetUI<Button>("Btn_Back");
    }

    /// <summary>
    /// 绑定按钮、开关事件并订阅房间管理器事件。
    /// </summary>
    protected override void AddUIListeners()
    {
        base.AddUIListeners();

        AddButtonListen(Btn_Refresh, OnRefreshClicked);
        AddButtonListen(Btn_CreateRoom, OnCreateRoomClicked);
        AddButtonListen(Btn_Back, OnBackClicked);

        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.OnRoomListUpdated += OnRoomListUpdated;
            SteamRoomManager.Instance.OnSearchError += OnSearchError;
            SteamRoomManager.Instance.OnRoomJoined += OnRoomJoined;
            SteamRoomManager.Instance.OnRoomLeave += OnRoomLeft;
        }
    }

    /// <summary>
    /// 解绑事件并取消订阅房间管理器事件。
    /// </summary>
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();

        RemoveButtonListen(Btn_Refresh, OnRefreshClicked);
        RemoveButtonListen(Btn_CreateRoom, OnCreateRoomClicked);
        RemoveButtonListen(Btn_Back, OnBackClicked);

        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.OnRoomJoined -= OnRoomJoined;
            SteamRoomManager.Instance.OnRoomLeave -= OnRoomLeft;
            SteamRoomManager.Instance.OnRoomListUpdated -= OnRoomListUpdated;
            SteamRoomManager.Instance.OnSearchError -= OnSearchError;
        }
    }

    /// <summary>
    /// 打开面板时按当前是否在房间中决定自动刷新并立即拉取一次列表。
    /// </summary>
    /// <param name="args">打开参数。</param>
    protected override void OnOpen(params object[] args)
    {
        base.OnOpen(args);

        if (SteamRoomManager.Instance == null) return;

        // 已在房间中时不再轮询刷新
        bool isInRoom = SteamRoomManager.Instance.IsInRoom();
        SteamRoomManager.Instance.SetRoomListAutoRefresh(!isInRoom);
        SteamRoomManager.Instance.ForceRefreshRoomList();
    }

    /// <summary>
    /// 关闭面板时停止自动刷新：该开关保存在 SteamRoomManager/RoomList 上，
    /// 不随面板销毁而复位，若不显式关闭会在面板已销毁后持续请求 Steam 房间列表。
    /// </summary>
    /// <param name="args">关闭参数。</param>
    public override void OnClose(params object[] args)
    {
        base.OnClose(args);

        // 单例可能已在退出流程中销毁，使用非创建式访问避免反向创建
        SteamRoomManager.GetInstance()?.SetRoomListAutoRefresh(false);
    }

    /// <summary>
    /// 点击刷新：立即拉取一次房间列表。
    /// </summary>
    private void OnRefreshClicked()
    {
        SteamRoomManager.Instance.ForceRefreshRoomList();
    }

    /// <summary>
    /// 点击返回：卸载 Steam 子场景并回到主界面。
    /// </summary>
    private void OnBackClicked()
    {
        // 仍在房间中时先退房，避免房间状态残留到主界面
        if (SteamRoomManager.Instance != null && SteamRoomManager.Instance.IsInRoom())
            SteamRoomManager.Instance.LeaveRoom();

        // 遮罩的打开动画回调在 alpha 到达 1 时触发，此处以它为起点，
        // 保证卸载与界面切换都发生在屏幕已完全变黑之后
        UIManager.Instance.OpenPanel<UIMaskPanel>(true, () => StartCoroutine(ReturnToMainScene()));
    }

    /// <summary>
    /// 黑屏期间卸载本面板所属子场景，卸载完成并重开主界面后请求遮罩淡出。
    /// </summary>
    private IEnumerator ReturnToMainScene()
    {
        // 卸载子场景会一并销毁网络管理器物体，先结束尚未收尾的联机会话
        StopActiveNetworkSession();

        Scene steamScene = SceneManager.GetSceneByName(STEAM_SCENE);
        if (steamScene.isLoaded)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(steamScene);
            if (operation != null)
            {
                yield return operation;
            }
        }

        UIManager.Instance.OpenPanel<UIMainPanel>(false);

        // 场景与界面均已就绪，此时才允许遮罩淡出
        UIManager.Instance.GetPanel<UIMaskPanel>()?.RequestReveal();
        UIManager.Instance.ClosePanel<UIRoomListPanel>(false);
    }

    

    /// <summary>
    /// 结束当前尚未收尾的联机会话。
    /// </summary>
    private void StopActiveNetworkSession()
    {
        NetworkManager networkManager = NetworkManager.singleton;
        if (networkManager == null) return;

        if (NetworkServer.active)
            networkManager.StopHost();
        else if (NetworkClient.active)
            networkManager.StopClient();
    }

    /// <summary>
    /// 按当前搜索与筛选条件刷新房间列表显示。
    /// </summary>
    private void RefreshRoomList()
    {
        if (SteamRoomManager.Instance == null || Root_Room == null) return;

        List<RoomData> filteredRooms;

        // 密码只用于列表标记与进入房间时的校验，不作为筛选条件
        filteredRooms = SteamRoomManager.Instance.GetFilteredRooms(
            hasPassword: null,
            notFull: false
        );

        UIManager.Instance.ShowItemList<UIRoomListPanel, UIRoomItem, RoomData>(Root_Room, filteredRooms);
    }

    /// <summary>
    /// 房间列表更新事件：刷新界面显示。
    /// </summary>
    /// <param name="roomList">最新的房间列表。</param>
    private void OnRoomListUpdated(List<RoomData> roomList)
    {
        RefreshRoomList();
    }

    /// <summary>
    /// 搜索错误事件：提示错误信息。
    /// </summary>
    /// <param name="errorMessage">错误信息。</param>
    private void OnSearchError(string errorMessage)
    {
        UIHandle.ShowTips(errorMessage);
    }

    /// <summary>
    /// 点击创建房间：带上默认房间名打开创建面板。
    /// </summary>
    private void OnCreateRoomClicked()
    {
        string playerName = SteamManager.Initialized ? SteamFriends.GetPersonaName() : string.Empty;
        UIManager.Instance.OpenPanel<UICreateRoomPanel>(true, null, playerName);
    }



    /// <summary>
    /// 加入房间成功事件：停止自动刷新并打开房间面板。
    /// </summary>
    private void OnRoomJoined()
    {
        if (SteamRoomManager.Instance == null) return;

        SteamRoomManager.Instance.SetRoomListAutoRefresh(false);

        if (!SteamRoomManager.Instance.IsInRoom()) return;

        // 房间面板已打开时不再重复打开
        var roomPanel = UIManager.Instance.GetPanel<UIRoomPanel>();
        if (roomPanel == null)
        {
            UIManager.Instance.OpenPanel<UIRoomPanel>();
        }
        else
        {
            Debug.Log("[UIRoomListPanel] 房间界面已经打开，跳过重复打开");
        }
    }

    /// <summary>
    /// 离开房间事件：恢复自动刷新并立即刷新一次。
    /// </summary>
    private void OnRoomLeft()
    {
        if (SteamRoomManager.Instance == null) return;

        SteamRoomManager.Instance.SetRoomListAutoRefresh(true);
        SteamRoomManager.Instance.ForceRefreshRoomList();
    }
}
