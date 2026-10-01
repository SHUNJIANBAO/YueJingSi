using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

public class UIPasswordPanel : UIPanelBase
{
    // 主要交互组件
    Button Btn_Enter;
    Button Btn_Cancel;
    TMP_InputField Input_Password;
    Button Btn_Close;
    
    // 房间信息显示组件
    TextMeshProUGUI Txt_RoomName;
    TextMeshProUGUI Txt_HostName;
    TextMeshProUGUI Txt_PlayerCount;
    
    // 错误提示组件
    TextMeshProUGUI Txt_Error;
    
    // 加载状态组件
    Transform Panel_Loading;
    
    // 本次要加入的房间 ID
    private CSteamID _currentLobbyId;

    // 本次要加入的房间数据
    private RoomData _currentRoomData;

    // 是否正在发起加入请求
    private bool _isProcessing = false;
    protected override void GetUIComponents()
    {
        // 获取主要UI组件
        Btn_Enter = GetUI<Button>("Btn_Enter");
        Btn_Cancel = GetUI<Button>("Btn_Cancel");
        Input_Password = GetUI<TMP_InputField>("Input_Password");
        
        // 获取房间信息显示组件
        Txt_RoomName = GetUI<TextMeshProUGUI>("Txt_RoomName");
        Txt_HostName = GetUI<TextMeshProUGUI>("Txt_HostName");
        Txt_PlayerCount = GetUI<TextMeshProUGUI>("Txt_PlayerCount");
        
        // 获取错误提示组件
        Txt_Error = GetUI<TextMeshProUGUI>("Txt_Error");
        
        // 获取加载状态组件
        Panel_Loading = GetUI<Transform>("Panel_Loading");
        
        // 获取关闭按钮
        Btn_Close = GetUI<Button>("Btn_Close");
    }
    protected override void AddUIListeners()
    {
        base.AddUIListeners();
        
        // 添加按钮点击事件
        AddButtonListen(Btn_Enter, OnEnterClicked);
        AddButtonListen(Btn_Cancel, OnCancelClicked);
        AddButtonListen(Btn_Close, OnCancelClicked);
        
        // 添加输入框事件
        if (Input_Password != null)
        {
            Input_Password.onSubmit.AddListener(OnPasswordSubmitted);
            Input_Password.onValueChanged.AddListener(OnPasswordValueChanged);
        }
        
        // 注册SteamRoomManager事件
        RegisterSteamRoomManagerEvents();
    }
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();
        
        // 移除按钮点击事件
        RemoveButtonListen(Btn_Enter, OnEnterClicked);
        RemoveButtonListen(Btn_Cancel, OnCancelClicked);
        RemoveButtonListen(Btn_Close, OnCancelClicked);
        
        // 移除输入框事件
        if (Input_Password != null)
        {
            Input_Password.onSubmit.RemoveListener(OnPasswordSubmitted);
            Input_Password.onValueChanged.RemoveListener(OnPasswordValueChanged);
        }
        
