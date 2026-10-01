using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 房间列表中的单个房间项，展示房间名、房主、人数与密码标记。
/// </summary>
public class UIRoomItem : UIItemBase
{
    // 房间名文本
    TextMeshProUGUI Text_Name;

    // 房主名文本
    TextMeshProUGUI Text_HostName;

    // 人数文本
    TextMeshProUGUI Text_Count;

    // 密码标记节点
    RectTransform Image_Password;

    // 当前项对应的房间数据
    RoomData _data;

    // 项自身的按钮组件
    Button _btn;

    /// <summary>
    /// 刷新该项显示的房间数据。
    /// </summary>
    /// <param name="data">房间数据，为空时忽略本次刷新。</param>
    protected override void SetData(object data)
    {
        _data = data as RoomData;
        if (_data == null)
        {
            Debug.LogWarning("[UIRoomItem] 房间数据为空，跳过刷新");
            return;
        }

        Text_Name.text = _data.roomName;
        Text_HostName.text = _data.hostName;
        Text_Count.text = $"{_data.currentPlayers}/{_data.maxPlayers}";
        Image_Password.gameObject.SetActive(_data.hasPassword);
    }

    /// <summary>
    /// 获取并缓存项内各 UI 组件引用。
    /// </summary>
    protected override void GetUIComponents()
    {
        base.GetUIComponents();
        Text_Name = GetUI<TextMeshProUGUI>("Text_Name");
        Text_HostName = GetUI<TextMeshProUGUI>("Text_HostName");
        Text_Count = GetUI<TextMeshProUGUI>("Text_Count");
        Image_Password = GetUI<RectTransform>("Image_Password");
        _btn = GetComponent<Button>();
        if (_btn == null)
        {
            Debug.LogError($"[UIRoomItem] 缺少 Button 组件，节点名:{name}");
        }
    }

    /// <summary>
    /// 绑定项自身的点击事件。
    /// </summary>
    protected override void AddUIListeners()
    {
        base.AddUIListeners();
        if (_btn == null) return;
        _btn.onClick.AddListener(OnClick);
    }

    /// <summary>
    /// 解绑点击事件，避免对象池复用后回调叠加触发多次加入。
    /// </summary>
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();
        if (_btn == null) return;
        _btn.onClick.RemoveListener(OnClick);
    }

    /// <summary>
    /// 点击房间项：有密码时打开密码面板，无密码时直接加入房间。
    /// </summary>
    void OnClick()
    {
        if (_data == null)
        {
            Debug.LogWarning("[UIRoomItem] 房间数据为空，忽略本次点击");
            return;
        }

        if (_data.hasPassword)
        {
            UIManager.Instance.OpenPanel<UIPasswordPanel>(true, null, _data.lobbyId);
        }
        else
        {
            SteamRoomManager.Instance.JoinRoom(_data.lobbyId);
        }
    }
}
