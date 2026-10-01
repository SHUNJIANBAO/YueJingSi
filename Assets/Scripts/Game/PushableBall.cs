using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 在服务端缓存各玩家对球体的推力，并逐物理帧合成合力驱动球体刚体。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PushableBall : NetworkBehaviour
{
    // 推力状态在该秒数内没有刷新即失效
    [SerializeField]
    [InspectorName("推力保持时长")]
    [Tooltip("超过该秒数没有收到新的推力上报，就认为玩家已经停止推球。")]
    private float _pushTimeout = 0.1f;

    // 球体刚体
    private Rigidbody _rigidbody;

    // 各玩家当前施加的推力，键为玩家网络标识
    private readonly Dictionary<uint, PushState> _pushes = new Dictionary<uint, PushState>();

    /// <summary>
    /// 缓存球体刚体引用。
    /// </summary>
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// 碰撞体进入球体接触范围时同步本地玩家的接触状态。
    /// </summary>
    /// <param name="other">进入接触范围的碰撞体。</param>
    private void OnTriggerEnter(Collider other)
    {
        NotifyLocalPlayerTouch(other, true);
    }

    /// <summary>
    /// 碰撞体离开球体接触范围时同步本地玩家的接触状态。
    /// </summary>
    /// <param name="other">离开接触范围的碰撞体。</param>
    private void OnTriggerExit(Collider other)
    {
        NotifyLocalPlayerTouch(other, false);
    }

    /// <summary>
    /// 查找碰撞体所属的本地玩家，并把接触状态交给该玩家的推球组件。
    /// </summary>
    /// <param name="other">与球体接触范围重叠的碰撞体。</param>
    /// <param name="touching">是否进入接触状态。</param>
    private void NotifyLocalPlayerTouch(Collider other, bool touching)
    {
        if (other == null) return;

        PlayerPush playerPush = other.GetComponentInParent<PlayerPush>();
        if (playerPush == null || !playerPush.isLocalPlayer) return;

        playerPush.SetBallTouching(touching);
    }

    /// <summary>
    /// 服务端每物理帧合成尚未失效的推力并施加到球体刚体上。
    /// </summary>
    private void FixedUpdate()
    {
        // 球体物理只在服务端模拟，客户端由 NetworkRigidbodyReliable 驱动位置
        if (!isServer) return;
        if (_pushes.Count == 0) return;

        Vector3 totalForce = Vector3.zero;
        float now = Time.time;
        List<uint> expired = null;

        foreach (KeyValuePair<uint, PushState> pair in _pushes)
        {
            if (now - pair.Value.LastTime > _pushTimeout)
            {
                if (expired == null) expired = new List<uint>();
                expired.Add(pair.Key);
                continue;
            }

            totalForce += pair.Value.Direction * pair.Value.Strength;
        }

        if (expired != null)
        {
            foreach (uint playerNetId in expired)
            {
                _pushes.Remove(playerNetId);
            }
        }

        if (totalForce == Vector3.zero) return;

        _rigidbody.AddForce(totalForce, ForceMode.Force);
    }

    /// <summary>
    /// 记录或刷新某个玩家施加的推力，仅在服务端生效。
    /// </summary>
    /// <param name="playerNetId">推球玩家的网络标识。</param>
    /// <param name="direction">推力的水平方向。</param>
    /// <param name="strength">推力大小。</param>
    public void AddPush(uint playerNetId, Vector3 direction, float strength)
    {
        if (!isServer) return;

        _pushes[playerNetId] = new PushState
        {
            Direction = direction,
            Strength = strength,
            LastTime = Time.time
        };
    }

    /// <summary>
    /// 单个玩家的推力状态。
    /// </summary>
    private struct PushState
    {
        // 推力方向
        public Vector3 Direction;
        // 推力大小
        public float Strength;
        // 最近一次上报时刻
        public float LastTime;
    }
}
