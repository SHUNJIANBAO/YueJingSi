using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIHandle
{

    public static void ShowTips(string tips)
    {
        if (UIManager.Instance.IsOpen<UITipsPanel>())
        {
            // 如果面板已经打开，直接向现有面板发送消息
            var panel = UIManager.Instance.GetPanel<UITipsPanel>();
            panel.CreateSingleTip(tips);
        }
        else
        {
            UIManager.Instance.OpenPanel<UITipsPanel>(false, null, tips);
        }
    }

    public static void ShowTipsList(List<string> tipsList)
    {
        if (UIManager.Instance.IsOpen<UITipsPanel>())
        {
            // 如果面板已经打开，直接向现有面板发送消息
            var panel = UIManager.Instance.GetPanel<UITipsPanel>();
            panel.CreateTipsList(tipsList);
        }
        else
        {
            UIManager.Instance.OpenPanel<UITipsPanel>(false, null, tipsList);
        }
    }

}
