using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Steamworks;

/// <summary>
/// Steam房间管理器，处理房间创建、加入、管理等功能
/// </summary>
public class SteamRoomManager : MonoSingleton<SteamRoomManager>
{
    // 房主 SteamID 在 lobby 数据中的键
    private const string HOST_ADDRESS_KEY = "hostAddress";

    // 房间是否已开局在 lobby 数据中的键
    private const string STARTED_KEY = "started";

    // 房间是否已被房主解散在 lobby 数据中的键
    private const string CLOSED_KEY = "closed";

    // 成员连接房主的超时秒数
    private const float CONNECT_TIMEOUT_SECONDS = 5f;

    private readonly RoomList roomList = new RoomList(); // 房间列表组件（由本管理器创建并管理）

    private int maxPlayersPerRoom = 4; // 房间最大人数

    public CSteamID currentLobbyId;
    private bool isHost = false;
    private bool isMatchmaking = false;
    private bool isManualJoinRandom = false; // 标记是否是手动调用 JoinRandomRoom

    // 本端本次房间会话是否已发起过联机
    private bool _matchEntryStarted;

    // 最近一次同步到的房间名称，用于识别房间级数据里的房名变化
    private string _lastRoomName = "";

    /// <summary>
    /// 获取是否为房主
    /// </summary>
    public bool IsHost => isHost;

    // 临时存储创建房间时的参数
    private string roomNameToSet = "";
    private string roomPasswordToSet = "";
    private bool isRoomPrivate = false;

    #region Steam回调

    // Steam回调
    private Callback<LobbyCreated_t> lobbyCreatedCallback;
    private Callback<LobbyEnter_t> lobbyEnterCallback;
    private Callback<LobbyKicked_t> lobbyKickedCallback;
    private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
    private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
    private Callback<LobbyMatchList_t> lobbyMatchListCallback;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;

