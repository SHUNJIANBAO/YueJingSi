using System;
using UnityEngine;

/// <summary>
/// 计时器时间速率类别。
/// </summary>
public enum E_TimerType
{
    /// <summary>受时间速率影响，随 Time.timeScale 缩放推进。</summary>
    [InspectorName("受时间速率影响")] Affected = 0,
    /// <summary>不受时间速率影响，按真实时间恒定推进。</summary>
    [InspectorName("不受时间速率影响")] Unaffected = 1,
}

/// <summary>
/// 计时器类，支持单次倒计时、循环定时间隔回调与重置
/// </summary>
public class Timer
{
    // 总时长与剩余生命周期
    private float _lifeTime;
    private float _totalTime;

    // 间隔时长
    private float _intervalTime;

    /// <summary>
    /// 间隔触发的回调委托
    /// </summary>
    public Action intervalAction;

    /// <summary>
    /// 完成时触发的回调委托
    /// </summary>
    public Action completeAction;

    // 是否循环执行
    private bool loop;

    // 计时器类别，决定推进使用的增量来源
    private E_TimerType _timerType;

    /// <summary>
    /// 该计时器的时间速率类别。
    /// </summary>
    public E_TimerType TimerType => _timerType;

    /// <summary>
    /// 是否已结束/已完成
    /// </summary>
    public bool IsComplete { get; private set; }

    // 累积经过时间
    private float _timeCount;

    /// <summary>
    /// 当前周期内累计时间
    /// </summary>
    public float TimeCount => _timeCount;

    /// <summary>
    /// 构造新的计时器实例
    /// </summary>
    /// <param name="totalTime">总时长（秒）</param>
    /// <param name="intervalTime">间隔时长（秒），为0表示仅单次倒计时</param>
    /// <param name="loop">是否无限循环（若为 true 则总时长无效）</param>
    /// <param name="timerType">时间速率类别，默认受 Time.timeScale 影响</param>
    public Timer(float totalTime, float intervalTime = 0, bool loop = false, E_TimerType timerType = E_TimerType.Affected)
    {
        _totalTime = this._lifeTime = totalTime;
        this._intervalTime = intervalTime;
        this.loop = loop;
        _timerType = timerType;
    }

    /// <summary>
    /// 重置计时器状态（重置已累积时间、生命周期并复位 IsComplete，允许已完成的计时器重新生效）
    /// </summary>
    public void Reset()
    {
        _lifeTime = _totalTime;
        _timeCount = 0;
        IsComplete = false;
    }

    /// <summary>
    /// 推进计时（支持大帧补发间隔、单次倒计时直接扣减、明确分支互斥规避同帧双触发）
    /// </summary>
    /// <param name="deltaTime">自上一帧流逝的时间增量（秒）</param>
    public void Update(float deltaTime)
    {
        if (IsComplete) return;

        // 场景 A：带有间隔的循环或多段计时
        if (_intervalTime > 0)
        {
            _timeCount += deltaTime;

            // 采用 while 循环，保证当 deltaTime 超过多个间隔时能够补足触发回调
            while (_timeCount >= _intervalTime)
            {
                _lifeTime -= _intervalTime;
                _timeCount -= _intervalTime;
                intervalAction?.Invoke();

                if (IsComplete) return;
            }

            // 非无限循环模式下，当总时长消耗完毕则触发完成
            if (!loop && _lifeTime <= 0)
            {
                completeAction?.Invoke();
                IsComplete = true;
            }
        }
        // 场景 B：无间隔的纯倒计时
        else
        {
            _timeCount += deltaTime;
            if (_timeCount >= _totalTime)
            {
                completeAction?.Invoke();
                IsComplete = true;
            }
        }
    }

    /// <summary>
    /// 强制标记为完成并置空回调委托（供 RemoveListener 在回调中安全移除），防止野指针与二次触发
    /// </summary>
    public void ForceComplete()
    {
        IsComplete = true;
        intervalAction = null;
        completeAction = null;
    }
}
