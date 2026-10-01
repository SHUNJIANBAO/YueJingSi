using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

/// <summary>
/// 房间列表管理器，专门处理房间列表的获取、筛选和管理。
/// 普通 C# 类，不继承 MonoBehaviour，由 SteamRoomManager 创建并管理其生命周期。
/// 对外的接口由 SteamRoomManager 提供。
/// </summary>
public class RoomList
{
    private int maxRoomListCount = 50;
    private float refreshInterval = 5f; // 自动刷新间隔

    private List<RoomData> availableRooms = new List<RoomData>();
    private Callback<LobbyMatchList_t> lobbyMatchListCallback;
    private bool isSearching = false;
    private float lastRefreshTime = 0f;
    private bool autoRefreshEnabled = true; // 控制自动刷新是否启用

    // 事件委托
    public Action<List<RoomData>> OnRoomListUpdated; // 房间列表更新
    public Action OnSearchStarted; // 开始搜索
    public Action OnSearchCompleted; // 搜索完成
    public Action<string> OnSearchError; // 搜索错误

    /// <summary>
    /// 最大房间列表数量
    /// </summary>
    public int MaxRoomListCount
    {
        get => maxRoomListCount;
        set => maxRoomListCount = value;
    }

    /// <summary>
    /// 自动刷新间隔（秒）
    /// </summary>
    public float RefreshInterval
    {
        get => refreshInterval;
        set => refreshInterval = value;
    }

    /// <summary>
    /// 初始化Steam回调
    /// </summary>
    public void Initialize()
    {
        if (SteamManager.Initialized)
        {
            lobbyMatchListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }
    }

    /// <summary>
    /// 每帧由 SteamRoomManager 调用，处理自动刷新计时
    /// </summary>
    public void Tick()
    {
        // 只在启用自动刷新时进行计时刷新
        if (autoRefreshEnabled && Time.time - lastRefreshTime > refreshInterval)
        {
            RefreshRoomList();
        }
    }

    /// <summary>
    /// 释放Steam回调（由 SteamRoomManager 销毁时调用）
    /// </summary>
    public void Dispose()
    {
        if (lobbyMatchListCallback != null)
        {
            lobbyMatchListCallback.Dispose();
            lobbyMatchListCallback = null;
        }
    }

    /// <summary>
    /// 刷新房间列表
    /// </summary>
    private void RefreshRoomList()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[Steam] Steam未初始化");
            return;
        }

        if (SteamRoomManager.Instance.currentLobbyId != CSteamID.Nil)
        {
            return;
        }

        if (isSearching)
        {
            return;
        }

        isSearching = true;
        lastRefreshTime = Time.time;

        // 设置搜索条件
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(maxRoomListCount);
        SteamMatchmaking.AddRequestLobbyListStringFilter("gameName", "pb",
            ELobbyComparison.k_ELobbyComparisonEqual);

        // 开始搜索
        SteamMatchmaking.RequestLobbyList();
        OnSearchStarted?.Invoke();
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
        List<RoomData> filteredRooms = new List<RoomData>();

        foreach (RoomData room in availableRooms)
        {
            // 密码筛选
            if (hasPassword.HasValue && room.hasPassword != hasPassword.Value)
                continue;

            // 房间是否已满
            if (notFull && room.IsFull)
                continue;

            // 玩家数量筛选
            if (room.currentPlayers < minPlayers || room.currentPlayers > maxPlayers)
                continue;

            // 私人房间筛选（不显示私人房间）
            if (room.isPrivate)
                continue;

            // 已开局的房间不再接受加入
            if (room.started)
                continue;

            filteredRooms.Add(room);
        }

        return filteredRooms;
    }

    /// <summary>
    /// 按房间名称搜索
    /// </summary>
    /// <param name="searchText">搜索关键词</param>
    /// <returns>匹配的房间列表</returns>
    public List<RoomData> SearchRoomsByName(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return availableRooms;

        List<RoomData> searchResults = new List<RoomData>();
        string lowerSearchText = searchText.ToLower();

        foreach (RoomData room in availableRooms)
        {
            if (room.roomName.ToLower().Contains(lowerSearchText) ||
                room.hostName.ToLower().Contains(lowerSearchText))
            {
                searchResults.Add(room);
            }
        }

        return searchResults;
    }

    /// <summary>
    /// 获取推荐房间（有空位且无密码的房间）
    /// </summary>
    /// <param name="count">推荐数量</param>
    /// <returns>推荐房间列表</returns>
    public List<RoomData> GetRecommendedRooms(int count = 5)
    {
        List<RoomData> recommendedRooms = GetFilteredRooms(hasPassword: false, notFull: true);

        // 按玩家数量排序，优先推荐人数较多但未满的房间
        recommendedRooms.Sort((a, b) => b.currentPlayers.CompareTo(a.currentPlayers));

        if (recommendedRooms.Count > count)
        {
            recommendedRooms = recommendedRooms.GetRange(0, count);
        }

        return recommendedRooms;
    }

    /// <summary>
    /// 获取指定房间的详细信息
    /// </summary>
    /// <param name="lobbyId">房间ID</param>
    /// <returns>房间详细信息</returns>
    public RoomData GetRoomDetails(CSteamID lobbyId)
    {
        foreach (RoomData room in availableRooms)
        {
            if (room.lobbyId == lobbyId)
            {
                return room;
            }
        }

        // 如果列表中没有，尝试直接从Steam获取
        return new RoomData(lobbyId);
    }

    /// <summary>
    /// 检查房间是否仍然存在
    /// </summary>
    /// <param name="lobbyId">房间ID</param>
    /// <returns>房间是否存在</returns>
    public bool IsRoomExists(CSteamID lobbyId)
    {
        foreach (RoomData room in availableRooms)
        {
            if (room.lobbyId == lobbyId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 设置刷新间隔
    /// </summary>
    /// <param name="interval">刷新间隔（秒）</param>
    public void SetRefreshInterval(float interval)
    {
        refreshInterval = interval;
    }

    /// <summary>
    /// Steam回调：房间列表搜索结果
    /// </summary>
    private void OnLobbyMatchList(LobbyMatchList_t pCallback)
    {
        isSearching = false;
        availableRooms.Clear();


        for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);

            try
            {
                RoomData roomData = new RoomData(lobbyId);
                availableRooms.Add(roomData);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Steam] 解析房间数据失败: {e.Message}");
            }
        }

        OnRoomListUpdated?.Invoke(availableRooms);
        OnSearchCompleted?.Invoke();
    }

    /// <summary>
    /// 设置自动刷新是否启用
    /// </summary>
    /// <param name="enabled">是否启用自动刷新</param>
    public void SetAutoRefreshEnabled(bool enabled)
    {
        if (autoRefreshEnabled != enabled)
        {
            autoRefreshEnabled = enabled;
            // 如果重新启用自动刷新，立即刷新一次
            if (enabled && !isSearching)
            {
                ForceRefresh();
            }
        }
    }

    /// <summary>
    /// 强制立即刷新房间列表（忽略时间间隔）
    /// </summary>
    public void ForceRefresh()
    {
        if (!isSearching)
        {
            lastRefreshTime = Time.time - refreshInterval; // 确保立即触发刷新
            RefreshRoomList();
        }
    }

    /// <summary>
    /// 获取当前自动刷新状态
    /// </summary>
    /// <returns>是否启用自动刷新</returns>
    public bool IsAutoRefreshEnabled()
    {
        return autoRefreshEnabled;
    }
}
