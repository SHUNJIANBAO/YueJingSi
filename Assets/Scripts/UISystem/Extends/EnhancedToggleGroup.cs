using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System;
using UnityEngine.Serialization;

/// <summary>
/// 增强版Toggle Group，支持方向键导航和外部组件切换
/// 支持导航到其他EnhancedToggleGroup
/// </summary>
public class EnhancedToggleGroup : ToggleGroup
{
        // 是否循环导航
    [SerializeField]
    private bool wrapAround = true;

    [Header("垂直导航目标")] [Tooltip("上方导航目标（当按上方向键时）")] [SerializeField]
    private NavigationTarget navigationUpTarget = new NavigationTarget();

    [Tooltip("下方导航目标（当按下方向键时）")] [SerializeField]
    private NavigationTarget navigationDownTarget = new NavigationTarget();

    private List<Toggle> _toggleList = new List<Toggle>();
    private Dictionary<Toggle, int> _toggleIndices = new Dictionary<Toggle, int>();
    private bool _isInitialized = false;

    // 当前选中的Toggle
    private Toggle _currentSelectedToggle = null;

    protected override void Start()
    {
        base.Start();

        // 延迟一帧执行，确保所有Toggle都已注册
        if (gameObject.activeInHierarchy)
        {
            CancelInvoke(nameof(InitializeNavigation));
            Invoke(nameof(InitializeNavigation), 0.1f);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!_isInitialized && gameObject.activeInHierarchy)
        {
            CancelInvoke(nameof(InitializeNavigation));
            Invoke(nameof(InitializeNavigation), 0.1f);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        // 禁用时清理，同时丢掉尚未执行的延迟初始化
        CancelInvoke(nameof(InitializeNavigation));
        _isInitialized = false;
        _toggleList.Clear();
        _toggleIndices.Clear();
        _currentSelectedToggle = null;
    }

    private void InitializeNavigation()
    {
        if (_isInitialized) return;

        // 获取所有子Toggle
        GetComponentsInChildren(true, _toggleList);

        // 清除没有激活的Toggle
        _toggleList.RemoveAll(t => !t.isActiveAndEnabled);

        // 建立索引映射
        _toggleIndices.Clear();
        for (int i = 0; i < _toggleList.Count; i++)
        {
            Toggle toggle = _toggleList[i];
            _toggleIndices[toggle] = i;

            // 复用已有的事件触发器，重复挂载会让回调叠加
            var eventTrigger = toggle.GetComponent<EnhancedToggleEventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = toggle.gameObject.AddComponent<EnhancedToggleEventTrigger>();
            }

            eventTrigger.Initialize(this, toggle);

            // 添加选中状态监听
            //toggle.onValueChanged.AddListener((isOn) => OnToggleValueChanged(toggle, isOn));

            // 自动设置导航

            SetupToggleNavigation(toggle, i);
        }

        _isInitialized = true;
        Debug.Log($"[EnhancedToggleGroup] EnhancedToggleGroup 初始化完成，找到 {_toggleList.Count} 个Toggle");
    }

    private void SetupToggleNavigation(Toggle toggle, int index)
    {
        if (toggle == null) return;

        var nav = toggle.navigation;
        nav.mode = Navigation.Mode.Explicit;

        // 设置水平导航
        if (_toggleList.Count > 1)
        {
            nav.selectOnLeft = GetToggleAt(index - 1);
            nav.selectOnRight = GetToggleAt(index + 1);
        }
        else
        {
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
        }

        // 垂直导航：根据导航目标类型设置
        if (navigationUpTarget.targetType == NavigationTarget.TargetType.Selectable)
        {
            nav.selectOnUp = navigationUpTarget.selectableTarget;
        }
        else
        {
            nav.selectOnUp = null; // EnhancedToggleGroup目标需要通过事件处理
        }

        if (navigationDownTarget.targetType == NavigationTarget.TargetType.Selectable)
        {
            nav.selectOnDown = navigationDownTarget.selectableTarget;
        }
        else
        {
            nav.selectOnDown = null; // EnhancedToggleGroup目标需要通过事件处理
        }

        toggle.navigation = nav;
    }

    /// <summary>
    /// 处理垂直方向键导航
    /// </summary>
    public void HandleVerticalNavigation(bool isUpDirection)
    {
        NavigationTarget target = isUpDirection ? navigationUpTarget : navigationDownTarget;

        if (target.IsValid)
        {
            target.NavigateTo();
        }
    }

    /// <summary>
    /// 获取指定索引的Toggle
    /// </summary>
    private Toggle GetToggleAt(int index)
    {
        if (_toggleList.Count <= 1) return null; // 只有一个Toggle时不需要导航

        if (wrapAround)
        {
            // 循环索引
            if (index < 0) index = _toggleList.Count - 1;
            if (index >= _toggleList.Count) index = 0;
            return _toggleList[index];
        }
        else
        {
            // 不循环
            if (index >= 0 && index < _toggleList.Count)
                return _toggleList[index];
            return null;
        }
    }

    /// <summary>
    /// 导航到相邻Toggle
    /// </summary>
    public void Navigate(int direction)
    {
        if (_toggleList.Count <= 1) return; // 只有一个Toggle时不需要导航

        Toggle currentToggle = GetCurrentSelectedToggle();

        // 如果没有选中的，选择默认或第一个
        if (currentToggle == null)
        {
            SelectDefaultToggle();
            return;
        }

        // 获取当前索引并计算新索引
        if (_toggleIndices.TryGetValue(currentToggle, out int currentIndex))
        {
            int newIndex = currentIndex + direction;
            Toggle newToggle = GetToggleAt(newIndex);

            if (newToggle != null && newToggle != currentToggle)
            {
                SelectToggle(newToggle);
            }
        }
    }

