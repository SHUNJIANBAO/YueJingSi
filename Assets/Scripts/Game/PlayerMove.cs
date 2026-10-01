using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 本地玩家的方向键平面移动、鼠标转向、跟随相机控制与头顶玩家名显示。
/// </summary>
[DisallowMultipleComponent]
public class PlayerMove : NetworkBehaviour
{
    // 移动速度，单位米每秒
    [SerializeField]
    [InspectorName("移动速度")]
    [Tooltip("玩家每秒移动的米数。")]
    private float _moveSpeed = 3f;

    // 鼠标灵敏度，单位角度每像素
    [SerializeField]
    [InspectorName("鼠标灵敏度")]
    [Tooltip("鼠标每移动一个像素转动的角度。")]
    private float _mouseSensitivity = 0.08f;

    // 相机俯仰的最大角度
    [SerializeField]
    [InspectorName("俯仰限制")]
    [Tooltip("相机向上与向下俯仰的最大角度。")]
    private float _pitchLimit = 60f;

    // 头顶玩家名文本
    [SerializeField]
    [InspectorName("玩家名文本")]
    [Tooltip("显示玩家名的头顶文本组件。")]
    private TextMesh _nameText;

    // 玩家名，由服务端在生成时确定后同步给所有客户端
    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    private string _playerName = string.Empty;

    // 服务端本次会话已发放到的玩家编号
    private static int _playerNumberSeed;

    // 方向键移动输入
    private InputAction _moveAction;

    // 鼠标移动输入
    private InputAction _lookAction;

    // 角色控制器，负责平面位移
    private CharacterController _controller;

    // 跟随相机
    private Transform _cameraTransform;

    // 相机挂到玩家之前所属的父节点
    private Transform _cameraOriginalParent;

    // 相机挂到玩家之前的局部位置
    private Vector3 _cameraOriginalLocalPosition;

    // 相机挂到玩家之前的局部旋转
    private Quaternion _cameraOriginalLocalRotation;

    // 本地玩家的水平朝向角度
    private float _yaw;

    // 相机的俯仰角度
    private float _pitch;

    // 相机是否已挂到本对象上
    private bool _cameraAttached;

    // 当前是否由本对象锁定着光标
    private bool _cursorCaptured;

    /// <summary>
    /// 构建方向键与 WASD 的移动输入、鼠标输入动作，并缓存角色控制器。
    /// </summary>
    private void Awake()
    {
        // NetworkBehaviour 未提供可覆写的 Awake，此处直接使用 Unity 生命周期

        // 预制体上已有角色控制器，缺失时补齐，避免位移路径空引用
        _controller = GetComponent<CharacterController>();
        if (_controller == null)
        {
            _controller = gameObject.AddComponent<CharacterController>();
        }

        // 方向键与 WASD 合成同一个二维向量，上下对应 Z 轴、左右对应 X 轴
        _moveAction = new InputAction("Move", InputActionType.Value);
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/downArrow")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/rightArrow")
            .With("Right", "<Keyboard>/d");