        // 注销SteamRoomManager事件
        UnregisterSteamRoomManagerEvents();
    }
    protected override void OnOpen(params object[] args)
    {
        base.OnOpen(args);
        
        // 重置状态，避免上次的房间信息残留导致误加入
        _isProcessing = false;
        _currentRoomData = null;
        _currentLobbyId = CSteamID.Nil;
        
        // 解析参数
        if (args.Length > 0)
        {
            if (args[0] is CSteamID)
            {
                _currentLobbyId = (CSteamID)args[0];
            }
            else if (args[0] is RoomData)
            {
                _currentRoomData = (RoomData)args[0];
                _currentLobbyId = _currentRoomData.lobbyId;
            }
        }
        
        // 初始化UI状态
        InitializeUIState();
        
        // 显示房间信息
        DisplayRoomInfo();
        
        // 清空输入框
        if (Input_Password != null)
        {
            Input_Password.text = "";
            Input_Password.ActivateInputField();
        }
        
        // 隐藏错误提示
        HideError();
        
        // 隐藏加载状态
        HideLoading();
    }
    
    #region 私有方法
    
    /// <summary>
    /// 初始化UI状态
    /// </summary>
    private void InitializeUIState()
    {
        // 设置按钮状态
        if (Btn_Enter != null)
        {
            Btn_Enter.interactable = true;
        }
        
        if (Btn_Cancel != null)
        {
            Btn_Cancel.interactable = true;
        }
        
        if (Input_Password != null)
        {
            Input_Password.interactable = true;
        }
    }
    
    /// <summary>
    /// 显示房间信息
    /// </summary>
    private void DisplayRoomInfo()
    {
        if (_currentRoomData != null)
        {
            if (Txt_RoomName != null)
            {
                Txt_RoomName.text = _currentRoomData.roomName;
            }
            
            if (Txt_HostName != null)
            {
                Txt_HostName.text = _currentRoomData.hostName;
            }
            
            if (Txt_PlayerCount != null)
            {
                Txt_PlayerCount.text = $"{_currentRoomData.currentPlayers}/{_currentRoomData.maxPlayers}";
            }
        }
        else
        {
            // 如果没有房间数据，尝试从SteamRoomManager获取
            TryGetRoomInfoFromManager();
        }
    }
    
    /// <summary>
    /// 尝试从SteamRoomManager获取房间信息
    /// </summary>
    private void TryGetRoomInfoFromManager()
    {
        if (SteamRoomManager.Instance != null && _currentLobbyId != CSteamID.Nil)
        {
            // 这里可以根据需要添加获取房间信息的逻辑
            // 例如：SteamRoomManager.Instance.GetRoomInfo(_currentLobbyId);
            if (Txt_RoomName != null)
            {
                Txt_RoomName.text = "密码保护房间";
            }
            
            if (Txt_PlayerCount != null)
            {
                Txt_PlayerCount.text = "玩家数量未知";
            }
        }
    }
    
    /// <summary>
    /// 显示错误信息
    /// </summary>
    private void ShowError(string errorMessage)
    {
        if (Txt_Error != null)
        {
            Txt_Error.text = errorMessage;
            Txt_Error.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// 隐藏错误信息
    /// </summary>
    private void HideError()
    {
        if (Txt_Error != null)
        {
            Txt_Error.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 显示加载状态
    /// </summary>
    private void ShowLoading()
    {
        if (Panel_Loading != null)
        {
            Panel_Loading.gameObject.SetActive(true);
        }
        
        // 禁用交互
        SetInteractive(false);
    }
    
    /// <summary>
    /// 隐藏加载状态
    /// </summary>
    private void HideLoading()
    {
        if (Panel_Loading != null)
        {
            Panel_Loading.gameObject.SetActive(false);
        }
        
        // 启用交互
        SetInteractive(true);
    }
    
    /// <summary>
    /// 设置UI交互状态
    /// </summary>
    private void SetInteractive(bool interactive)
    {
        if (Btn_Enter != null)
        {
            Btn_Enter.interactable = interactive;
        }
        
        if (Btn_Cancel != null)
        {
            Btn_Cancel.interactable = interactive;
        }
        
        if (Input_Password != null)
        {
            Input_Password.interactable = interactive;
        }
    }
    
    /// <summary>
    /// 验证密码输入
    /// </summary>
    private bool ValidatePasswordInput()
    {
        if (Input_Password == null || string.IsNullOrWhiteSpace(Input_Password.text))
        {
            ShowError("请输入密码");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 注册SteamRoomManager事件
    /// </summary>
    private void RegisterSteamRoomManagerEvents()
    {
        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.OnRoomJoined += OnRoomJoined;
            // 密码错误、加入失败等错误都要复位加载态，否则面板会一直卡在加载中
            SteamRoomManager.Instance.OnError += OnError;
        }
    }
    
    /// <summary>
    /// 注销SteamRoomManager事件
    /// </summary>
    private void UnregisterSteamRoomManagerEvents()
    {
        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.OnRoomJoined -= OnRoomJoined;
            SteamRoomManager.Instance.OnError -= OnError;
        }
    }
    
    #endregion
    
    #region 事件处理方法
    
    /// <summary>
    /// 确认按钮点击事件
    /// </summary>
    private void OnEnterClicked()
    {
        if (_isProcessing) return;
        
        if (!ValidatePasswordInput())
        {
            return;
        }
        
        TryJoinRoom();
    }
    
    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void OnCancelClicked()
    {
        if (_isProcessing) return;
        
        UIManager.Instance.ClosePanel<UIPasswordPanel>();
    }
    
    /// <summary>
    /// 密码输入框提交事件
    /// </summary>
    /// <param name="password">输入框当前文本。</param>
    private void OnPasswordSubmitted(string password)
    {
        OnEnterClicked();
    }
    
    /// <summary>
    /// 密码输入框内容变化事件
    /// </summary>
    private void OnPasswordValueChanged(string password)
    {
        HideError();
    }
    
    /// <summary>
    /// 尝试加入房间
    /// </summary>
    private void TryJoinRoom()
    {
        if (_isProcessing) return;
        
        _isProcessing = true;
        ShowLoading();
        HideError();
        
        if (SteamRoomManager.Instance != null && _currentLobbyId != CSteamID.Nil)
        {
            // 使用密码加入房间
            string password = Input_Password.text.Trim();
            SteamRoomManager.Instance.JoinRoom(_currentLobbyId, password);
        }
        else
        {
            ShowError("房间信息无效");
            HideLoading();
            _isProcessing = false;
        }
    }
    
    /// <summary>
    /// 房间加入成功事件
    /// </summary>
    private void OnRoomJoined()
    {
        HideLoading();
        _isProcessing = false;
        
        // 关闭密码面板
        UIManager.Instance.ClosePanel<UIPasswordPanel>();
    }
    
    
    /// <summary>
    /// 错误事件处理
    /// </summary>
    private void OnError(string errorMessage)
    {
        HideLoading();
        _isProcessing = false;
        
        ShowError(errorMessage);
        
        // 重新激活输入框
        if (Input_Password != null)
        {
            Input_Password.ActivateInputField();
        }
    }
    
    #endregion
}