    /// <summary>
    /// 从其他组件切换过来时的专用方法
    /// </summary>
    public void EnterFromOtherComponent()
    {
        if (_toggleList.Count == 0) return;

        Toggle targetToggle = null;

        targetToggle = GetActiveToggle();


        if (targetToggle != null)
        {
            SelectToggle(targetToggle);
        }
    }


    /// <summary>
    /// 导航到左侧Toggle
    /// </summary>
    public void NavigateLeft()
    {
        if (_toggleList.Count > 1)
        {
            Navigate(-1);
        }
    }

    /// <summary>
    /// 导航到右侧Toggle
    /// </summary>
    public void NavigateRight()
    {
        if (_toggleList.Count > 1)
        {
            Navigate(1);
        }
    }


    /// <summary>
    /// 选中指定Toggle
    /// </summary>
    private void SelectToggle(Toggle toggle)
    {
        if (toggle != null && toggle.isActiveAndEnabled)
        {
            toggle.isOn = true;
            _currentSelectedToggle = toggle;

            // 确保Toggle被选中（UI状态）
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(toggle.gameObject);

                // 触发Select事件（为了触发SelectionIndicator）
                var pointer = new BaseEventData(EventSystem.current);
                ExecuteEvents.Execute(toggle.gameObject, pointer, ExecuteEvents.selectHandler);
            }
        }
    }

    /// <summary>
    /// 获取当前选中的Toggle
    /// </summary>
    private Toggle GetCurrentSelectedToggle()
    {
        // 首先使用缓存的当前选中
        if (_currentSelectedToggle != null && _currentSelectedToggle.isActiveAndEnabled)
        {
            return _currentSelectedToggle;
        }

        // 然后检查EventSystem的当前选中
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            Toggle selectedToggle = EventSystem.current.currentSelectedGameObject.GetComponent<Toggle>();
            if (selectedToggle != null && _toggleIndices.ContainsKey(selectedToggle))
            {
                _currentSelectedToggle = selectedToggle;
                return selectedToggle;
            }
        }

        // 最后检查isOn的Toggle
        foreach (var toggle in _toggleList)
        {
            if (toggle.isOn)
            {
                _currentSelectedToggle = toggle;
                return toggle;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取激活的Toggle（isOn为true）
    /// </summary>
    public Toggle GetActiveToggle()
    {
        return GetCurrentSelectedToggle();
    }

    /// <summary>
    /// 选择默认Toggle
    /// </summary>
    private void SelectDefaultToggle()
    {
        if (_toggleList.Count > 0)
        {
            SelectToggle(_toggleList[0]);
        }
    }

    /// <summary>
    /// 设置垂直导航目标
    /// </summary>
    public void SetVerticalNavigationTargets(NavigationTarget upTarget, NavigationTarget downTarget)
    {
        navigationUpTarget = upTarget;
        navigationDownTarget = downTarget;

        // 更新所有Toggle的导航设置
        if (_isInitialized)
        {
            for (int i = 0; i < _toggleList.Count; i++)
            {
                SetupToggleNavigation(_toggleList[i], i);
            }
        }
    }

    /// <summary>
    /// 设置垂直导航目标（Selectable版本）
    /// </summary>
    public void SetVerticalNavigationTargets(Selectable upTarget, Selectable downTarget)
    {
        navigationUpTarget = new NavigationTarget
        {
            targetType = NavigationTarget.TargetType.Selectable,
            selectableTarget = upTarget
        };

        navigationDownTarget = new NavigationTarget
        {
            targetType = NavigationTarget.TargetType.Selectable,
            selectableTarget = downTarget
        };

        // 更新所有Toggle的导航设置
        if (_isInitialized)
        {
            for (int i = 0; i < _toggleList.Count; i++)
            {
                SetupToggleNavigation(_toggleList[i], i);
            }
        }
    }

    /// <summary>
    /// 手动刷新Toggle列表
    /// </summary>
    public void RefreshToggleList()
    {
        _isInitialized = false;
        InitializeNavigation();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // 在编辑器中预览时刷新
        if (Application.isPlaying && _isInitialized)
        {
            RefreshToggleList();
        }
    }

#endif
}

[System.Serializable]
public class NavigationTarget
{
    public enum TargetType
    {
        None,
        Selectable,
        EnhancedToggleGroup
    }

    [Tooltip("目标类型")] public TargetType targetType = TargetType.None;

    [Tooltip("普通的Selectable目标")] public Selectable selectableTarget;

    [Tooltip("EnhancedToggleGroup目标")] public EnhancedToggleGroup toggleGroupTarget;

    /// <summary>
    /// 导航到这个目标
    /// </summary>
    public void NavigateTo()
    {
        switch (targetType)
        {
            case TargetType.Selectable:
                if (selectableTarget != null && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(selectableTarget.gameObject);
                }

                break;
            case TargetType.EnhancedToggleGroup:
                if (toggleGroupTarget != null)
                {
                    toggleGroupTarget.EnterFromOtherComponent();
                }

                break;
        }
    }

    /// <summary>
    /// 检查目标是否有效
    /// </summary>
    public bool IsValid
    {
        get
        {
            switch (targetType)
            {
                case TargetType.Selectable:
                    return selectableTarget != null && selectableTarget.isActiveAndEnabled;
                case TargetType.EnhancedToggleGroup:
                    return toggleGroupTarget != null;
                default:
                    return false;
            }
        }
    }
}