        // 鼠标增量直接作为转向输入，锁定光标后才有稳定手感
        _lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
    }

    /// <summary>
    /// 服务端生成玩家时确定并下发玩家名。
    /// </summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        _playerName = ResolvePlayerName();
    }

    /// <summary>
    /// 客户端生成玩家后按当前同步值刷新头顶文字。
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        ApplyPlayerName(_playerName);
    }

    /// <summary>
    /// 本地玩家获得控制权时挂载相机、锁定光标并启用输入。
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        AttachCamera();

        // 以生成时的朝向作为初始水平朝向，相机俯仰从零开始
        _yaw = transform.eulerAngles.y;
        _pitch = 0f;

        _moveAction?.Enable();
        _lookAction?.Enable();

        SetCursorLocked(true);
    }

    /// <summary>
    /// 本地玩家失去控制权时停用输入、还原相机并解锁光标。
    /// </summary>
    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();

        _moveAction?.Disable();
        _lookAction?.Disable();

        RestoreCamera();
        SetCursorLocked(false);
    }

    /// <summary>
    /// 销毁时释放输入动作并还原相机。
    /// </summary>
    private void OnDestroy()
    {
        if (_moveAction != null)
        {
            _moveAction.Disable();
            _moveAction.Dispose();
            _moveAction = null;
        }

        if (_lookAction != null)
        {
            _lookAction.Disable();
            _lookAction.Dispose();
            _lookAction = null;
        }

        RestoreCamera();

        // 只有本对象锁定过光标时才解锁，避免远程玩家销毁时影响本机操作
        if (_cursorCaptured)
        {
            SetCursorLocked(false);
        }
    }

    /// <summary>
    /// 每帧读取输入并驱动本地玩家的朝向与位移。
    /// </summary>
    private void Update()
    {
        if (!isLocalPlayer) return;

        HandleCursorToggle();
        HandleLook();
        HandleMove();
    }

    /// <summary>
    /// 处理光标的临时解锁与重新锁定。
    /// </summary>
    private void HandleCursorToggle()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // Esc 临时解锁，便于在运行中操作鼠标
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetCursorLocked(false);
            return;
        }

        if (mouse == null) return;

        // 解锁状态下点击画面重新锁定
        if (!_cursorCaptured && mouse.leftButton.wasPressedThisFrame)
        {
            SetCursorLocked(true);
        }
    }

    /// <summary>
    /// 按鼠标增量更新玩家的水平朝向与相机的俯仰。
    /// </summary>
    private void HandleLook()
    {
        // 解锁状态下不改朝向，鼠标在界面控件上移动时角色保持静止
        if (!_cursorCaptured) return;

        Vector2 delta = _lookAction.ReadValue<Vector2>();

        _yaw += delta.x * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch - delta.y * _mouseSensitivity, -_pitchLimit, _pitchLimit);

        // 玩家本体只承载水平朝向，俯仰全部交给相机子物体
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (_cameraTransform != null)
        {
            _cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    /// <summary>
    /// 按方向键沿角色朝向在水平面上位移。
    /// </summary>
    private void HandleMove()
    {
        if (_controller == null || _moveAction == null) return;

        Vector2 input = _moveAction.ReadValue<Vector2>();

        // 朝向只取水平投影，俯仰不参与移动方向
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 direction = right * input.x + forward * input.y;

        // 复合输入本身已归一化，此处再兜一层，杜绝对角移动加速
        direction = Vector3.ClampMagnitude(direction, 1f);

        _controller.Move(direction * _moveSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 把主相机挂到本对象上并对齐到玩家原点。
    /// </summary>
    private void AttachCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[PlayerMove] 场景中没有主相机，鼠标只控制角色朝向");
            return;
        }

        _cameraTransform = mainCamera.transform;
        _cameraOriginalParent = _cameraTransform.parent;
        _cameraOriginalLocalPosition = _cameraTransform.localPosition;
        _cameraOriginalLocalRotation = _cameraTransform.localRotation;

        _cameraTransform.SetParent(transform, false);
        _cameraTransform.localPosition = Vector3.zero;
        _cameraTransform.localRotation = Quaternion.identity;
        _cameraAttached = true;
    }

    /// <summary>
    /// 把相机还原到挂载前的父节点与局部变换。
    /// </summary>
    private void RestoreCamera()
    {
        if (!_cameraAttached) return;

        _cameraAttached = false;

        if (_cameraTransform == null) return;

        // 脱离玩家层级，避免玩家销毁时相机一同被销毁
        if (_cameraOriginalParent != null)
        {
            _cameraTransform.SetParent(_cameraOriginalParent, false);
        }
        else
        {
            // 原父节点已不存在，退化为场景根节点，保证相机继续存活
            _cameraTransform.SetParent(null, false);
        }

        _cameraTransform.localPosition = _cameraOriginalLocalPosition;
        _cameraTransform.localRotation = _cameraOriginalLocalRotation;
    }

    /// <summary>
    /// 切换光标锁定状态。
    /// </summary>
    /// <param name="locked">是否锁定光标。</param>
    private void SetCursorLocked(bool locked)
    {
        _cursorCaptured = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    #region 玩家名

    /// <summary>
    /// 按当前联机模式确定本玩家在服务端的显示名。
    /// </summary>
    /// <returns>本玩家的显示名。</returns>
    private string ResolvePlayerName()
    {
        int number = NextPlayerNumber();
        string steamName = ResolveSteamName();

        return string.IsNullOrEmpty(steamName) ? $"Player{number}" : steamName;
    }

    /// <summary>
    /// 取服务端本次会话的下一个玩家编号。
    /// </summary>
    /// <returns>玩家编号，房主固定为 1。</returns>
    private int NextPlayerNumber()
    {
        // 房主的玩家总是本会话第一个生成的对象，以它为计数起点，
        // 避免静态编号在上一局结束后残留
        if (connectionToClient == NetworkServer.localConnection)
        {
            _playerNumberSeed = 1;
            return _playerNumberSeed;
        }

        _playerNumberSeed++;
        return _playerNumberSeed;
    }

    /// <summary>
    /// 按当前联机模式读取本玩家的 Steam 名称。
    /// </summary>
    /// <returns>Steam 名称；非 Steam 对局或名称不可用时返回空字符串。</returns>
    private string ResolveSteamName()
    {
        // Steam 房间管理器只随 Steam 场景存在，用它在场与否区分房间模式与本地模式；
        // SteamManager.Initialized 在实例缺失时会自建对象，因此放在后面由短路兜住
        if (SteamRoomManager.GetInstance() == null || !SteamManager.Initialized)
            return string.Empty;

        // 房主走本机 Steam 账号，其余连接从传输层地址取回 SteamID
        if (connectionToClient == NetworkServer.localConnection)
            return SteamFriends.GetPersonaName();

        if (connectionToClient == null || !ulong.TryParse(connectionToClient.address, out ulong steamId))
            return string.Empty;

        string steamName = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));

        // Steam 尚未拿到对方资料时不会给出昵称
        return steamName == "[unknown]" ? string.Empty : steamName;
    }

    /// <summary>
    /// 玩家名同步值变化时刷新头顶文字。
    /// </summary>
    /// <param name="oldName">变化前的玩家名。</param>
    /// <param name="newName">变化后的玩家名。</param>
    private void OnPlayerNameChanged(string oldName, string newName)
    {
        ApplyPlayerName(newName);
    }

    /// <summary>
    /// 把玩家名写入头顶文本组件。
    /// </summary>
    /// <param name="playerName">要显示的玩家名。</param>
    private void ApplyPlayerName(string playerName)
    {
        if (_nameText == null)
        {
            Debug.LogWarning("[PlayerMove] 未配置玩家名文本组件，玩家名不会显示");
            return;
        }

        _nameText.text = playerName;
    }

    #endregion
}
