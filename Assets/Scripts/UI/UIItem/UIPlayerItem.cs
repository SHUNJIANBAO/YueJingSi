using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 房间玩家列表中的单个席位项，区分真人玩家与空位展示。
/// </summary>
public class UIPlayerItem : UIItemBase
{
    // 玩家名文本
    private Text Text_PlayerName;


    // 当前项绑定的统一席位数据
    private RoomSeatData _seatData;

    /// <summary>
    /// 当前项绑定的统一席位数据。
    /// </summary>
    public RoomSeatData SeatData => _seatData;

    /// <summary>
    /// 刷新该项显示的席位数据，由 UIRoomPanel 下发 RoomSeatData 驱动。
    /// </summary>
    /// <param name="data">统一席位数据对象（RoomSeatData）。</param>
    protected override void SetData(object data)
    {
        EnsureUIComponents();

        if (data is RoomSeatData seat)
        {
            _seatData = seat;
            RenderSeatData(seat);
        }
        else
        {
            Debug.LogWarning($"[UIPlayerItem] 收到未知数据类型: {data?.GetType().Name ?? "null"}，重置为空位展示");
            _seatData = null;
            ResetToEmptySeat();
        }
    }

    /// <summary>
    /// 根据统一席位模型分流渲染真人与空位。
    /// </summary>
    /// <param name="seat">统一席位数据。</param>
    private void RenderSeatData(RoomSeatData seat)
    {
        switch (seat.SeatType)
        {
            case E_SeatType.Human:
                if (Text_PlayerName != null) Text_PlayerName.text = seat.PlayerName;
                break;
            case E_SeatType.Empty:
            default:
                if (Text_PlayerName != null) Text_PlayerName.text = "";
                break;
        }
    }

    /// <summary>
    /// 数据异常或未绑定数据时重置为空位状态。
    /// </summary>
    private void ResetToEmptySeat()
    {
        if (Text_PlayerName != null) Text_PlayerName.text = "";
    }

    /// <summary>
    /// 获取并缓存项内各 UI 组件引用。
    /// </summary>
    protected override void GetUIComponents()
    {
        base.GetUIComponents();
        EnsureUIComponents();
    }

    /// <summary>
    /// 确保 UI 组件已被获取并缓存。
    /// </summary>
    private void EnsureUIComponents()
    {
        if (Text_PlayerName == null) Text_PlayerName = GetUI<Text>("Text_PlayerName");
    }
}
