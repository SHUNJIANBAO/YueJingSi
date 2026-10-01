using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;

/// <summary>
/// 为UI元素添加选中指示器
/// 可以附加到任何UI元素上，当元素被选中时显示指定的图片
/// 支持在ScrollRect中自动滚动到可见位置
/// </summary>
[RequireComponent(typeof(Selectable))]
public class SelectionIndicator : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("指示器设置")] [Tooltip("作为选中指示器的图片")] [SerializeField]
    private Image indicatorImage;

    [Tooltip("选中时的图片颜色")] [SerializeField] private Color selectedColor = Color.white;

    [Tooltip("是否在选中时播放声音")] [SerializeField]
    private bool playSoundOnSelect = false;

    [Header("声音设置")] [SerializeField] private AudioClip selectSound;

    [Tooltip("滚动时的动画持续时间（秒）")] [SerializeField]
    private float scrollDuration = 0.3f;

    [Tooltip("元素在视图中需要保持的最小边距（0-0.5）")] [SerializeField]
    private float viewportMargin = 0.1f;

    [Tooltip("如果找不到ScrollRect，自动向上查找")] [SerializeField]
    private bool autoFindScrollRect = true;

    private Selectable selectable;
    private AudioSource audioSource;
    private CanvasGroup indicatorCanvasGroup;
    private ScrollRect scrollRect;
    private RectTransform scrollContent;
    private RectTransform viewportRect;
    private bool isInitialized = false;
    private Coroutine currentAnimation;

    // 滚动到可见位置的协程
    private Coroutine scrollCoroutine;

    // 平滑滚动协程，独立持有句柄，避免滚动中启动新滚动时丢掉句柄
    private Coroutine smoothScrollCoroutine;

    // 运行时生成的默认指示器贴图，多个实例共用一份
    private static Texture2D s_DefaultTexture;

    // 运行时生成的默认指示器精灵，多个实例共用一份
    private static Sprite s_DefaultSprite;
    private Canvas canvas;
    private Canvas rootCanvas;

    /// <summary>
    /// 当前是否选中状态
    /// </summary>
    public bool IsSelected { get; private set; }

    /// <summary>
    /// 选中状态变化事件
    /// </summary>
    public event Action<bool> OnSelectionChanged;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        var scroll = GetComponentInParent<ScrollRect>();
        SetScrollRect(scroll);
        canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas?.rootCanvas;
    }

    private void Initialize()
    {
        if (isInitialized) return;

        selectable = GetComponent<Selectable>();

        // 如果还没有设置指示器图片，尝试从子物体中查找
        if (indicatorImage == null)
        {
            indicatorImage = GetComponentInChildren<Image>(true);
            if (indicatorImage != null && indicatorImage.transform != transform)
            {
                // 确保找到的图片不是组件自身（对于Slider等）
                if (indicatorImage.GetComponent<SelectionIndicator>() != null)
                {
                    indicatorImage = null;
                }
            }
        }

        // 如果仍然没有图片，创建一个新的
        if (indicatorImage == null)
        {
            CreateDefaultIndicator();
        }

        // 添加或获取CanvasGroup用于淡入淡出
        indicatorCanvasGroup = indicatorImage.GetComponent<CanvasGroup>();
        if (indicatorCanvasGroup == null)
        {
            indicatorCanvasGroup = indicatorImage.gameObject.AddComponent<CanvasGroup>();
        }


        FindScrollRect();

        // 确保初始状态正确
        SetIndicatorActive(false, true);

        // 添加声音组件
        if (playSoundOnSelect)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        isInitialized = true;
    }

    private void FindScrollRect()
    {
        scrollRect = null;

        if (autoFindScrollRect)
        {
            // 向上查找ScrollRect
            Transform parent = transform.parent;
            while (parent != null && scrollRect == null)
            {
                scrollRect = parent.GetComponent<ScrollRect>();
                parent = parent.parent;
            }
        }

        if (scrollRect != null)
        {
            scrollContent = scrollRect.content;
            viewportRect = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        }
        else
        {
            Debug.LogWarning($"[SelectionIndicator] SelectionIndicator on {gameObject.name}: 未找到ScrollRect，滚动到视图功能将禁用。", this);
        }
    }

    private void CreateDefaultIndicator()
    {
        GameObject indicatorObj = new GameObject("SelectionIndicator");
        indicatorImage = indicatorObj.AddComponent<Image>();

        // 尝试创建默认的选中框纹理
        CreateDefaultTexture();

        RectTransform rt = indicatorObj.GetComponent<RectTransform>();
        rt.SetParent(transform);
        rt.localPosition = Vector3.zero;
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(40, 40);
    }

    private void CreateDefaultTexture()
    {
        // 默认纹理只在首次使用时生成并全局共享，避免每个实例各泄漏一份贴图
        if (s_DefaultSprite == null)
        {
            s_DefaultTexture = new Texture2D(64, 64);

            for (int y = 0; y < s_DefaultTexture.height; y++)
            {
                for (int x = 0; x < s_DefaultTexture.width; x++)
                {
                    bool isBorder = x < 2 || x >= s_DefaultTexture.width - 2 ||
                                    y < 2 || y >= s_DefaultTexture.height - 2;
                    s_DefaultTexture.SetPixel(x, y, isBorder ? Color.white : Color.clear);
                }
            }

            s_DefaultTexture.Apply();
            s_DefaultSprite = Sprite.Create(s_DefaultTexture,
                new Rect(0, 0, s_DefaultTexture.width, s_DefaultTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        indicatorImage.sprite = s_DefaultSprite;
        indicatorImage.type = Image.Type.Sliced;
    }

    private void OnEnable()
    {
        if (!isInitialized) return;

        // 检查当前是否已经选中（对于通过代码设置选中的情况）
        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            OnSelect(null);
        }
        else
        {
            SetIndicatorActive(false, true);
        }
    }

    private void OnDisable()
    {
        // 禁用时停止所有动画
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }

        if (scrollCoroutine != null)
        {
            StopCoroutine(scrollCoroutine);
            scrollCoroutine = null;
        }

        if (smoothScrollCoroutine != null)
        {
            StopCoroutine(smoothScrollCoroutine);
            smoothScrollCoroutine = null;
        }

        SetIndicatorActive(false, true);
    }

    public void OnSelect(BaseEventData eventData)
    {
        IsSelected = true;
        SetIndicatorActive(true);

        // 播放声音
        if (playSoundOnSelect && selectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(selectSound);
        }

        // 如果需要且支持滚动，滚动到视图
        if (scrollRect != null)
        {
            ScrollToView();
        }

        OnSelectionChanged?.Invoke(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        IsSelected = false;
        SetIndicatorActive(false);
        OnSelectionChanged?.Invoke(false);
    }

    /// <summary>
    /// 检查元素是否在视图内（考虑边距）
    /// </summary>
    private bool IsElementInViewWithMargin()
    {
        if (scrollRect == null || scrollContent == null || viewportRect == null)
            return true;

        RectTransform elementRect = GetComponent<RectTransform>();
        if (elementRect == null)
            return true;

        // 将元素的四个角转换到视口空间
        Vector3[] elementCorners = new Vector3[4];
        elementRect.GetWorldCorners(elementCorners);
        
        Vector3[] viewportCorners = new Vector3[4];
        viewportRect.GetWorldCorners(viewportCorners);

        // 对于垂直滚动列表
        if (scrollRect.vertical)
        {
            // 只检查Y轴
            float elementTop = elementCorners[1].y; // 左上角
            float elementBottom = elementCorners[0].y; // 左下角
            float viewportTop = viewportCorners[1].y;
            float viewportBottom = viewportCorners[0].y;
            
            float margin = (viewportTop - viewportBottom) * viewportMargin;
            
            // 检查元素是否完全在视图内（考虑边距）
            return elementBottom >= viewportBottom + margin && elementTop <= viewportTop - margin;
        }
        
        // 对于水平滚动列表
        if (scrollRect.horizontal)
        {
            // 只检查X轴
            float elementRight = elementCorners[2].x; // 右下角
            float elementLeft = elementCorners[0].x; // 左下角
            float viewportRight = viewportCorners[2].x;
            float viewportLeft = viewportCorners[0].x;
            
            float margin = (viewportRight - viewportLeft) * viewportMargin;
            
            // 检查元素是否完全在视图内（考虑边距）
            return elementLeft >= viewportLeft + margin && elementRight <= viewportRight - margin;
        }
        
        return true;
    }

    /// <summary>
    /// 滚动当前元素到视图中（边缘对齐）
    /// </summary>
    private void ScrollToView()
    {
        // 如果已经在视图内，不需要滚动
        if (IsElementInViewWithMargin())
        {
            return;
        }

        if (scrollCoroutine != null)
        {
            StopCoroutine(scrollCoroutine);
        }

        scrollCoroutine = StartCoroutine(ScrollToViewCoroutine());
    }

    private IEnumerator ScrollToViewCoroutine()
    {
        if (scrollRect == null || scrollContent == null || viewportRect == null)
            yield break;

        // 获取元素的RectTransform
        RectTransform elementRect = GetComponent<RectTransform>();
        if (elementRect == null)
            yield break;

        // 对于使用Layout Group的情况，强制重新计算布局
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
        
        // 等待一帧确保布局已经计算完成
        yield return null;
        
        // 再次强制重新计算布局，确保Grid Layout计算完成
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
        yield return null;

        // 获取Content在Canvas中的锚点位置
        Vector2 contentPivot = scrollContent.pivot;
        Vector2 contentSize = scrollContent.rect.size;
        
        // 计算元素相对于Content的位置（考虑锚点和轴心）
        Vector3 elementLocalPos = elementRect.localPosition;
        Vector3 elementSize = elementRect.rect.size;
        
        // 计算元素在Content坐标系中的边界
        // 对于垂直滚动：需要考虑Content的pivot位置
        if (scrollRect.vertical)
        {
            // 垂直滚动时，Y轴正方向通常是向上的
            float elementTop = elementLocalPos.y + elementSize.y * (1 - elementRect.pivot.y);
            float elementBottom = elementLocalPos.y - elementSize.y * elementRect.pivot.y;
            
            // 调整到Content的坐标系（考虑pivot）
            float contentTop = contentSize.y * (1 - contentPivot.y);
            float contentBottom = -contentSize.y * contentPivot.y;
            
            // 计算视口在Content坐标系中的边界
            float viewportHeight = viewportRect.rect.height;
            float currentScrollPos = scrollRect.verticalNormalizedPosition;
            
            // 视口在Content坐标系中的位置
            float viewportTop = Mathf.Lerp(contentTop - viewportHeight, contentBottom, currentScrollPos);
            float viewportBottom = viewportTop + viewportHeight;
            
            float margin = viewportHeight * viewportMargin;
            
            // 判断滚动方向并计算目标位置
            float targetNormalizedPosition = currentScrollPos;
            
            // 检查元素是在视图上方还是下方
            if (elementTop > viewportTop - margin)
            {
                // 元素在视图上方（需要向下滚动）
                // 将元素的底部与视口顶部对齐（考虑边距）
                float requiredViewportTop = elementTop + margin;
                targetNormalizedPosition = Mathf.InverseLerp(contentBottom, contentTop - viewportHeight, requiredViewportTop - viewportHeight);
            }
            else if (elementBottom < viewportBottom + margin)
            {
                // 元素在视图下方（需要向上滚动）
                // 将元素的顶部与视口底部对齐（考虑边距）
                float requiredViewportBottom = elementBottom - margin;
                targetNormalizedPosition = Mathf.InverseLerp(contentBottom, contentTop - viewportHeight, requiredViewportBottom);
            }
            
            // 只有在需要滚动时才执行滚动动画
            if (Mathf.Abs(targetNormalizedPosition - currentScrollPos) > 0.001f)
            {
                StartSmoothScroll(targetNormalizedPosition, true);
            }
        }
        else if (scrollRect.horizontal)
        {
            // 水平滚动时，X轴正方向通常是向右的
            float elementRight = elementLocalPos.x + elementSize.x * (1 - elementRect.pivot.x);
            float elementLeft = elementLocalPos.x - elementSize.x * elementRect.pivot.x;
            
            // 调整到Content的坐标系（考虑pivot）
            float contentRight = contentSize.x * (1 - contentPivot.x);
            float contentLeft = -contentSize.x * contentPivot.x;
            
            // 计算视口在Content坐标系中的边界
            float viewportWidth = viewportRect.rect.width;
            float currentScrollPos = scrollRect.horizontalNormalizedPosition;
            
            // 视口在Content坐标系中的位置
            float viewportRight = Mathf.Lerp(contentRight - viewportWidth, contentLeft, currentScrollPos);
            float viewportLeft = viewportRight + viewportWidth;
            
            float margin = viewportWidth * viewportMargin;
            
            // 判断滚动方向并计算目标位置
            float targetNormalizedPosition = currentScrollPos;
            
            // 检查元素是在视图左侧还是右侧
            if (elementRight > viewportRight - margin)
            {
                // 元素在视图右侧（需要向左滚动）
                // 将元素的左侧与视口右侧对齐（考虑边距）
                float requiredViewportRight = elementRight + margin;
                targetNormalizedPosition = Mathf.InverseLerp(contentLeft, contentRight - viewportWidth, requiredViewportRight - viewportWidth);
            }
            else if (elementLeft < viewportLeft + margin)
            {
                // 元素在视图左侧（需要向右滚动）
                // 将元素的右侧与视口左侧对齐（考虑边距）
                float requiredViewportLeft = elementLeft - margin;
                targetNormalizedPosition = Mathf.InverseLerp(contentLeft, contentRight - viewportWidth, requiredViewportLeft);
            }
            
            // 只有在需要滚动时才执行滚动动画
            if (Mathf.Abs(targetNormalizedPosition - currentScrollPos) > 0.001f)
            {
                StartSmoothScroll(targetNormalizedPosition, false);
            }
        }
        
        scrollCoroutine = null;
    }
    
    private void StartSmoothScroll(float targetNormalizedPos, bool isVertical)
    {
        // 平滑滚动使用独立句柄，避免停止外层滚动协程时把自身句柄清空
        if (smoothScrollCoroutine != null)
        {
            StopCoroutine(smoothScrollCoroutine);
        }

        smoothScrollCoroutine = StartCoroutine(SmoothScrollCoroutine(targetNormalizedPos, isVertical));
    }
    
    private IEnumerator SmoothScrollCoroutine(float targetNormalizedPos, bool isVertical)
    {
        float startValue = isVertical ? scrollRect.verticalNormalizedPosition : scrollRect.horizontalNormalizedPosition;
        float elapsedTime = 0f;
        
        while (elapsedTime < scrollDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / scrollDuration);
            
            // 使用平滑的缓动函数
            t = 1f - Mathf.Pow(1f - t, 3); // 三次方缓动
            
            float currentValue = Mathf.Lerp(startValue, targetNormalizedPos, t);
            
            if (isVertical)
            {
                scrollRect.verticalNormalizedPosition = Mathf.Clamp01(currentValue);
            }
            else
            {
                scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(currentValue);
            }
            
            yield return null;
        }
        
        // 确保最终值
        if (isVertical)
        {
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(targetNormalizedPos);
        }
        else
        {
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(targetNormalizedPos);
        }
    }

    private void SetIndicatorActive(bool active, bool immediate = false)
    {
        if (indicatorImage == null) return;

        indicatorImage.color = selectedColor;

        // 立即设置
        if (active)
        {
            indicatorCanvasGroup.alpha = 1f;
        }
        else
        {
            indicatorCanvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// 手动设置ScrollRect（如果自动查找失败）
    /// </summary>
    public void SetScrollRect(ScrollRect rect)
    {
        scrollRect = rect;
        if (scrollRect != null)
        {
            scrollContent = scrollRect.content;
            viewportRect = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        }
    }

#if UNITY_EDITOR

    private void Reset()
    {
        // 重置时自动设置默认值
        selectedColor = new Color(1f, 1f, 1f, 0.8f);
    }

    [ContextMenu("查找并设置ScrollRect")]
    private void EditorFindScrollRect()
    {
        FindScrollRect();
        if (scrollRect != null)
        {
            Debug.Log($"[SelectionIndicator] 已找到ScrollRect: {scrollRect.gameObject.name}", scrollRect);
        }
    }
    
    [ContextMenu("测试检查视图")]
    private void EditorTestCheckView()
    {
        if (scrollRect != null)
        {
            bool isInView = IsElementInViewWithMargin();
            Debug.Log($"[SelectionIndicator] 元素是否在视图内: {isInView}", this);
        }
    }
#endif
}