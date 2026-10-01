using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 按选中状态切换目标文本颜色的简单组件。
/// </summary>
[RequireComponent(typeof(Selectable))]
public class SimpleSelectableColorChanger : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    // 目标文本
    [SerializeField]
    private Text targetText;

    // 选中时的文本颜色
    [SerializeField]
    private Color selectedColor = Color.red;

    // 默认状态的文本颜色
    [SerializeField]
    private Color normalColor = Color.white;

    // 自身的选择组件
    private Selectable _selectable;

    /// <summary>
    /// 缓存选择组件并在未指定时自动查找目标文本。
    /// </summary>
    private void Awake()
    {
        _selectable = GetComponent<Selectable>();

        if (targetText == null)
            targetText = GetComponentInChildren<Text>();
    }

    /// <summary>
    /// 启用时按当前选中状态刷新颜色。
    /// </summary>
    private void OnEnable()
    {
        UpdateColor();
    }

    /// <summary>
    /// 选中时切换为选中颜色。
    /// </summary>
    /// <param name="eventData">事件数据。</param>
    public void OnSelect(BaseEventData eventData)
    {
        if (targetText == null) return;
        targetText.color = selectedColor;
    }

    /// <summary>
    /// 取消选中时切回默认颜色。
    /// </summary>
    /// <param name="eventData">事件数据。</param>
    public void OnDeselect(BaseEventData eventData)
    {
        if (targetText == null) return;
        targetText.color = normalColor;
    }

    /// <summary>
    /// 按当前选中状态刷新文本颜色。
    /// </summary>
    private void UpdateColor()
    {
        if (targetText == null) return;

        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            targetText.color = selectedColor;
        }
        else
        {
            targetText.color = normalColor;
        }
    }

    /// <summary>
    /// 供外部主动刷新颜色。
    /// </summary>
    public void ManualUpdate()
    {
        UpdateColor();
    }
}
