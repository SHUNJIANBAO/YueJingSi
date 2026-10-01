using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 按固定间隔在自身位置附近的水平面内随机生成可推动球体，仅在服务端生成并同步给客户端。
/// </summary>
public class BallSpawner : MonoSingleton<BallSpawner>
{
    // 球体预制体在 Resources 下的路径
    private const string BALL_PREFAB_PATH = "Prefabs/Sphere";

    // 两次生成之间的间隔秒数
    [SerializeField]
    [InspectorName("生成间隔")]
    [Tooltip("两次生成之间间隔的秒数。")]
    private float _spawnInterval = 15f;

    // 本组件生成的球同时存在的最大数量
    [SerializeField]
    [InspectorName("最大数量")]
    [Tooltip("本组件生成的球同时存在的最大数量，超出后销毁最早生成的那个。")]
    private int _maxCount = 10;

    // 以本组件所在位置为圆心、在水平面内随机生成球体的半径
    [SerializeField]
    [InspectorName("生成半径")]
    [Tooltip("以本组件所在位置为圆心，在 xz 平面内随机生成球体的半径。")]
    private float _spawnRadius = 2f;

    // 生成球体所用的预制体
    private GameObject _ballPrefab;

    // 生成循环的协程句柄
    private Coroutine _spawnCoroutine;

    // 本组件已生成的球体，按生成顺序排列
    private readonly Queue<GameObject> _spawnedBalls = new Queue<GameObject>();

    /// <summary>
    /// 启动生成循环，立即生成第一个球体并按间隔持续生成。
    /// </summary>
    public void Begin()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[BallSpawner] 服务端未启动，无法开始生成球体");
            return;
        }

        _ballPrefab = Resources.Load<GameObject>(BALL_PREFAB_PATH);
        if (_ballPrefab == null)
        {
            Debug.LogError($"[BallSpawner] 球体预制体加载失败，路径:{BALL_PREFAB_PATH}");
            return;
        }

        if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
        _spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>
    /// 按间隔循环生成球体，服务端停止后结束循环。
    /// </summary>
    private IEnumerator SpawnLoop()
    {
        while (NetworkServer.active)
        {
            SpawnBall();
            yield return new WaitForSeconds(_spawnInterval);
        }

        _spawnCoroutine = null;
    }

    /// <summary>
    /// 以自身位置为圆心、在水平面内随机取点生成一个球体并注册到网络，超出数量上限时销毁最早生成的那个。
    /// </summary>
    private void SpawnBall()
    {
        // 圆盘内取点后只取用 xz 分量，y 保持与自身位置一致，球体从同一高度落下
        Vector2 offset = Random.insideUnitCircle * _spawnRadius;
        Vector3 spawnPosition = transform.position + new Vector3(offset.x, 0f, offset.y);

        GameObject ball = Instantiate(_ballPrefab, spawnPosition, transform.rotation);

        // 运行时创建的物体默认归属活动场景，移入本组件所在场景后才会随该场景卸载一并销毁
        SceneManager.MoveGameObjectToScene(ball, gameObject.scene);

        NetworkServer.Spawn(ball);
        _spawnedBalls.Enqueue(ball);

        int maxCount = Mathf.Max(1, _maxCount);
        while (_spawnedBalls.Count > maxCount)
        {
            GameObject oldest = _spawnedBalls.Dequeue();
            if (oldest == null) continue;
            NetworkServer.Destroy(oldest);
        }
    }
}
