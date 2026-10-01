using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public abstract class UIBase : MonoBehaviour
{
    private RectTransform m_RectTransform;

    public RectTransform rectTransform
    {
        get
        {
            if (m_RectTransform == null)
            {
                m_RectTransform = GetComponent<RectTransform>();
            }

            return m_RectTransform;
        }
    }

    private CanvasGroup _canvasGroup;

    protected CanvasGroup m_CanvasGroup
    {
        get
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            return _canvasGroup;
        }
    }

    protected Dictionary<string, List<GameObject>> m_UiDict;

    // UI 节点缓存，编辑器下每次 Awake 都与实际子节点结构比对后刷新
    [SerializeField] private List<Transform> _uiList = new List<Transform>();

    /// <summary>
    /// 生命周期入口：派生类如需自定义 Awake，必须调用 base.Awake()，否则节点字典不会建立。
    /// </summary>
    protected virtual void Awake()
    {
#if UNITY_EDITOR
        var uiList = new List<Transform>(transform.GetComponentsInChildren<Transform>(true));
        if (uiList.Count != _uiList.Count)
        {
            _uiList = uiList;
        }
        else
        {
            for (int i = 0; i < uiList.Count; i++)
            {
                try
                {
                    if (_uiList[i] == null || uiList[i].name != _uiList[i].name)
                    {
                        _uiList = uiList;
                        break;
                    }
                }
                catch (System.Exception e)
                {
                    // 节点结构不一致时不打断 Awake，仅记录后由编辑器重刷
                    Debug.LogWarning($"[UIBase] 同步UI节点缓存失败，节点:{name}，原因:{e.Message}");
                }
            }
        }
#endif
        m_UiDict = LoadAllUI();
        GetUIComponents();
        OnInit();
    }

    /// <summary>
    /// 加载所有子物体
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, List<GameObject>> LoadAllUI()
    {
        Dictionary<string, List<GameObject>> uiDict = new Dictionary<string, List<GameObject>>();
        for (int i = 1; i < _uiList.Count; i++)
        {
            // 序列化缓存里可能残留已删除节点的空引用，跳过避免中断整个字典构建
            if (_uiList[i] == null) continue;

            if (!uiDict.ContainsKey(_uiList[i].name))
            {
                List<GameObject> tempList = new List<GameObject>();
                uiDict.Add(_uiList[i].name, tempList);
            }

            uiDict[_uiList[i].name].Add(_uiList[i].gameObject);
        }

        return uiDict;
    }


    /// <summary>
    /// 启用时绑定 UI 事件；派生类覆写时必须调用 base.OnEnable。
    /// </summary>
    protected virtual void OnEnable()
    {
        AddUIListeners();
    }

    /// <summary>
    /// 停用时解绑 UI 事件；派生类覆写时必须调用 base.OnDisable。
    /// </summary>
    protected virtual void OnDisable()
    {
        RemoveUIListeners();
    }

    protected virtual void GetUIComponents()
    {
    }

    protected virtual void AddUIListeners()
    {
    }
    protected virtual void RemoveUIListeners()
    {
    }

    protected virtual void OnInit()
    {
    }

    /// <summary>
    /// 立即显示，仅恢复透明度；需要同时恢复交互的面板在派生类里重写。
    /// </summary>
    public virtual void QuickShow()
    {
        m_CanvasGroup.alpha = 1;
    }

    /// <summary>
    /// 立即隐藏，仅清零透明度；需要同时屏蔽交互的面板在派生类里重写。
    /// </summary>
    public virtual void QuickHide()
    {
        m_CanvasGroup.alpha = 0;
    }

    public virtual void Refresh(params object[] args)
    {
    }

    #region 事件接口

    #region 获取UI

    public virtual T GetUI<T>(string uiName) where T : Object
    {
        if (m_UiDict.TryGetValue(uiName, out List<GameObject> uiList))
        {
            if (typeof(T) == typeof(GameObject))
            {
                return uiList[0] as T;
            }

            if (typeof(T).IsSubclassOf(typeof(Component)))
            {
                return uiList[0].GetComponent<T>();
            }
        }

        return null;
    }

    public virtual List<T> GetUIList<T>(string uiName) where T : Object
    {
        if (m_UiDict.TryGetValue(uiName, out List<GameObject> uiList))
        {
            if (typeof(T) == typeof(GameObject))
            {
                return uiList as List<T>;
            }

            if (typeof(T).IsSubclassOf(typeof(Component)))
            {
                List<T> coms = new List<T>();
                foreach (var ui in uiList)
                {
                    coms.Add(ui.GetComponent<T>());
                }

                return coms;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取UIAction事件类
    /// </summary>
    /// <param name="uiName"></param>
    /// <returns></returns>
    /// <summary>
    /// 获取指定子节点上的 UIAction 事件组件，缺失时动态补挂。
    /// </summary>
    /// <param name="uiName">子节点名。</param>
    /// <returns>节点上的 UIAction 组件；节点不存在时返回 null。</returns>
    UIAction GetBehaviour(string uiName)
    {
        UIAction be = GetUI<UIAction>(uiName);
        if (be != null)
        {
            return be;
        }

        GameObject uiGo = GetUI<GameObject>(uiName);
        if (uiGo == null)
        {
            Debug.LogError($"[UIBase] 节点不存在，无法挂载事件组件，节点名:{uiName}");
            return null;
        }

        return uiGo.AddComponent<UIAction>();
    }

    #endregion

    #region 添加事件

    /// <summary>
    /// 添加Toggle点击事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    protected void AddToggleListen(string uiName, UnityAction<bool> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddToggleListen(action);
    }

    /// <summary>
    /// 添加 Toggle 值变更事件。
    /// </summary>
    /// <param name="toggle">目标 Toggle，为空时忽略。</param>
    /// <param name="action">值变更回调。</param>
    protected void AddToggleListen(Toggle toggle, UnityAction<bool> action)
    {
        if (toggle == null || action == null) return;
        toggle.onValueChanged.AddListener(action);
    }

    /// <summary>
    /// 移除 Toggle 值变更事件。
    /// </summary>
    /// <param name="toggle">目标 Toggle，为空时忽略。</param>
    /// <param name="action">待移除的回调。</param>
    protected void RemoveToggleListen(Toggle toggle, UnityAction<bool> action)
    {
        if (toggle == null || action == null) return;
        toggle.onValueChanged.RemoveListener(action);
    }
    /// <summary>
    /// 添加Button点击事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    protected void AddButtonListen(string uiName, UnityAction action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddButtonListen(action);
    }

    /// <summary>
    /// 添加 Button 点击事件。
    /// </summary>
    /// <param name="btn">目标 Button，为空时忽略。</param>
    /// <param name="action">点击回调。</param>
    protected void AddButtonListen(Button btn, UnityAction action)
    {
        if (btn == null || action == null) return;
        btn.onClick.AddListener(action);
    }

    /// <summary>
    /// 移除 Button 点击事件。
    /// </summary>
    /// <param name="btn">目标 Button，为空时忽略。</param>
    /// <param name="action">待移除的回调。</param>
    protected void RemoveButtonListen(Button btn, UnityAction action)
    {
        if (btn == null || action == null) return;
        btn.onClick.RemoveListener(action);
    }

    /// <summary>
    /// 添加Slider滑动事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    protected void AddSliderListen(string uiName, UnityAction<float> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddSliderListen(action);
    }

    /// <summary>
    /// 添加 Slider 值变更事件。
    /// </summary>
    /// <param name="slider">目标 Slider，为空时忽略。</param>
    /// <param name="action">值变更回调。</param>
    protected void AddSliderListen(Slider slider, UnityAction<float> action)
    {
        if (slider == null || action == null) return;
        slider.onValueChanged.AddListener(action);
    }

    /// <summary>
    /// 添加点击事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddPointClick(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddPointClick(action);
    }

    /// <summary>
    /// 添加点击事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddPointClick(UIAction ui, UnityAction<BaseEventData> action)
    {
        ui.AddPointClick(action);
    }

    public void RemoveListener(UIAction ui, EventTriggerType eventID, UnityAction<BaseEventData> action)
    {
        ui.RemoveListener(eventID, action);
    }

    /// <summary>
    /// 添加点击按下事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddPointClickDown(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddPointClickDown(action);
    }
    /// <summary>
    /// 添加点击按下事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddPointClickDown(UIAction ui, UnityAction<BaseEventData> action)
    {
        ui.AddPointClickDown(action);
    }

    /// <summary>
    /// 添加点击抬起事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddPointClickUP(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddPointClickUP(action);
    }

    /// <summary>
    /// 添加拖拽事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddDrag(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        AddDrag(uiBehaviour, action);
    }
    /// <summary>
    /// 添加拖拽事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddDrag(UIAction uiBehaviour, UnityAction<BaseEventData> action)
    {
        uiBehaviour?.AddDrag(action);
    }
    /// <summary>
    /// 添加拖拽开始事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddBeginDrag(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        AddBeginDrag(uiBehaviour, action);
    }
    /// <summary>
    /// 添加拖拽开始事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddBeginDrag(UIAction uiBehaviour, UnityAction<BaseEventData> action)
    {
        uiBehaviour?.AddBeginDrag(action);
    }
    /// <summary>
    /// 添加拖拽结束事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddEndDrag(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        AddEndDrag(uiBehaviour, action);
    }
    /// <summary>
    /// 添加拖拽结束事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddEndDrag(UIAction uiBehaviour, UnityAction<BaseEventData> action)
    {
        uiBehaviour?.AddEndDrag(action);
    }
    /// <summary>
    /// 添加鼠标滚轮事件
    /// </summary>
    /// <param name="uiName"></param>
    /// <param name="action"></param>
    public void AddScroll(UIAction uiBehaviour, UnityAction<BaseEventData> action)
    {
        uiBehaviour?.AddScroll(action);
    }
    //public void AddOnSelectListen(string uiName,UnityAction<BaseEventData> action)
    //{
    //    UIAction uiBehaviour = GetBehaviour(uiName);
    //    uiBehaviour?.AddOnSelectListen(action);
    //}

    public void AddOnSelectListen(Selectable ui, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(ui.name);
        uiBehaviour?.AddOnSelectListen(action);
    }

    public void AddUpdateSelectListen(Selectable ui, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(ui.name);
        uiBehaviour?.AddUpdateSelectListen(action);
    }

    public void AddOnDeSelectListen(Selectable ui, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(ui.name);
        uiBehaviour?.AddOnDeSelectListen(action);
    }

    public void AddOnPointerEnterListen(UIAction ui, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(ui.name);
        uiBehaviour?.AddOnPointerEnterListen(action);
    }

    public void AddOnPointerEnterListen(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddOnPointerEnterListen(action);
    }

    public void AddOnPointerExitListen(UIAction ui, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(ui.name);
        uiBehaviour?.AddOnPointerExitListen(action);
    }

    public void AddOnPointerExitListen(string uiName, UnityAction<BaseEventData> action)
    {
        UIAction uiBehaviour = GetBehaviour(uiName);
        uiBehaviour?.AddOnPointerExitListen(action);
    }

    #endregion

    #endregion
}