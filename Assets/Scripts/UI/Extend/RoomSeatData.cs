using System;

/// <summary>
/// 房间统一席位显示数据模型，区分真人与空位。
/// </summary>
public class RoomSeatData
{
    /// <summary>
    /// 席位编号（0–3）。
    /// </summary>
    public int SeatIndex { get; set; }

    /// <summary>
    /// 席位占用类型：真人或空位。
    /// </summary>
    public E_SeatType SeatType { get; set; }

    /// <summary>
    /// 席位成员的显示名称。
    /// </summary>
    public string PlayerName { get; set; }

    /// <summary>
    /// 是否为房主。
    /// </summary>
    public bool IsHost { get; set; }
}
