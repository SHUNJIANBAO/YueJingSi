using System;
using Steamworks;

/// <summary>
/// 房间数据结构
/// </summary>
[Serializable]
public class RoomData
{
    public CSteamID lobbyId;           // 房间ID
    public string roomName;            // 房间名称
    public string hostName;            // 房主名称
    public int currentPlayers;         // 当前玩家数
    public int maxPlayers;             // 最大玩家数
    public bool hasPassword;           // 是否有密码
    public bool isPrivate;             // 是否为私人房间
    public bool started;               // 是否已开局
    public string gameVersion;         // 游戏版本

    public RoomData(CSteamID lobbyId)
    {
        this.lobbyId = lobbyId;
        this.roomName = SteamMatchmaking.GetLobbyData(lobbyId, "name");
        this.hostName = SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyOwner(lobbyId));
        this.currentPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        string maxPlayersStr = SteamMatchmaking.GetLobbyData(lobbyId, "maxPlayers");
        if (!int.TryParse(maxPlayersStr, out this.maxPlayers))
        {
            this.maxPlayers = 4; // 默认值
        }
        this.hasPassword = !string.IsNullOrEmpty(SteamMatchmaking.GetLobbyData(lobbyId, "password"));
        this.gameVersion = SteamMatchmaking.GetLobbyData(lobbyId, "version");

        // 通过lobby数据判断是否为私人房间
        string lobbyTypeStr = SteamMatchmaking.GetLobbyData(lobbyId, "lobbyType");
        this.isPrivate = lobbyTypeStr == "Private" || lobbyTypeStr == "FriendsOnly";

        this.started = SteamMatchmaking.GetLobbyData(lobbyId, "started") == "1";
    }

    /// <summary>
    /// 检查房间是否已满
    /// </summary>
    public bool IsFull => currentPlayers >= maxPlayers;

    /// <summary>
    /// 检查是否可以加入房间
    /// </summary>
    public bool CanJoin => !IsFull && !isPrivate && !started;
}
