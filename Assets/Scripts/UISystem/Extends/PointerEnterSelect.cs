using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 鼠标进入时把该节点设为当前选中对象。
/// </summary>
public class PointerEnterSelect : MonoBehaviour, IPointerEnterHandler
{
    /// <summary>
    /// 指针进入时选中本节点。
    /// </summary>
    /// <param name="eventData">事件数据。</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning($"[PointerEnterSelect] 事件系统不存在，选中失败，节点名:{name}");
            return;
        }

        EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
