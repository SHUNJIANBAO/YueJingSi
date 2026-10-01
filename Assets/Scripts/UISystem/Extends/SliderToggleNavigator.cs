using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Slider扩展，支持导航到Toggle Group
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderToggleNavigator : MonoBehaviour, IMoveHandler, ISelectHandler, IDeselectHandler
{
    [Header("Toggle Group导航")] [Tooltip("关联的Toggle Group")] [SerializeField]
    private EnhancedToggleGroup targetToggleGroup;

    [SerializeField] private E_ToggleGroupDirection _toggleGroupDirection = E_ToggleGroupDirection.Down;

    [Header("导航设置")] [Tooltip("导航时是否播放声音")] [SerializeField]
    private bool playNavigationSound = true;

    [SerializeField] private AudioClip navigationSound;

    private AudioSource audioSource;
    private bool isSelected = false;

    private void Awake()
    {
        if (playNavigationSound)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
    }

    public void OnMove(AxisEventData eventData)
    {
        if (!isSelected) return;

        switch (eventData.moveDir)
        {
            case MoveDirection.Up:
                if ((_toggleGroupDirection == E_ToggleGroupDirection.Up ||
                     _toggleGroupDirection == E_ToggleGroupDirection.Both) && targetToggleGroup != null)
                {
                    NavigateToToggleGroup();
                    eventData.Use();
                }

                break;

            case MoveDirection.Down:
                if ((_toggleGroupDirection == E_ToggleGroupDirection.Down ||
                     _toggleGroupDirection == E_ToggleGroupDirection.Both) && targetToggleGroup != null)
                {
                    NavigateToToggleGroup();
                    eventData.Use();
                }

                break;
        }
    }

    /// <summary>
    /// 导航到关联的Toggle Group
    /// </summary>
    private void NavigateToToggleGroup()
    {
        if (targetToggleGroup == null) return;

        // 告诉Toggle Group从哪个方向进入
        targetToggleGroup.EnterFromOtherComponent();

        // 播放声音
        if (playNavigationSound && navigationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(navigationSound);
        }
    }
}

public enum E_ToggleGroupDirection
{
    Up,
    Down,
    Both,
}