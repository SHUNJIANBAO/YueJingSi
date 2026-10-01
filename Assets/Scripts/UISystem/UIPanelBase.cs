using UnityEngine.UI;
using DG.Tweening;
using System;
using UnityEngine;

public enum E_PanelType
{
    Normal,
    Top
}
public enum E_PanelMemoryType
{
    [InspectorName("常驻界面")]
    Forever,
    [InspectorName("临时界面")]
    Temp
}
public class UIPanelBase : UIBase, IPanel
{
    [SerializeField]
    private E_PanelType _panelType = E_PanelType.Normal;
    [SerializeField]
    private E_PanelMemoryType _panelMemoryType = E_PanelMemoryType.Temp;
    public E_PanelType PanelType => _panelType;
    public E_PanelMemoryType PanelMemoryType => _panelMemoryType;

    protected override void GetUIComponents()
    {
    }

    protected override void AddUIListeners()
    {
    }

    protected override void OnInit()
    {
    }


    public void Open(params object[] args)
    {
        QuickShow();
        OnOpen(args);
        Refresh(args);
    }

    public void Close(params object[] args)
    {
        QuickHide();
        OnClose(args);
    }

    public virtual void PlayOpenAnim(Action callback)
    {
        m_CanvasGroup.DOKill(); // 先杀掉上一段动画，避免多个 Tween 叠加
        m_CanvasGroup.alpha = 0;
        m_CanvasGroup.blocksRaycasts = true; // 打开动画期间即允许交互（淡入过程可点）
        m_CanvasGroup.interactable = true;
        m_CanvasGroup.DOFade(1, .5f).SetUpdate(true).OnComplete(() =>
        {
            callback?.Invoke();
        });
    }
    public virtual void PlayCloseAnim(Action callback)
    {
        m_CanvasGroup.DOKill(); // 先杀掉上一段动画，避免多个 Tween 叠加
        m_CanvasGroup.alpha = 1;
        m_CanvasGroup.DOFade(0, .5f).SetUpdate(true).OnComplete(() =>
        {
            callback?.Invoke();
        });
    }

    /// <summary>
    /// 取消面板当前播放中的开/关动画（杀掉 Tween，其 OnComplete 不再触发）
    /// </summary>
    public void CancelAnim()
    {
        m_CanvasGroup.DOKill();
        m_CanvasGroup.alpha = 1;
        m_CanvasGroup.blocksRaycasts = true;
        m_CanvasGroup.interactable = true;
    }

    protected virtual void OnOpen(params object[] objs)
    {
    }

    public void Focus()
    {
        OnFocus();
    }

    public void LostFocus()
    {
        OnLostFocus();
    }

    public virtual void OnFocus()
    {
        m_CanvasGroup.blocksRaycasts = true;
    }

    public virtual void OnLostFocus()
    {
        m_CanvasGroup.blocksRaycasts = false;
    }


    public virtual void OnClose(params object[] args)
    {
    }

    public override void Refresh(params object[] args)
    {
        base.Refresh(args);
        OnRefresh(args);
    }

    /// <summary>
    /// 刷新
    /// </summary>
    /// <param name="objs"></param>
    protected virtual void OnRefresh(params object[] args)
    {
    }

    /// <summary>
    /// 立即显示，并同步恢复射线与交互状态。
    /// </summary>
    public override void QuickShow()
    {
        base.QuickShow();

        m_CanvasGroup.blocksRaycasts = true;
        m_CanvasGroup.interactable = true;
    }

    /// <summary>
    /// 立即隐藏，并同步屏蔽射线与交互状态。
    /// </summary>
    public override void QuickHide()
    {
        base.QuickHide();

        m_CanvasGroup.blocksRaycasts = false;
        m_CanvasGroup.interactable = false;
    }
}