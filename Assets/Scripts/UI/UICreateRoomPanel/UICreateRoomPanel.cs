using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using TMPro;

public class UICreateRoomPanel : UIPanelBase
{
    // UI控件引用
    private TMP_InputField Input_RoomName;
    private TMP_InputField Input_Password;
    private Button Btn_Create;
    private Button Btn_Cancel;
    
    // 常量定义
    private const int DEFAULT_MAX_PLAYERS = 6;
    private const int MAX_ROOM_NAME_LENGTH = 20;
    private const int MAX_PASSWORD_LENGTH = 12;
    protected override void GetUIComponents()
    {
        // 获取UI控件引用
        Input_RoomName = GetUI<TMP_InputField>("Input_RoomName");
        Input_Password = GetUI<TMP_InputField>("Input_Password");
        Btn_Create = GetUI<Button>("Btn_Create");
        Btn_Cancel = GetUI<Button>("Btn_Cancel");
    }
    protected override void AddUIListeners()
    {
        base.AddUIListeners();
        
        // 添加按钮点击事件
        AddButtonListen(Btn_Create, OnCreateClicked);
        AddButtonListen(Btn_Cancel, OnCancelClicked);
        
        // 添加输入框内容变化事件
        if (Input_RoomName != null)
        {
            Input_RoomName.onValueChanged.AddListener(OnRoomNameChanged);
        }
        if (Input_Password != null)
        {
            Input_Password.onValueChanged.AddListener(OnPasswordChanged);
        }
        
        // 订阅SteamRoomManager事件
        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.OnRoomCreated += OnRoomCreated;
            // 创建失败要复位按钮状态，否则创建按钮会一直不可点
            SteamRoomManager.Instance.OnError += OnRoomError;
        }
    }
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();
        
        // 移除按钮点击事件
        RemoveButtonListen(Btn_Create, OnCreateClicked);
        RemoveButtonListen(Btn_Cancel, OnCancelClicked);
        
        // 移除输入框内容变化事件
        if (Input_RoomName != null)
        {
            Input_RoomName.onValueChanged.RemoveListener(OnRoomNameChanged);
        }
        if (Input_Password != null)
        {
            Input_Password.onValueChanged.RemoveListener(OnPasswordChanged);
        }
        
        // 取消订阅SteamRoomManager事件
        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.OnRoomCreated -= OnRoomCreated;
            SteamRoomManager.Instance.OnError -= OnRoomError;
        }
    }
    protected override void OnOpen(params object[] args)
    {
        base.OnOpen(args);
        
        // 初始化UI状态
        InitializeUIState();
        
        if (Input_RoomName == null)
        {
            Debug.LogError($"[UICreateRoomPanel] 缺少房间名输入框，节点名:{name}");
            return;
        }

        // 如果传递了玩家名称参数，设置为默认房间名称
        if (args != null && args.Length > 0 && args[0] is string playerName)
        {
            Input_RoomName.text = $"{playerName}  Room";
        }
        else
        {
            // 使用Steam用户名作为默认房间名称，Steam未就绪时退回通用名称
            string defaultRoomName = SteamManager.Initialized ? SteamFriends.GetPersonaName() : string.Empty;
            if (string.IsNullOrEmpty(defaultRoomName))
            {
                defaultRoomName = "玩家";
            }
            Input_RoomName.text = $"{defaultRoomName}  Room";
        }

        // 清空密码输入框
        if (Input_Password != null)
        {
            Input_Password.text = "";
        }
        
        // 更新创建按钮状态
        UpdateCreateButtonState();
    }

        
    #region 私有方法
    
    /// <summary>
    /// 初始化UI状态
    /// </summary>
    private void InitializeUIState()
    {
        if (Input_RoomName != null)
        {
            Input_RoomName.characterLimit = MAX_ROOM_NAME_LENGTH;
        }
        if (Input_Password != null)
        {
            Input_Password.characterLimit = MAX_PASSWORD_LENGTH;
        }
        if (Btn_Create != null)
        {
            Btn_Create.interactable = false;
        }
    }
    
    /// <summary>
    /// 更新创建按钮状态
    /// </summary>
    private void UpdateCreateButtonState()
    {
        if (Btn_Create == null) return;
        
        bool canCreate = !string.IsNullOrEmpty(Input_RoomName?.text?.Trim());
        Btn_Create.interactable = canCreate;
    }
    
    /// <summary>
    /// 验证房间名称
    /// </summary>
    private bool ValidateRoomName(string roomName)
    {
        if (string.IsNullOrEmpty(roomName?.Trim()))
        {
            UIHandle.ShowTips("Room name cannot be empty");
            return false;
        }
        
        if (roomName.Length > MAX_ROOM_NAME_LENGTH)
        {
            UIHandle.ShowTips($"Room name cannot exceed {MAX_ROOM_NAME_LENGTH} characters");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 验证密码
    /// </summary>
    private bool ValidatePassword(string password)
    {
        if (!string.IsNullOrEmpty(password) && password.Length > MAX_PASSWORD_LENGTH)
        {
            UIHandle.ShowTips($"Password cannot exceed {MAX_PASSWORD_LENGTH} characters");
            return false;
        }
        
        return true;
    }
    
    #endregion
    
    #region 事件处理方法
    
    /// <summary>
    /// 创建房间按钮点击事件
    /// </summary>
    private void OnCreateClicked()
    {
        string roomName = Input_RoomName?.text?.Trim();
        string password = Input_Password?.text?.Trim();
        
        // 验证输入
        if (!ValidateRoomName(roomName) || !ValidatePassword(password))
        {
            return;
        }
        
        // 禁用创建按钮，防止重复点击
        if (Btn_Create != null)
        {
            Btn_Create.interactable = false;
        }
        
        var roomManager = SteamRoomManager.Instance;
        if (roomManager == null)
        {
            Debug.LogError("[UICreateRoomPanel] 房间管理器未就绪，创建房间中止");
            UIHandle.ShowTips("Room service is not ready");
            return;
        }

        // 设置房间最大人数
        roomManager.SetMaxPlayersPerRoom(DEFAULT_MAX_PLAYERS);

        // 创建房间
        roomManager.CreateRoom(roomName, password);
    }
    
    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void OnCancelClicked()
    {
        // 关闭当前面板
        UIManager.Instance.ClosePanel<UICreateRoomPanel>();
    }
    
    /// <summary>
    /// 房间名称输入变化事件
    /// </summary>
    private void OnRoomNameChanged(string value)
    {
        UpdateCreateButtonState();
    }
    
    /// <summary>
    /// 密码输入变化事件
    /// </summary>
    private void OnPasswordChanged(string value)
    {
        // 密码是可选的，不需要特别处理
    }
    
    /// <summary>
    /// 房间创建成功事件
    /// </summary>
    private void OnRoomCreated()
    {
        // 关闭创建房间面板
        UIManager.Instance.ClosePanel<UICreateRoomPanel>();
    }
    
    /// <summary>
    /// 房间创建错误事件
    /// </summary>
    private void OnRoomError(string errorMessage)
    {
        Debug.LogError($"[UICreateRoomPanel] 房间创建失败: {errorMessage}");
        
        // 显示错误提示
        UIHandle.ShowTips($"Create failed: {errorMessage}");
        
        // 重新启用创建按钮
        if (Btn_Create != null)
        {
            Btn_Create.interactable = true;
        }
    }
    
    #endregion
}