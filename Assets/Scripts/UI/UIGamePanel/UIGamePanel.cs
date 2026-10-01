using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIGamePanel : UIPanelBase
{
    // 触碰球体时显示的文案
    private const string TOUCH_TEXT = "Tapped";

    // 未触碰球体时显示的文案
    private const string UNTOUCH_TEXT = "Untapped";

    // 触碰球体时的文字颜色
    private static readonly Color TOUCH_COLOR = new Color(0.298f, 0.851f, 0.392f);

    TextMeshProUGUI Text_Touch;

    // 当前显示的接触状态
    private bool _isTouching;

    protected override void GetUIComponents()
    {
        Text_Touch = GetUI<TextMeshProUGUI>("Text_Touch");
    }
    protected override void AddUIListeners()
    {
        base.AddUIListeners();
    }
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();
    }
    protected override void OnOpen(params object[] args)
    {
        base.OnOpen(args);

        _isTouching = false;
        ApplyTouchState(false);
    }

    /// <summary>
    /// 设置本地玩家与球体的接触状态显示。
    /// </summary>
    /// <param name="isTouching">是否正在接触球体。</param>
    public void SetTouchState(bool isTouching)
    {
        if (_isTouching == isTouching) return;

        _isTouching = isTouching;
        ApplyTouchState(isTouching);
    }

    /// <summary>
    /// 按接触状态刷新文本内容与文字颜色。
    /// </summary>
    /// <param name="isTouching">是否正在接触球体。</param>
    private void ApplyTouchState(bool isTouching)
    {
        if (Text_Touch == null) return;

        Text_Touch.text = isTouching ? TOUCH_TEXT : UNTOUCH_TEXT;
        Text_Touch.color = isTouching ? TOUCH_COLOR : Color.white;
    }
}
