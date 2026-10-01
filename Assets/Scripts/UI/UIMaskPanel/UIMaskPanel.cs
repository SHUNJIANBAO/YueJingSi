using System;
using DG.Tweening;

/// <summary>
/// 全屏遮罩面板，开合动画固定一秒淡入淡出。
/// 淡出必须同时满足「已淡入全黑」与「场景已就绪」两个条件，避免动画被中途打断。
/// </summary>
public class UIMaskPanel : UIPanelBase
{
    // 是否已完成淡入（已全黑）
    private bool _covered;

    // 是否已收到揭示请求（通常是场景加载就绪）
    private bool _revealRequested;

    /// <summary>
    /// 打开动画：一秒淡入，全黑后回调，并在此时机兑现期间挂起的揭示请求。
    /// </summary>
    /// <param name="callback">动画完成回调。</param>
    public override void PlayOpenAnim(Action callback)
    {
        m_CanvasGroup.DOKill();
        m_CanvasGroup.alpha = 0;
        _covered = false;
        _revealRequested = false;
        m_CanvasGroup.DOFade(1, 1f).SetUpdate(true).OnComplete(() =>
        {
            _covered = true;
            callback?.Invoke();
            // 淡入期间若已收到揭示请求（场景提前加载完），此刻再淡出，保证"先全黑再淡出"
            TryReveal();
        });
    }

    /// <summary>
    /// 立即显示：遮罩以外观呈现在打开动画的淡入过程中完成，此处不提前置为不透明。
    /// </summary>
    public override void QuickShow()
    {
        // 基类会把 alpha 直接置 1，遮罩会在淡入前先满屏闪一帧黑，故改为从全透明起步
        m_CanvasGroup.alpha = 0;
        m_CanvasGroup.blocksRaycasts = true;
        m_CanvasGroup.interactable = true;
    }

    /// <summary>
    /// 关闭动画：一秒淡出后回调。
    /// </summary>
    /// <param name="callback">动画完成回调。</param>
    public override void PlayCloseAnim(Action callback)
    {
        m_CanvasGroup.DOKill();
        m_CanvasGroup.alpha = 1;
        m_CanvasGroup.DOFade(0, 1f).SetUpdate(true).OnComplete(() =>
        {
            callback?.Invoke();
        });
    }

    /// <summary>
    /// 请求揭示遮罩：仅当已淡入全黑时才淡出，否则等淡入结束后再兑现。
    /// </summary>
    public void RequestReveal()
    {
        _revealRequested = true;
        TryReveal();
    }

    /// <summary>
    /// 双条件均满足时执行淡出。
    /// </summary>
    private void TryReveal()
    {
        if (!_covered || !_revealRequested) return;

        _revealRequested = false;
        UIManager.Instance.ClosePanel<UIMaskPanel>();
    }
}
