using UnityEngine;

/// <summary>
/// 房间席位的占用类型，区分真人、AI 与空位。
/// </summary>
public enum E_SeatType
{
    [InspectorName("空位")] Empty = 0,
    [InspectorName("真人")] Human = 1,
}
