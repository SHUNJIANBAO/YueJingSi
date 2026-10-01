using System.Collections.Generic;
using UnityEngine;
using System;

public class TimerManager : MonoBehaviour
{
    #region 单例
    private static TimerManager _instance;
    public static TimerManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TimerManager>();
                if (_instance == null)
                {
                    _instance = new GameObject("TimeManager").AddComponent<TimerManager>();
                }
                DontDestroyOnLoad(_instance.gameObject);
            }
            return _instance;
        }
    }
    #endregion

    /// <summary>
    /// 已存在的计时器管理器实例，尚未创建时返回 null，不会触发对象创建。
    /// </summary>
    /// <returns>计时器管理器实例，未创建时为 null。</returns>
    public static TimerManager GetInstance()
    {
        return _instance;
    }

    private static readonly List<Timer> timerActions = new List<Timer>();

    private void Update()
    {
        for (int i = 0; i < timerActions.Count; i++)
        {
            Timer timer = timerActions[i];
            if (timer.IsComplete) continue;
            // 按类别分流：不受影响类保持真实时间恒定推进，默认类随 Time.timeScale 缩放
            float delta = timer.TimerType == E_TimerType.Unaffected
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            timer.Update(delta);
        }
        // 单次遍历移除已完成的 timer，避免 per-item Remove 引起的 O(n²)。
        timerActions.RemoveAll(t => t.IsComplete);
    }

    /// <param name="time">总时长</param>
    /// <param name="action">计时结束后调用的方法</param>
    /// <param name="timerType">时间速率类别，默认受 Time.timeScale 影响</param>
    public Timer AddListener(float totalTime, Action action, E_TimerType timerType = E_TimerType.Affected)
    {
        Timer temp = new Timer(totalTime, 0, false, timerType);
        temp.completeAction = action;
        timerActions.Add(temp);
        return temp;
    }

    /// <summary>
    /// 添加循环事件
    /// </summary>
    /// <param name="intervalTime"></param>
    /// <param name="intervalAction"></param>
    /// <param name="timerType">时间速率类别，默认受 Time.timeScale 影响</param>
    /// <returns></returns>
    public Timer AddLoopListener(float intervalTime, Action intervalAction, E_TimerType timerType = E_TimerType.Affected)
    {
        Timer temp = new Timer(1, intervalTime, true, timerType);
        temp.intervalAction = intervalAction;
        timerActions.Add(temp);
        return temp;
    }

    /// <summary>
    /// 计时多次调用方法
    /// </summary>
    /// <param name="totalTime">总时长</param>
    /// <param name="intervalTime">间隔时长</param>
    /// <param name="action">间隔调用的方法</param>
    /// <param name="loop">是否循环（总时长无效）</param>
    /// <param name="timerType">时间速率类别，默认受 Time.timeScale 影响</param>
    public Timer AddListener(float totalTime, float intervalTime, Action intervalAction, Action completeAction = null, E_TimerType timerType = E_TimerType.Affected)
    {
        Timer temp = new Timer(totalTime, intervalTime, false, timerType);
        temp.intervalAction = intervalAction;
        temp.completeAction = completeAction;
        timerActions.Add(temp);
        return temp;
    }

    public void RemoveListener(Timer timer)
    {
        if (timer == null) return;
        // 调用方常在 timer 回调中调用 RemoveListener（修改 List 正在被 Update 遍历），
        // 因此只标记完成，真实清理交给下一帧的 RemoveAll，避免迭代中变更集合。
        if (!timer.IsComplete) timer.ForceComplete();
    }

    public void ClearActions()
    {
        timerActions.Clear();
    }

    private void OnDestroy()
    {
        timerActions.Clear();
    }
}