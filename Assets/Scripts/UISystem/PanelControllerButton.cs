using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在按钮上，按 Inspector 配置的面板类型名打开或关闭指定面板。
/// </summary>
public class PanelControllerButton : MonoBehaviour
{
    // 要打开的面板类型名
    public string OpenPanel;

    // 打开面板是否播放动画
    [SerializeField]
    private bool _openUseAnim;

    // 要关闭的面板类型名
    public string ClosePanel;

    // 关闭面板是否播放动画
    [SerializeField]
    private bool _closeUseAnim;

    // 按钮组件
    private Button _btn;

    // 要打开的面板类型
    private Type _openType;

    // 要关闭的面板类型
    private Type _closeType;

    /// <summary>
    /// 缓存按钮与面板类型，类型名写错时提前报错。
    /// </summary>
    private void Awake()
    {
        _btn = GetComponent<Button>();
        if (_btn == null)
        {
            Debug.LogError($"[PanelControllerButton] 缺少 Button 组件，节点名:{name}");
            return;
        }

        _btn.onClick.AddListener(OnClick);

        _openType = ResolvePanelType(OpenPanel);
        _closeType = ResolvePanelType(ClosePanel);
    }

    /// <summary>
    /// 按钮点击：按配置打开与关闭面板。
    /// </summary>
    private void OnClick()
    {
        if (_openType != null)
        {
            UIManager.Instance.OpenPanel(_openType, _openUseAnim);
        }

        if (_closeType != null)
        {
            UIManager.Instance.ClosePanel(_closeType, _closeUseAnim);
        }
    }

    /// <summary>
    /// 按类型名解析面板类型，名为空或写错时返回 null。
    /// </summary>
    /// <param name="typeName">面板类型名。</param>
    /// <returns>解析到的类型，无法解析时为 null。</returns>
    private Type ResolvePanelType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        Type type = Util.GetType(typeName);
        if (type == null)
        {
            Debug.LogError($"[PanelControllerButton] 面板类型名无效，节点名:{name}，类型名:{typeName}");
        }

        return type;
    }
}