    /// <summary>
    /// 初始化Steam回调
    /// </summary>
    private void InitializeSteamCallbacks()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[Steam] Steam未初始化！");
            return;
        }

        lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        lobbyKickedCallback = Callback<LobbyKicked_t>.Create(OnLobbyKicked);
        lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        lobbyMatchListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
    }

    #endregion

    // 事件委托
    public Action OnRoomCreated; // 房间创建成功
    public Action OnRoomJoined; // 加入房间成功
    public Action OnRoomLeave; // 离开房间
    public Action OnPlayerJoined; // 玩家加入
    public Action OnPlayerLeft; // 玩家离开
    public Action OnPlayerKicked; // 玩家被踢出
    public Action OnRoomNameChanged; // 房间名称改变
    public Action<string> OnError; // 错误信息

    private List<CSteamID> availableLobbies = new List<CSteamID>();

    #region 房间列表对外接口 (RoomList)

    // 房间列表更新事件（对外）
    public event Action<List<RoomData>> OnRoomListUpdated
    {
        add => roomList.OnRoomListUpdated += value;
        remove => roomList.OnRoomListUpdated -= value;
    }

    // 房间列表搜索错误事件（对外）
    public event Action<string> OnSearchError
    {
        add => roomList.OnSearchError += value;
        remove => roomList.OnSearchError -= value;
    }

    /// <summary>
    /// 设置房间列表自动刷新是否启用
    /// </summary>
    /// <param name="enabled">是否启用</param>
    public void SetRoomListAutoRefresh(bool enabled)
    {
        roomList.SetAutoRefreshEnabled(enabled);
    }

    /// <summary>
    /// 强制立即刷新房间列表（忽略时间间隔）
    /// </summary>
    public void ForceRefreshRoomList()
    {
        roomList.ForceRefresh();
    }

    /// <summary>
    /// 按房间名称搜索
    /// </summary>
    /// <param name="searchText">搜索关键词</param>
    /// <returns>匹配的房间列表</returns>
    public List<RoomData> SearchRoomsByName(string searchText)
    {
        return roomList.SearchRoomsByName(searchText);
    }

    /// <summary>
    /// 按条件筛选房间列表
    /// </summary>
    /// <param name="hasPassword">是否有密码的房间</param>
    /// <param name="notFull">是否只显示未满的房间</param>
    /// <param name="minPlayers">最小玩家数</param>
    /// <param name="maxPlayers">最大玩家数</param>
    /// <returns>筛选后的房间列表</returns>
    public List<RoomData> GetFilteredRooms(bool? hasPassword = null, bool notFull = true, int minPlayers = 0,
        int maxPlayers = int.MaxValue)
    {
        return roomList.GetFilteredRooms(hasPassword, notFull, minPlayers, maxPlayers);
    }

    #endregion

    private void Start()
    {
        InitializeSteamCallbacks();
        roomList.Initialize();
    }

    private void Update()
    {
        roomList.Tick();
    }

    /// <summary>
    /// 设置房间最大人数
    /// </summary>
    public void SetMaxPlayersPerRoom(int maxPlayers)
    {
        maxPlayersPerRoom = maxPlayers;
        // 只有在房间中才同步 lobby 数据
        if (currentLobbyId != CSteamID.Nil && SteamManager.Initialized)
        {
            SteamMatchmaking.SetLobbyData(currentLobbyId, "maxPlayers", maxPlayersPerRoom.ToString());
        }
    }

    /// <summary>
    /// 获取房间最大人数
    /// </summary>
    /// <returns>房间最大人数</returns>
    public int GetMaxPlayersPerRoom()
    {
        // 用局部变量接住解析结果，解析失败时保留当前值，避免字段被清零
        if (currentLobbyId != CSteamID.Nil && SteamManager.Initialized)
        {
            if (int.TryParse(SteamMatchmaking.GetLobbyData(currentLobbyId, "maxPlayers"), out int parsed))
            {
                maxPlayersPerRoom = parsed;
            }
        }

        return maxPlayersPerRoom;
    }

    #region 房间状态查询

    /// <summary>
    /// 获取当前房间原始房主的 SteamID 字符串。
    /// </summary>
    /// <returns>建房时写入的房主 SteamID 字符串；不在房间或数据未同步时返回空字符串。</returns>
    public string GetHostSteamId()
    {
        if (currentLobbyId == CSteamID.Nil || !SteamManager.Initialized)
            return string.Empty;

        return SteamMatchmaking.GetLobbyData(currentLobbyId, HOST_ADDRESS_KEY);
    }

    /// <summary>
    /// 判断当前房间是否已被房主标记为开局。
    /// </summary>
    /// <returns>房间已开局返回真。</returns>
    public bool IsLobbyStarted()
    {
        if (currentLobbyId == CSteamID.Nil || !SteamManager.Initialized)
            return false;

        return SteamMatchmaking.GetLobbyData(currentLobbyId, STARTED_KEY) == "1";
    }

    /// <summary>
    /// 按房主置顶、其余按 SteamID 升序返回房间内的成员，供席位列表稳定显示。
    /// </summary>
    /// <returns>排序后的房间成员列表；不在房间时返回空列表。</returns>
    public List<CSteamID> GetRoomMembers()
    {
        List<CSteamID> members = new List<CSteamID>();
        if (currentLobbyId == CSteamID.Nil || !SteamManager.Initialized)
            return members;

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
        for (int i = 0; i < memberCount; i++)
        {
            members.Add(SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, i));
        }

        // 席位号只作显示，用稳定排序避免成员进出导致位置漂移
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
        members.Sort((left, right) =>
        {
            if (left == ownerId) return -1;
            if (right == ownerId) return 1;
            return left.m_SteamID.CompareTo(right.m_SteamID);
        });

        return members;
    }

    #endregion

    /// <summary>
    /// 创建房间
    /// </summary>
    /// <param name="roomName">房间名称</param>
    /// <param name="password">房间密码，为空则无密码</param>
    /// <param name="isPrivate">是否为私人房间</param>
    public void CreateRoom(string roomName, string password = "", bool isPrivate = false)
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam is not initialized");
            return;
        }

        if (!SteamUser.BLoggedOn())
        {
            Debug.LogError("[Steam] Steam用户未登录！");
            return;
        }

        LeaveRoom();
        // 保存房间名称和密码用于回调
        this.roomNameToSet = roomName;
        this.roomPasswordToSet = password;
        this.isRoomPrivate = isPrivate;

        ELobbyType lobbyType = isPrivate ? ELobbyType.k_ELobbyTypePrivate : ELobbyType.k_ELobbyTypePublic;
        SteamMatchmaking.CreateLobby(lobbyType, maxPlayersPerRoom);
        Debug.Log($"[Steam] 正在创建房间: {roomName}");
    }

    /// <summary>
    /// 加入指定房间
    /// </summary>
    /// <param name="lobbyId">房间ID</param>
    /// <param name="password">房间密码</param>
    public void JoinRoom(CSteamID lobbyId, string password = "")
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam is not initialized");
            return;
        }

        // 已经在同一个房间中时无需重复加入
        if (currentLobbyId != CSteamID.Nil && currentLobbyId == lobbyId)
        {
            Debug.Log("[Steam] 已经在目标房间中，检查UI状态");
            // 如果已经在房间中但UI没有打开，尝试打开房间界面
            if (UIManager.Instance != null)
            {
                UIManager.Instance.OpenPanel<UIRoomPanel>();
            }

            return;
        }

        // 如果房间有密码，先验证密码
        string roomPassword = SteamMatchmaking.GetLobbyData(lobbyId, "password");
        if (!string.IsNullOrEmpty(roomPassword) && roomPassword != password)
        {
            OnError?.Invoke("Incorrect room password");
            return;
        }

        SteamMatchmaking.JoinLobby(lobbyId);
        Debug.Log($"[Steam] 正在加入房间: {lobbyId}");
    }

    /// <summary>
    /// 随机加入房间，如果没有可用房间则创建新房间
    /// </summary>
    public void JoinRandomRoom()
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam is not initialized");
            return;
        }

        // 设置手动调用标记
        isManualJoinRandom = true;

        // 搜索可用的公共房间，过滤条件必须与创建房间时写入的 lobby 数据 key 一致
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
        SteamMatchmaking.AddRequestLobbyListStringFilter("gameName", "pb", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();

        Debug.Log($"[Steam] 正在搜索可用房间... (游戏版本: {Application.version})");
    }

    /// <summary>
    /// 房主调起 Steam 覆盖层邀请对话框，由 Steam 客户端向好友发送当前房间的邀请。
    /// </summary>
    public void OpenInviteDialog()
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam is not initialized");
            return;
        }

        if (currentLobbyId == CSteamID.Nil)
        {
            OnError?.Invoke("Cannot invite friends: not in a room");
            return;
        }

        if (!isHost)
        {
            OnError?.Invoke("Only the host can invite friends");
            return;
        }

        if (!SteamUtils.IsOverlayEnabled())
        {
            OnError?.Invoke("Cannot invite friends: Steam Overlay is disabled");
            return;
        }

        // 开局时房主已把房间置为不可加入，此时发出的邀请即使被接受也进不来
        if (IsLobbyStarted())
        {
            OnError?.Invoke("Cannot invite friends: the game has already started");
            return;
        }

        if (GetRoomPlayerCount() >= GetMaxPlayersPerRoom())
        {
            OnError?.Invoke("Cannot invite friends: the room is full");
            return;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(currentLobbyId);
        Debug.Log($"[Steam] 已调起邀请好友对话框: {currentLobbyId}");
    }

    /// <summary>
    /// 设置房间密码（仅房主可用）
    /// </summary>
    /// <param name="password">新密码，为空则移除密码</param>
    public void SetRoomPassword(string password)
    {
        if (!isHost || currentLobbyId == CSteamID.Nil)
        {
            OnError?.Invoke("Only the host can set the room password");
            return;
        }

        SteamMatchmaking.SetLobbyData(currentLobbyId, "password", password);
        Debug.Log("[Steam] 房间密码已更新");
    }

    /// <summary>
    /// 修改房间名称（仅房主可用）
    /// </summary>
    /// <param name="newName">新房间名称</param>
    public void ChangeRoomName(string newName)
    {
        if (!isHost || currentLobbyId == CSteamID.Nil)
        {
            OnError?.Invoke("Only the host can change the room name");
            return;
        }

        SteamMatchmaking.SetLobbyData(currentLobbyId, "name", newName);
        // 本地先落缓存，房主自身收到的房间数据回调不再重复触发一次
        _lastRoomName = newName;
        OnRoomNameChanged?.Invoke();
        Debug.Log($"[Steam] 房间名称已更改为: {newName}");
    }

    /// <summary>
    /// 开始匹配（创建隐形房间）
    /// </summary>
    public void StartMatchmaking()
    {
        if (!SteamManager.Initialized)
        {
            OnError?.Invoke("Steam is not initialized");
            return;
        }

        isMatchmaking = true;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeInvisible, maxPlayersPerRoom);
        Debug.Log("[Steam] 开始匹配，创建匹配房间...");
    }

    /// <summary>
    /// 离开当前房间
    /// </summary>
    public void LeaveRoom()
    {
        if (currentLobbyId != CSteamID.Nil)
        {
            if (SteamManager.Initialized)
                SteamMatchmaking.LeaveLobby(currentLobbyId);

            currentLobbyId = CSteamID.Nil;
            isMatchmaking = false;
            isHost = false;
            _matchEntryStarted = false;
            _lastRoomName = "";

            // 延迟一帧触发事件，避免在 Steam 回调栈内同步销毁面板；
            // 退出流程中计时器可能尚未创建，取不到时直接触发
            TimerManager timerManager = TimerManager.GetInstance();
            if (timerManager != null)
                timerManager.AddListener(0.1f, () => { OnRoomLeave?.Invoke(); });
            else
                OnRoomLeave?.Invoke();
        }
    }

    /// <summary>
    /// 房主开始游戏：建立主机联机，并把房间标记为已开局、关闭加入入口。
    /// </summary>
    public void StartGame()
    {
        if (!isHost || currentLobbyId == CSteamID.Nil)
        {
            OnError?.Invoke("Only the host can start the game");
            return;
        }

        if (!SteamManager.Initialized)
            return;

        CSteamID lobbyId = currentLobbyId;

        EnterMatch(() =>
        {
            NetworkManager networkManager = NetworkManager.singleton;
            if (networkManager == null)
            {
                Debug.LogError("[Steam] 场景中不存在 NetworkManager，无法建立主机");
                return;
            }

            networkManager.StartHost();

            // 房主先落标记，成员读到标记后各自连入；同时关闭加入入口
            SteamMatchmaking.SetLobbyData(lobbyId, STARTED_KEY, "1");
            SteamMatchmaking.SetLobbyJoinable(lobbyId, false);

            // 生成循环由主机驱动，成员加入路径不会走到这里
            BallSpawner.GetInstance()?.Begin();

            Debug.Log("[Steam] 房主开始游戏，房间已关闭加入");
        });
    }

    /// <summary>
    /// 房主关闭房间：先写入解散标记再退出，成员读到标记后各自退房。
    /// </summary>
    public void CloseRoom()
    {
        if (currentLobbyId == CSteamID.Nil)
            return;

        // Steam 在房主离开时只会转移房间所有权，不会解散房间，解散必须显式标记
        if (isHost && SteamManager.Initialized)
            SteamMatchmaking.SetLobbyData(currentLobbyId, CLOSED_KEY, "1");

        LeaveRoom();
    }

    /// <summary>
    /// 遮罩完全变黑后执行联机启动，随后清空大厅界面、揭示遮罩并打开对局界面。
    /// </summary>
    /// <param name="startAction">遮罩全黑后要执行的联机启动动作。</param>
    private void EnterMatch(Action startAction)
    {
        if (_matchEntryStarted)
            return;

        _matchEntryStarted = true;

        // 遮罩的打开动画回调在 alpha 到达 1 时触发，此处以它为起点，
        // 保证联机与界面切换都发生在屏幕已完全变黑之后
        UIManager.Instance.OpenPanel<UIMaskPanel>(true, () =>
        {
            startAction?.Invoke();

            // 房间列表与房间面板等常驻面板在遮罩之下统一清除，杜绝残留到对局
            UIManager.Instance.CloseAllNormalPanel(false);
            UIManager.Instance.GetPanel<UIMaskPanel>()?.RequestReveal();
            UIManager.Instance.OpenPanel<UIGamePanel>(false);
        });
    }

    /// <summary>
    /// 成员收到开局标记后连入房主所在主机。
    /// </summary>
    private void JoinMatchAsClient()
    {
        if (_matchEntryStarted)
            return;

        string hostAddress = GetHostSteamId();
        if (string.IsNullOrEmpty(hostAddress))
        {
            Debug.LogError("[Steam] 房主地址为空，无法连入对局");
            OnError?.Invoke("Cannot get the host address");
            return;
        }

        EnterMatch(() =>
        {
            NetworkManager networkManager = NetworkManager.singleton;
            if (networkManager == null)
            {
                Debug.LogError("[Steam] 场景中不存在 NetworkManager，无法连入对局");
                return;
            }

            // 监听组件先补挂、订阅后置：StartClient 内部会整体重写 NetworkClient 的静态事件，
            // 排在它之前订阅会被直接覆盖掉
            MatchDisconnectWatcher watcher = MatchDisconnectWatcher.EnsureOn(networkManager);
            networkManager.networkAddress = hostAddress;
            networkManager.StartClient();
            watcher?.Subscribe();

            StartConnectTimeout();
        });
    }

    /// <summary>
    /// 启动连接超时检测：超时仍未连上房主时退出房间并提示。
    /// </summary>
    private void StartConnectTimeout()
    {
        // 计时器在首次访问时自建并常驻，取用创建式入口保证超时检测一定被安排
        TimerManager timerManager = TimerManager.Instance;
        if (timerManager == null)
            return;

        timerManager.AddListener(CONNECT_TIMEOUT_SECONDS, () =>
        {
            if (NetworkClient.isConnected)
                return;

            Debug.LogWarning("[Steam] 连接房主超时，退出房间");
            OnError?.Invoke("Connection to the host timed out");
            LeaveRoom();
        });
    }

    /// <summary>
    /// 检查玩家是否在房间中
    /// </summary>
    /// <returns>是否在房间中</returns>
    public bool IsInRoom()
    {
        return currentLobbyId != CSteamID.Nil;
    }

    /// <summary>
    /// 获取当前房间信息
    /// </summary>
    /// <returns>当前房间信息字符串</returns>
    public string GetCurrentRoomInfo()
    {
        if (currentLobbyId == CSteamID.Nil)
            return "未在房间中";

        string roomName = SteamMatchmaking.GetLobbyData(currentLobbyId, "name");
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
        bool hasPassword = !string.IsNullOrEmpty(SteamMatchmaking.GetLobbyData(currentLobbyId, "password"));

        return
            $"房间: {roomName} | 人数: {memberCount}/{maxPlayersPerRoom} | 密码: {(hasPassword ? "有" : "无")} | 房主: {(isHost ? "是" : "否")}";
    }

    /// <summary>
    /// 判断指定玩家是否为房间的房主
    /// </summary>
    /// <param name="playerSteamId">要检查的玩家Steam ID</param>
    /// <returns>如果该玩家是房主则返回true，否则返回false</returns>
    public bool IsPlayerHost(CSteamID playerSteamId)
    {
        if (currentLobbyId == CSteamID.Nil)
        {
            Debug.LogWarning("[Steam] 未在房间中，无法判断房主");
            return false;
        }

        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(currentLobbyId);
        return ownerId == playerSteamId;
    }

    /// <summary>
    /// 获取房间内所有玩家
    /// </summary>
    /// <returns>玩家Steam ID列表</returns>
    public List<CSteamID> GetRoomPlayers()
    {
        List<CSteamID> players = new List<CSteamID>();

        if (currentLobbyId != CSteamID.Nil)
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
            for (int i = 0; i < memberCount; i++)
            {
                players.Add(SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyId, i));
            }
        }

        return players;
    }

    /// <summary>
    /// 获取房间内当前人数
    /// </summary>
    /// <returns>房间内人数；不在房间时返回 0。</returns>
    public int GetRoomPlayerCount()
    {
        // 不在房间时 GetNumLobbyMembers 无效，返回 0
        if (currentLobbyId == CSteamID.Nil)
            return 0;
        return SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);
    }

    // Steam回调处理
    /// <summary>
    /// 响应房间创建回调，写入房间基础数据并打开房间界面。
    /// </summary>
    private void OnLobbyCreated(LobbyCreated_t pCallback)
    {
        if (pCallback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[Steam] 创建房间失败: {pCallback.m_eResult}");
            return;
        }

        currentLobbyId = new CSteamID(pCallback.m_ulSteamIDLobby);
        isHost = true;

        // 设置房间基本信息
        string roomName = !string.IsNullOrEmpty(roomNameToSet) ? roomNameToSet : (isMatchmaking ? "匹配房间" : "新房间");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "name", roomName);
        _lastRoomName = roomName;
        SteamMatchmaking.SetLobbyData(currentLobbyId, HOST_ADDRESS_KEY, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(currentLobbyId, "gameName", "pb");
        SteamMatchmaking.SetLobbyData(currentLobbyId, "maxPlayers", maxPlayersPerRoom.ToString());
        SteamMatchmaking.SetLobbyData(currentLobbyId, "lobbyType", isMatchmaking ? "Invisible" : "Public");
        // 写入游戏版本号供快速加入时做版本过滤
        SteamMatchmaking.SetLobbyData(currentLobbyId, "version", Application.version);
        SteamMatchmaking.SetLobbyData(currentLobbyId, STARTED_KEY, "0");
        SteamMatchmaking.SetLobbyJoinable(currentLobbyId, true);

        // 设置房间密码（如果有）
        if (!string.IsNullOrEmpty(roomPasswordToSet))
        {
            SteamMatchmaking.SetLobbyData(currentLobbyId, "password", roomPasswordToSet);
        }

        OnRoomCreated?.Invoke();

        // 重置临时存储的参数
        roomNameToSet = "";
        roomPasswordToSet = "";
        isRoomPrivate = false;

        // 创建房间成功后自动打开房间面板
        if (UIManager.Instance != null)
            UIManager.Instance.OpenPanel<UIRoomPanel>();

        // 房主创建即已在房间中，这里直接补发加入事件
        OnRoomJoined?.Invoke();
    }

    /// <summary>
    /// 响应进入房间回调，同步本地房间状态并打开房间界面。
    /// </summary>
    private void OnLobbyEnter(LobbyEnter_t pCallback)
    {
        // 检查进入结果，失败时不更新本地房间状态
        if (pCallback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"[Steam] 加入房间失败: {(EChatRoomEnterResponse)pCallback.m_EChatRoomEnterResponse}");
            OnError?.Invoke("Failed to join the room");
            return;
        }

        CSteamID lobbyId = new CSteamID(pCallback.m_ulSteamIDLobby);

        // 检查是否是新房间加入
        bool isNewRoom = currentLobbyId == CSteamID.Nil || currentLobbyId != lobbyId;
        // 如果是新房间加入，触发事件并打开UI
        if (!isNewRoom)
        {
            return;
        }

        currentLobbyId = lobbyId;
        isHost = SteamMatchmaking.GetLobbyOwner(currentLobbyId) == SteamUser.GetSteamID();
        _lastRoomName = SteamMatchmaking.GetLobbyData(currentLobbyId, "name");

        // 已开局的房间不再接纳新成员，命中说明是邀请或竞态绕过了加入限制
        if (!isHost && SteamMatchmaking.GetLobbyData(currentLobbyId, STARTED_KEY) == "1")
        {
            Debug.LogWarning("[Steam] 目标房间已开局，退出该房间");
            OnError?.Invoke("The game has already started in this room");
            LeaveRoom();
            return;
        }

        OnRoomJoined?.Invoke();
        string roomName = SteamMatchmaking.GetLobbyData(currentLobbyId, "name");
        Debug.Log($"[Steam] 加入房间成功: {roomName} (房主: {isHost})");

        // 自动打开房间界面（如果不是房主，因为房主创建时已经打开过了）
        if (!isHost && UIManager.Instance != null)
        {
            UIManager.Instance.OpenPanel<UIRoomPanel>();
        }
    }

    /// <summary>
    /// 响应被移出房间回调，清理本地房间状态。
    /// </summary>
    private void OnLobbyKicked(LobbyKicked_t pCallback)
    {
        // 被踢/房间解散后 Steam 已让本端离开该 lobby，同步清理本地状态
        Debug.LogWarning("[Steam] 已被移出房间或房间已解散");
        currentLobbyId = CSteamID.Nil;
        isHost = false;
        isMatchmaking = false;
        _lastRoomName = "";
        OnRoomLeave?.Invoke();
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
    {
        // 只处理当前所在房间的成员变动，其他房间的进出不应影响本房事件
        if (pCallback.m_ulSteamIDLobby != currentLobbyId.m_SteamID)
        {
            return;
        }

        CSteamID playerId = new CSteamID(pCallback.m_ulSteamIDUserChanged);
        string playerName = SteamFriends.GetFriendPersonaName(playerId);

        if ((pCallback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            OnPlayerJoined?.Invoke();
            Debug.Log($"[Steam] 玩家加入: {playerName}");
        }
        else if ((pCallback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
        {
            OnPlayerLeft?.Invoke();
            Debug.Log($"[Steam] 玩家离开: {playerName}");

            // 原始房主离开时 Steam 只会转移房间所有权，此处按房主锚点判定房间已失效
            if (playerId.m_SteamID.ToString() == GetHostSteamId())
            {
                Debug.LogWarning("[Steam] 房主已离开房间，退出该房间");
                OnError?.Invoke("The host has left the room");
                LeaveRoom();
                return;
            }
        }
        else if ((pCallback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeKicked) !=
                 0)
        {
            // 玩家被踢出
            OnPlayerKicked?.Invoke();
            Debug.Log($"[Steam] 玩家被踢出: {playerName}");
        }
    }

    /// <summary>
    /// 响应房间数据更新回调，同步房名变化并处理解散与开局标记。
    /// </summary>
    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        CSteamID lobbyId = new CSteamID(pCallback.m_ulSteamIDLobby);

        // 只处理当前房间的数据更新
        if (lobbyId != currentLobbyId)
        {
            return;
        }

        // 成员自身的数据变更与房名无关，只有房间级数据变更才需要比对
        if (pCallback.m_ulSteamIDMember == pCallback.m_ulSteamIDLobby)
        {
            string roomName = SteamMatchmaking.GetLobbyData(currentLobbyId, "name");
            if (roomName != _lastRoomName)
            {
                _lastRoomName = roomName;
                OnRoomNameChanged?.Invoke();
            }
        }

        // 房主解散后房间不会自动消失，成员读到解散标记时自行退出
        if (!isHost && SteamMatchmaking.GetLobbyData(currentLobbyId, CLOSED_KEY) == "1")
        {
            Debug.Log("[Steam] 房间已被房主关闭，退出该房间");
            LeaveRoom();
            return;
        }

        // 房主开局后成员据此建立联机
        if (!isHost && IsLobbyStarted())
        {
            JoinMatchAsClient();
        }
    }


    private void OnLobbyMatchList(LobbyMatchList_t pCallback)
    {
        // 如果玩家已经在房间中，跳过房间列表处理
        if (currentLobbyId != CSteamID.Nil)
        {
            Debug.Log("[Steam] 玩家已经在房间中，跳过房间列表处理");
            return;
        }

        // 只处理手动调用 JoinRandomRoom 的情况
        if (!isManualJoinRandom)
        {
            return;
        }

        availableLobbies.Clear();
        Debug.Log($"[Steam] 快速加入搜索到 {pCallback.m_nLobbiesMatching} 个匹配房间");

        for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);

            try
            {
                // 验证房间版本
                string roomVersion = SteamMatchmaking.GetLobbyData(lobbyId, "version");
                if (roomVersion != Application.version)
                {
                    Debug.LogWarning($"[Steam] 跳过版本不匹配的房间: {roomVersion} (需要: {Application.version})");
                    continue;
                }

                // 检查房间是否有空位且无密码
                int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
                string password = SteamMatchmaking.GetLobbyData(lobbyId, "password");
                string lobbyType = SteamMatchmaking.GetLobbyData(lobbyId, "lobbyType");

                // 只加入公共房间、未满且无密码的房间
                if (lobbyType != "Private" && lobbyType != "Invisible" &&
                    memberCount < maxPlayersPerRoom && string.IsNullOrEmpty(password))
                {
                    availableLobbies.Add(lobbyId);
                    Debug.Log(
                        $"找到可用房间: {SteamMatchmaking.GetLobbyData(lobbyId, "name")} ({memberCount}/{maxPlayersPerRoom})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Steam] 验证房间 {lobbyId} 时出错: {e.Message}");
            }
        }

        Debug.Log($"[Steam] 快速加入找到 {availableLobbies.Count} 个可用房间");

        if (availableLobbies.Count > 0)
        {
            // 随机选择一个可用房间加入
            int randomIndex = UnityEngine.Random.Range(0, availableLobbies.Count);
            CSteamID selectedLobby = availableLobbies[randomIndex];
            string selectedRoomName = SteamMatchmaking.GetLobbyData(selectedLobby, "name");
            Debug.Log($"[Steam] 随机选择房间: {selectedRoomName}");
            JoinRoom(selectedLobby);
        }
        else
        {
            // 没有可用房间，创建新房间（使用玩家名称作为默认房间名）
            string playerName = SteamFriends.GetPersonaName();
            string roomName = $"{playerName}的房间";
            Debug.Log("[Steam] 没有可用房间，创建新房间");
            CreateRoom(roomName);
        }

        // 重置手动调用标记
        isManualJoinRandom = false;
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t pCallback)
    {
        // 通过邀请加入房间
        JoinRoom(pCallback.m_steamIDLobby);
    }

    /// <summary>
    /// 销毁时释放全部 Steam 回调并清理房间状态。
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();

        // 释放全部 Steam 回调，避免对象销毁后重复注册
        lobbyCreatedCallback?.Dispose();
        lobbyCreatedCallback = null;
        lobbyEnterCallback?.Dispose();
        lobbyEnterCallback = null;
        lobbyKickedCallback?.Dispose();
        lobbyKickedCallback = null;
        lobbyChatUpdateCallback?.Dispose();
        lobbyChatUpdateCallback = null;
        lobbyDataUpdateCallback?.Dispose();
        lobbyDataUpdateCallback = null;
        lobbyMatchListCallback?.Dispose();
        lobbyMatchListCallback = null;
        gameLobbyJoinRequestedCallback?.Dispose();
        gameLobbyJoinRequestedCallback = null;

        roomList.Dispose();
        LeaveRoom();
    }

    private void OnApplicationQuit()
    {
        LeaveRoom();
    }
}
