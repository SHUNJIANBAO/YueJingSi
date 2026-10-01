using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using TMPro;

/// <summary>
/// 房间UI面板，处理房间内的UI交互
/// </summary>
public class UIRoomPanel : UIPanelBase
{
    #region UI组件声明

    [Header("房间信息UI")] private Text Txt_RoomName; // 房间名称显示
    private Text Txt_PlayerCount; // 玩家数量显示
    private Text Txt_RoomInfo; // 房间详细信息

    [Header("玩家列表UI")] public RectTransform Root_PlayerList; // 玩家列表容器
    private ScrollRect Scroll_PlayerList; // 玩家列表滚动视图

    [Header("房主控制UI")] private Transform Panel_HostControls; // 房主控制面板
    private Button Btn_ChangeRoomName; // 修改房间名称按钮
    private Button Btn_SetPassword; // 设置密码按钮
    private Button Btn_StartGame; // 开始游戏按钮
    private Button Btn_CloseRoom; // 关闭房间按钮
    private Button Btn_InviteFriend; // 邀请好友按钮
    private TMP_InputField Input_RoomName; // 房间名称输入框
    private TMP_InputField Input_Password; // 密码输入框

    [Header("成员控制UI")] private Transform Panel_MemberControls; // 成员控制面板
    private Button Btn_LeaveRoom; // 离开房间按钮

    [Header("通用控制UI")] private Button Btn_Settings; // 设置按钮
    private Button Btn_Chat; // 聊天按钮
    private TMP_InputField Input_ChatMessage; // 聊天消息输入框
    private Button Btn_SendMessage; // 发送聊天按钮
    private ScrollRect Scroll_ChatHistory; // 聊天记录滚动视图

    // 玩家席位数据缓存（统一模型：真人 + 空位）
    private List<RoomSeatData> _seatList = new List<RoomSeatData>();

    #endregion

    #region 重写基类方法

    protected override void GetUIComponents()
    {
        // 房间信息UI
        Txt_RoomName = GetUI<Text>("Txt_RoomName");
        Txt_PlayerCount = GetUI<Text>("Txt_PlayerCount");
        Txt_RoomInfo = GetUI<Text>("Txt_RoomInfo");

        // 玩家列表UI
        Root_PlayerList = GetUI<RectTransform>("Root_PlayerList");
        Scroll_PlayerList = GetUI<ScrollRect>("Scroll_PlayerList");

        // 房主控制UI
        Panel_HostControls = GetUI<Transform>("Panel_HostControls");
        Btn_ChangeRoomName = GetUI<Button>("Btn_ChangeRoomName");
        Btn_SetPassword = GetUI<Button>("Btn_SetPassword");
        Btn_StartGame = GetUI<Button>("Btn_StartGame");
        Btn_CloseRoom = GetUI<Button>("Btn_CloseRoom");
        Input_RoomName = GetUI<TMP_InputField>("Input_RoomName");
        Input_Password = GetUI<TMP_InputField>("Input_Password");

        // 成员控制UI
        Panel_MemberControls = GetUI<Transform>("Panel_MemberControls");
        Btn_LeaveRoom = GetUI<Button>("Btn_LeaveRoom");
        Btn_InviteFriend = GetUI<Button>("Btn_InviteFriend");

        // 通用控制UI
        Btn_Settings = GetUI<Button>("Btn_Settings");
        Btn_Chat = GetUI<Button>("Btn_Chat");
        Input_ChatMessage = GetUI<TMP_InputField>("Input_ChatMessage");
        Btn_SendMessage = GetUI<Button>("Btn_SendMessage");
        Scroll_ChatHistory = GetUI<ScrollRect>("Scroll_ChatHistory");
    }

    protected override void AddUIListeners()
    {
        base.AddUIListeners();

        // 房主控制按钮
        Btn_ChangeRoomName?.onClick.AddListener(OnChangeRoomNameClicked);
        Btn_SetPassword?.onClick.AddListener(OnSetPasswordClicked);
        Btn_StartGame?.onClick.AddListener(OnStartGameClicked);
        Btn_CloseRoom?.onClick.AddListener(OnCloseRoomClicked);

        // 成员控制按钮
        Btn_LeaveRoom?.onClick.AddListener(OnLeaveRoomClicked);
        Btn_InviteFriend?.onClick.AddListener(OnInviteFriendClicked);

        // 通用控制按钮
        Btn_Settings?.onClick.AddListener(OnSettingsClicked);
        Btn_Chat?.onClick.AddListener(OnChatClicked);
        Btn_SendMessage?.onClick.AddListener(OnSendMessageClicked);

        // 输入框事件
        Input_ChatMessage?.onSubmit.AddListener(OnChatInputSubmitted);
        Input_RoomName?.onValueChanged.AddListener(OnRoomNameInputChanged);
        Input_Password?.onValueChanged.AddListener(OnPasswordInputChanged);

        // 注册管理器事件
        RegisterManagerEvents();
    }

    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();

