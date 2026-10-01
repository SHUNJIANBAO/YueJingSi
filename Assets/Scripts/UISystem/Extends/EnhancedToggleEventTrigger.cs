using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Toggle事件触发器，处理方向键输入
/// </summary>
[RequireComponent(typeof(Toggle))]
public class EnhancedToggleEventTrigger : MonoBehaviour, IMoveHandler
{
    private EnhancedToggleGroup toggleGroup;
    private Toggle toggle;

    public void Initialize(EnhancedToggleGroup group, Toggle toggleComponent)
    {
        toggleGroup = group;
        toggle = toggleComponent;
    }


    public void OnMove(AxisEventData eventData)
    {
        if (toggleGroup == null) return;

        switch (eventData.moveDir)
        {
            case MoveDirection.Left:
                toggleGroup.NavigateLeft();
                eventData.Use();
                break;
            case MoveDirection.Right:
                toggleGroup.NavigateRight();
                eventData.Use();
                break;
            case MoveDirection.Up:
                // 处理向上导航
                toggleGroup.HandleVerticalNavigation(true);
                eventData.Use();
                break;

            case MoveDirection.Down:
                // 处理向下导航
                toggleGroup.HandleVerticalNavigation(false);
                eventData.Use();
                break;
        }
    }
}