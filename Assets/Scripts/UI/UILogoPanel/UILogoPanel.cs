using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 启动 Logo 面板，任意键按下后进入主界面。
/// </summary>
public class UILogoPanel : UIPanelBase
{
    // 任意键监听动作
    private InputAction _anyKeyAction;

    /// <summary>
    /// 创建任意键监听动作，并建立面板节点字典。
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        _anyKeyAction = new InputAction(binding: "*/<Button>");
        _anyKeyAction.started += OnAnyKeyPressed;
    }

    /// <summary>
    /// 获取并缓存面板内 UI 组件引用。
    /// </summary>
    protected override void GetUIComponents()
    {
    }

    /// <summary>
    /// 启用任意键监听。
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        _anyKeyAction?.Enable();
    }

    /// <summary>
    /// 停用任意键监听。
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        _anyKeyAction?.Disable();
    }

    /// <summary>
    /// 释放任意键监听，避免面板销毁后回调仍持有本实例。
    /// </summary>
    private void OnDestroy()
    {
        if (_anyKeyAction == null) return;

        _anyKeyAction.started -= OnAnyKeyPressed;
        _anyKeyAction.Dispose();
        _anyKeyAction = null;
    }

    /// <summary>
    /// 面板关闭后打开主界面面板。
    /// </summary>
    /// <param name="args">关闭参数。</param>
    public override void OnClose(params object[] args)
    {
        base.OnClose(args);
    }

    /// <summary>
    /// 任意键按下：立即停用监听并打开主界面，保证整个流程只触发一次。
    /// </summary>
    /// <param name="context">输入回调上下文。</param>
    private void OnAnyKeyPressed(InputAction.CallbackContext context)
    {
        // 关键：*/<Button> 会捕获鼠标左键，若不停用监听，后续在房间/对局里
        // 点击任何按钮都会再次触发本回调并重新打开主界面。此处立即停用，只响应首次。
        if (_anyKeyAction == null || !_anyKeyAction.enabled) return;
        _anyKeyAction.Disable();

        UIManager.Instance.OpenPanel<UIMainPanel>(false, () =>
        {
            UIManager.Instance.ClosePanel<UILogoPanel>();
        });
    }
}