        // 移除按钮监听
        Btn_ChangeRoomName?.onClick.RemoveAllListeners();
        Btn_SetPassword?.onClick.RemoveAllListeners();
        Btn_StartGame?.onClick.RemoveAllListeners();
        Btn_CloseRoom?.onClick.RemoveAllListeners();
        Btn_LeaveRoom?.onClick.RemoveAllListeners();
        Btn_InviteFriend?.onClick.RemoveAllListeners();
        Btn_Settings?.onClick.RemoveAllListeners();
        Btn_Chat?.onClick.RemoveAllListeners();
        Btn_SendMessage?.onClick.RemoveAllListeners();

        // 移除输入框监听
        Input_ChatMessage?.onSubmit.RemoveAllListeners();
        Input_RoomName?.onValueChanged.RemoveAllListeners();
        Input_Password?.onValueChanged.RemoveAllListeners();

        // 注销管理器事件
        UnregisterManagerEvents();
    }

    protected override void OnOpen(params object[] args)
    {
        base.OnOpen(args);
        // 更新UI状态
        RefreshRoomInfo();
        RefreshPlayerList();
        UpdateUIForCurrentRole();
    }

    #endregion

    #region 初始化和管理器相关

    /// <summary>
    /// 注册房间管理器事件。
    /// </summary>
    private void RegisterManagerEvents()
    {
        SteamRoomManager roomManager = SteamRoomManager.Instance;
        roomManager.OnRoomJoined += OnRoomJoined;
        roomManager.OnRoomLeave += OnRoomLeave;
        // 成员进出会改变席位表，房名变化会改变标题，都需要即时重画
        roomManager.OnPlayerJoined += RefreshPlayerList;
        roomManager.OnPlayerLeft += RefreshPlayerList;
        roomManager.OnPlayerKicked += RefreshPlayerList;
        roomManager.OnRoomNameChanged += RefreshRoomInfo;
        roomManager.OnError += OnRoomError;
    }

    /// <summary>
    /// 注销房间管理器事件。
    /// </summary>
    private void UnregisterManagerEvents()
    {
        // 管理器可能已先于面板销毁，只对现存实例解绑
        SteamRoomManager roomManager = SteamRoomManager.GetInstance();
        if (roomManager == null)
        {
            return;
        }

        roomManager.OnRoomJoined -= OnRoomJoined;
        roomManager.OnRoomLeave -= OnRoomLeave;
        roomManager.OnPlayerJoined -= RefreshPlayerList;
        roomManager.OnPlayerLeft -= RefreshPlayerList;
        roomManager.OnPlayerKicked -= RefreshPlayerList;
        roomManager.OnRoomNameChanged -= RefreshRoomInfo;
        roomManager.OnError -= OnRoomError;
    }

    #endregion

    #region UI更新方法

    /// <summary>
    /// 刷新房间信息显示
    /// </summary>
    private void RefreshRoomInfo()
    {
        if (SteamRoomManager.Instance == null) return;

        string roomInfo = SteamRoomManager.Instance.GetCurrentRoomInfo();
        if (Txt_RoomInfo != null)
            Txt_RoomInfo.text = roomInfo;

        // 更新房间名称
        string roomName = GetCurrentRoomName();
        if (Txt_RoomName != null)
            Txt_RoomName.text = roomName;
    }

    /// <summary>
    /// 按 Steam 房间成员刷新席位列表，空位补满房间人数上限。
    /// </summary>
    public void RefreshPlayerList()
    {
        if (Root_PlayerList == null)
        {
            return;
        }

        SteamRoomManager roomManager = SteamRoomManager.Instance;
        if (roomManager == null)
        {
            Debug.LogWarning("[UIRoomPanel] 房间管理器未就绪，跳过玩家列表刷新");
            return;
        }

        int maxPlayers = roomManager.GetMaxPlayersPerRoom();
        if (maxPlayers <= 0) maxPlayers = 4;

        List<CSteamID> members = roomManager.GetRoomMembers();
        bool isHost = CheckIsHost();
        CSteamID localId = SteamManager.Initialized ? SteamUser.GetSteamID() : CSteamID.Nil;

        _seatList = new List<RoomSeatData>(maxPlayers);

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < members.Count)
            {
                CSteamID memberId = members[i];
                _seatList.Add(new RoomSeatData
                {
                    SeatIndex = i,
                    SeatType = E_SeatType.Human,
                    PlayerName = memberId == localId
                        ? SteamFriends.GetPersonaName()
                        : SteamFriends.GetFriendPersonaName(memberId),
                    IsHost = roomManager.IsPlayerHost(memberId),
                });
            }
            else
            {
                // 空位
                _seatList.Add(new RoomSeatData
                {
                    SeatIndex = i,
                    SeatType = E_SeatType.Empty,
                    PlayerName = "空位",
                    IsHost = false,
                });
            }
        }

        UIManager.Instance.ShowItemList<UIRoomPanel, UIPlayerItem, RoomSeatData>(Root_PlayerList, _seatList);

        // 更新玩家数量显示
        if (Txt_PlayerCount != null)
        {
            Txt_PlayerCount.text = $"玩家: {members.Count}/{maxPlayers}";
        }

        // 房主切换后控件区需要跟随刷新
        UpdateUIForCurrentRole(isHost);
    }

    /// <summary>
    /// 获取当前客户端是否为房主。
    /// </summary>
    /// <returns>本地玩家是房间房主时返回真。</returns>
    private bool CheckIsHost()
    {
        if (SteamRoomManager.Instance == null || !SteamManager.Initialized)
            return false;

        if (SteamRoomManager.Instance.currentLobbyId == CSteamID.Nil)
            return false;

        return SteamRoomManager.Instance.IsHost;
    }

    /// <summary>
    /// 根据当前角色更新UI
    /// </summary>
    private void UpdateUIForCurrentRole()
    {
        UpdateUIForCurrentRole(CheckIsHost());
    }

    /// <summary>
    /// 按指定房主身份切换房主控件区与成员控件区。
    /// </summary>
    /// <param name="isHost">本地玩家是否为房主。</param>
    private void UpdateUIForCurrentRole(bool isHost)
    {
        if (isHost)
        {
            Panel_HostControls?.gameObject.SetActive(true);
            Panel_MemberControls?.gameObject.SetActive(false);
            UpdateHostUI();
        }
        else
        {
            Panel_HostControls?.gameObject.SetActive(false);
            Panel_MemberControls?.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 更新房主UI
    /// </summary>
    private void UpdateHostUI()
    {
        // 开局由房主单方面决定，不受其他成员状态约束
        if (Btn_StartGame != null)
        {
            Btn_StartGame.interactable = true;
        }

        // 更新房间设置输入框
        if (Input_RoomName != null)
        {
            Input_RoomName.text = GetCurrentRoomName();
        }

        // 初始化时隐藏修改按钮
        if (Btn_ChangeRoomName != null)
        {
            Btn_ChangeRoomName.gameObject.SetActive(false);
        }

        if (Btn_SetPassword != null)
        {
            Btn_SetPassword.gameObject.SetActive(false);
        }
    }

    #endregion

    #region 按钮事件处理

    private void OnChangeRoomNameClicked()
    {
        if (SteamRoomManager.Instance != null && Input_RoomName != null)
        {
            string newName = Input_RoomName.text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                SteamRoomManager.Instance.ChangeRoomName(newName);
                // 修改成功后隐藏按钮
                if (Btn_ChangeRoomName != null)
                {
                    Btn_ChangeRoomName.gameObject.SetActive(false);
                }
            }
        }
    }

    private void OnSetPasswordClicked()
    {
        if (SteamRoomManager.Instance != null && Input_Password != null)
        {
            string password = Input_Password.text;
            SteamRoomManager.Instance.SetRoomPassword(password);
            // 设置成功后隐藏按钮
            if (Btn_SetPassword != null)
            {
                Btn_SetPassword.gameObject.SetActive(false);
            }
        }
    }

    private void OnStartGameClicked()
    {
        SteamRoomManager.Instance?.StartGame();
    }

    /// <summary>
    /// 点击关闭房间：房主解散房间，清空房间内玩家并退回上一层。
    /// </summary>
    private void OnCloseRoomClicked()
    {
        ExitRoom();
    }

    /// <summary>
    /// 点击离开房间：非房主成员退出房间并退回上一层。
    /// </summary>
    private void OnLeaveRoomClicked()
    {
        ExitRoom();
    }

    /// <summary>
    /// 房间统一出口：按角色选择解散或离开，并清理本次房间的临时状态。
    /// </summary>
    private void ExitRoom()
    {
        // 先立即关闭房间面板：房间列表常驻在下方，关掉它即可露出；
        // 必须先于会话收尾执行，否则随后的离开事件会再发起一次带 1 秒动画的关闭
        UIManager.Instance.ClosePanel<UIRoomPanel>(false);

        SteamRoomManager roomManager = SteamRoomManager.Instance;
        if (roomManager == null) return;

        if (roomManager.IsHost)
            roomManager.CloseRoom();
        else
            roomManager.LeaveRoom();
    }

    /// <summary>
    /// 点击邀请好友：交由房间管理器调起 Steam 覆盖层邀请对话框。
    /// </summary>
    private void OnInviteFriendClicked()
    {
        SteamRoomManager.Instance?.OpenInviteDialog();
    }

    private void OnSettingsClicked()
    {
        Debug.Log("[UIRoomPanel] 房间设置功能尚未接入");
    }

    private void OnChatClicked()
    {
        bool isActive = Scroll_ChatHistory?.gameObject.activeSelf ?? false;
        Scroll_ChatHistory?.gameObject.SetActive(!isActive);
    }

    private void OnSendMessageClicked()
    {
        SendChatMessage();
    }

    /// <summary>
    /// 聊天输入框提交事件
    /// </summary>
    /// <param name="message">输入框当前文本。</param>
    private void OnChatInputSubmitted(string message)
    {
        SendChatMessage();
    }

    #endregion

    #region 聊天功能

    private void SendChatMessage()
    {
        if (Input_ChatMessage != null && !string.IsNullOrEmpty(Input_ChatMessage.text))
        {
            string message = Input_ChatMessage.text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                Debug.Log($"[UIRoomPanel] 发送聊天消息:{message}");
                Input_ChatMessage.text = "";
            }
        }
    }

    #endregion

    #region 管理器事件响应

    /// <summary>
    /// 响应加入房间事件，重画房间信息与席位表。
    /// </summary>
    private void OnRoomJoined()
    {
        RefreshRoomInfo();
        RefreshPlayerList();
    }

    private void OnRoomLeave()
    {
        UIManager.Instance.ClosePanel<UIRoomPanel>();
    }

    /// <summary>
    /// 响应房间管理器错误事件，把失败原因以飘字提示呈现给玩家。
    /// </summary>
    /// <param name="message">房间管理器给出的错误描述。</param>
    private void OnRoomError(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        UIHandle.ShowTips(message);
    }

    #endregion

    #region 输入框变化处理

    private void OnRoomNameInputChanged(string newValue)
    {
        // 只在房主模式下处理
        if (!SteamRoomManager.Instance.IsHost) return;

        // 检查输入是否与当前房间名称不同
        string currentName = GetCurrentRoomName();
        bool hasChanged = !string.IsNullOrEmpty(newValue) && newValue != currentName;

        // 显示/隐藏修改按钮
        if (Btn_ChangeRoomName != null)
        {
            Btn_ChangeRoomName.gameObject.SetActive(hasChanged);
        }
    }

    private void OnPasswordInputChanged(string newValue)
    {
        // 只在房主模式下处理
        if (!SteamRoomManager.Instance.IsHost) return;

        // 检查输入是否与当前密码不同（这里需要从SteamRoomManager获取当前密码）
        bool hasChanged = !string.IsNullOrEmpty(newValue);

        // 显示/隐藏设置密码按钮
        if (Btn_SetPassword != null)
        {
            Btn_SetPassword.gameObject.SetActive(hasChanged);
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 获取当前房间名称
    /// </summary>
    private string GetCurrentRoomName()
    {
        if (SteamRoomManager.Instance != null && SteamRoomManager.Instance.IsInRoom())
        {
            return Steamworks.SteamMatchmaking.GetLobbyData(SteamRoomManager.Instance.currentLobbyId, "name");
        }
        return "房间名称";
    }

    #endregion
}
