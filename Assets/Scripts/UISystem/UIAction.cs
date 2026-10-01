using System;
//using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine;


public class UIAction : MonoBehaviour
{
    #region 给UI添加Toggle触发事件

    public void AddToggleListen(UnityAction<bool> action)
    {
        Toggle tmpBtn = transform.GetComponent<Toggle>();
        if (tmpBtn == null)
        {
            tmpBtn = gameObject.AddComponent<Toggle>();
        }

        tmpBtn.onValueChanged.AddListener(action);
    }

    #endregion

    #region 给UI添加Button点击事件

    public void AddButtonListen(UnityAction action)
    {
        Button tmpBtn = transform.GetComponent<Button>();
        if (tmpBtn == null)
        {
            tmpBtn = gameObject.AddComponent<Button>();
        }

        tmpBtn.onClick.AddListener(action);
    }

    #endregion

    #region 给UI添加Slider滑动事件

    public void AddSliderListen(UnityAction<float> action)
    {
        Slider tmpSli = transform.GetComponent<Slider>();
        if (tmpSli == null)
        {
            tmpSli = gameObject.AddComponent<Slider>();
        }

        tmpSli.onValueChanged.AddListener(action);
    }

    #endregion

    #region 添加拖拽事件

    public void AddDrag(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Drag;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion

    #region 添加拖拽开始事件

    public void AddBeginDrag(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.BeginDrag;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion

    #region 添加结束拖拽事件

    public void AddEndDrag(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.EndDrag;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion

    #region 添加鼠标进入事件

    public void AddOnPointerEnterListen(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion

    #region 添加鼠标移出事件

    public void AddOnPointerExitListen(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerExit;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion

    #region 添加点击事件

    public void AddPointClick(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
    /// <summary>
    /// 移除指定事件类型的监听（只移除匹配 eventID 的条目，并清理空条目）
    /// </summary>
    public void RemoveListener(EventTriggerType eventID, UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            return;
        }
        for (int i = trigger.triggers.Count - 1; i >= 0; i--)
        {
            var entry = trigger.triggers[i];
            if (entry.eventID == eventID)
            {
                // 运行时监听数量无法统计，这里只移除回调，保留条目避免误删同类型的其他监听
                entry.callback.RemoveListener(action);
            }
        }
    }
    #endregion

    #region 添加点击按下事件

    public void AddPointClickDown(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion

    #region 添加点击抬起事件

    public void AddPointClickUP(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerUp;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion
    #region 添加鼠标滚轮事件

    public void AddScroll(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Scroll;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    #endregion
    public void AddOnSelectListen(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Select;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    public void AddUpdateSelectListen(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.UpdateSelected;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    public void AddOnDeSelectListen(UnityAction<BaseEventData> action)
    {
        EventTrigger trigger = gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.Deselect;
        entry.callback = new EventTrigger.TriggerEvent();
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}