using Mirror;
using UnityEngine;

/// <summary>
/// 本地玩家与可推动球体接触时，把推力上报给服务端。
/// </summary>
[DisallowMultipleComponent]
public class PlayerPush : NetworkBehaviour
{
    // 单个玩家施加的推力大小
    [SerializeField]
    [InspectorName("推力大小")]
    [Tooltip("玩家接触球体时向服务端上报的推力大小。")]
    private float _pushPower = 10f;

    // 持续接触时的上报间隔
    [SerializeField]
    [InspectorName("上报间隔")]
    [Tooltip("持续接触球体时两次推力上报之间的最小秒数。")]
    private float _sendInterval = 0.05f;

    // 服务端校验的推球距离上限
    [SerializeField]
    [InspectorName("推球距离")]
    [Tooltip("服务端允许玩家推动球体的最大距离，超出该距离的上报会被丢弃。")]
    private float _pushRange = 3f;

    // 推力方向变化超过该角度时立即上报，不等上报间隔
    private const float DIRECTION_CHANGE_ANGLE = 30f;

    // 最近一次上报推力的时刻
    private float _lastSendTime;

    // 最近一次上报的推力方向
    private Vector3 _lastDirection;

    // 是否已经有可供比较的上报方向
    private bool _hasLastDirection;

    // 接触状态复核的间隔秒数
    private const float RECHECK_INTERVAL = 0.5f;

    // 接触状态复核的查询半径，玩家胶囊半径 0.5 与球体接触范围 0.6 之和再留出几何余量
    private const float RECHECK_RADIUS = 0.7f;

    // 复核查询结果的复用缓冲
    private readonly Collider[] _overlapBuffer = new Collider[32];

    // 当前接触球体的计数
    private int _touchCount;

    // 最近一次复核接触状态的时刻
    private float _lastRecheckTime;

    /// <summary>
    /// 每帧按固定间隔复核与球体的接触状态。
    /// </summary>
    private void Update()
    {
        if (!isLocalPlayer) return;
        if (Time.time - _lastRecheckTime < RECHECK_INTERVAL) return;

        _lastRecheckTime = Time.time;
        RecheckTouchState();
    }

    /// <summary>
    /// 由球体在本地玩家进入或离开其接触范围时调用。
    /// </summary>
    /// <param name="touching">是否进入接触状态。</param>
    public void SetBallTouching(bool touching)
    {
        _touchCount = Mathf.Max(0, _touchCount + (touching ? 1 : -1));
        RefreshTouchUI();
    }

    /// <summary>
    /// 以物理查询结果为准纠正接触计数，并刷新界面。
    /// </summary>
    private void RecheckTouchState()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, RECHECK_RADIUS, _overlapBuffer);

        bool touching = false;
        for (int i = 0; i < count; i++)
        {
            if (_overlapBuffer[i].GetComponentInParent<PushableBall>() == null) continue;

            touching = true;
            break;
        }

        _touchCount = touching ? Mathf.Max(1, _touchCount) : 0;
        RefreshTouchUI();
    }

    /// <summary>
    /// 把当前接触状态同步到游戏界面。
    /// </summary>
    private void RefreshTouchUI()
    {
        UIManager.Instance.GetPanel<UIGamePanel>()?.SetTouchState(_touchCount > 0);
    }

    /// <summary>
    /// 角色控制器撞到球体时，按节流把推力上报给服务端。
    /// </summary>
    /// <param name="hit">角色控制器本帧的碰撞信息。</param>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!isLocalPlayer) return;

        PushableBall ball = hit.collider.GetComponentInParent<PushableBall>();
        if (ball == null) return;

        // 只取水平分量，玩家站到球上时不把球压向地面
        Vector3 direction = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up);
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        bool directionChanged = !_hasLastDirection ||
                                Vector3.Angle(_lastDirection, direction) > DIRECTION_CHANGE_ANGLE;
        bool intervalElapsed = Time.time - _lastSendTime >= _sendInterval;
        if (!directionChanged && !intervalElapsed) return;

        _hasLastDirection = true;
        _lastDirection = direction;
        _lastSendTime = Time.time;

        CmdPush(ball.netId, direction, _pushPower);
    }

    /// <summary>
    /// 向服务端上报对指定球体的推力。
    /// </summary>
    /// <param name="ballNetId">目标球体的网络标识。</param>
    /// <param name="direction">推力的水平方向。</param>
    /// <param name="strength">推力大小。</param>
    [Command]
    private void CmdPush(uint ballNetId, Vector3 direction, float strength)
    {
        if (!NetworkServer.spawned.TryGetValue(ballNetId, out NetworkIdentity identity) || identity == null) return;

        PushableBall ball = identity.GetComponent<PushableBall>();
        if (ball == null) return;

        // 服务端按接触距离校验，丢弃明显超出接触范围的上报
        if (Vector3.Distance(transform.position, identity.transform.position) > _pushRange) return;

        ball.AddPush(netId, direction, strength);
    }
}
