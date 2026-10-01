using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 通用提示面板，支持单条提示与列表提示两种弹出方式。
/// </summary>
public class UITipsPanel : UIPanelBase
{
    // 提示动画总时长
    private const float ANIMATION_DURATION = 2f;

    // 列表提示的垂直间距
    private const float TIP_SPACING = 120f;

    // 提示根节点
    private RectTransform _root;

    // 提示模板节点
    private GameObject _tipTemplate;

    // 单条提示的存活数量
    private int _singleTipCount = 0;

    // 列表提示的存活数量
    private int _activeListCount = 0;

    // 列表提示分组，键为列表编号
    private readonly Dictionary<int, List<GameObject>> _listTips = new Dictionary<int, List<GameObject>>();

    // 下一个可用的列表编号
    private int _nextListId = 0;

    // 运行中的提示动画，面板关闭时统一终止，避免回调访问已清空的数据
    private readonly List<Sequence> _runningSequences = new List<Sequence>();

    /// <summary>
    /// 获取并缓存面板内 UI 组件引用。
    /// </summary>
    protected override void GetUIComponents()
    {
        _root = GetUI<RectTransform>("Root");
        _tipTemplate = GetUI<GameObject>("Go_TipBg");
        if (_root == null || _tipTemplate == null)
        {
            Debug.LogError($"[UITipsPanel] 提示节点缺失，节点名:{name}");
            return;
        }

        _tipTemplate.SetActive(false);
    }

    /// <summary>
    /// 打开面板时按传入参数创建提示。
    /// </summary>
    /// <param name="args">首个参数为单条提示文本或提示文本列表。</param>
    protected override void OnOpen(params object[] args)
    {
        base.OnOpen(args);

        if (args.Length == 0) return;

        if (args[0] is string singleTip)
        {
            CreateSingleTip(singleTip);
        }
        else if (args[0] is List<string> tipsList)
        {
            CreateTipsList(tipsList);
        }
    }

    #region 单个提示功能

    /// <summary>
    /// 创建一条单条提示，播放完毕后自动销毁。
    /// </summary>
    /// <param name="tips">提示文本。</param>
    public void CreateSingleTip(string tips)
    {
        if (!CreateTipNode(tips, out GameObject tip, out RectTransform tipRect, out CanvasGroup canvasGroup)) return;

        _singleTipCount++;
        Sequence sequence = BuildAnimation(tipRect, canvasGroup, 0f);
        _runningSequences.Add(sequence);
        sequence.OnComplete(() =>
        {
            _runningSequences.Remove(sequence);
            Destroy(tip);
            _singleTipCount--;
            CheckClosePanel();
        });

        sequence.Play();
    }

    #endregion

    #region 列表提示功能

    /// <summary>
    /// 创建一组列表提示，按序号自上而下排列。
    /// </summary>
    /// <param name="tipsList">提示文本列表。</param>
    public void CreateTipsList(List<string> tipsList)
    {
        if (tipsList == null || tipsList.Count == 0) return;

        int listId = _nextListId++;
        _activeListCount++;
        _listTips[listId] = new List<GameObject>();

        for (int i = 0; i < tipsList.Count; i++)
        {
            CreateListTip(tipsList[i], listId, i);
        }
    }

    /// <summary>
    /// 创建列表提示中的单条提示。
    /// </summary>
    /// <param name="tip">提示文本。</param>
    /// <param name="listId">所属列表编号。</param>
    /// <param name="tipIndex">在列表中的序号。</param>
    private void CreateListTip(string tip, int listId, int tipIndex)
    {
        if (!CreateTipNode(tip, out GameObject tipGo, out RectTransform tipRect, out CanvasGroup canvasGroup)) return;

        _listTips[listId].Add(tipGo);

        float targetY = -100f + tipIndex * TIP_SPACING;
        Sequence sequence = BuildAnimation(tipRect, canvasGroup, targetY);
        _runningSequences.Add(sequence);
        sequence.OnComplete(() =>
        {
            _runningSequences.Remove(sequence);
            Destroy(tipGo);

            // 面板可能已经关闭并清空了分组数据，取不到时只销毁节点
            if (!_listTips.TryGetValue(listId, out List<GameObject> group)) return;

            group.Remove(tipGo);
            if (group.Count == 0)
            {
                _listTips.Remove(listId);
                _activeListCount--;
                CheckClosePanel();
            }
        });

        sequence.Play();
    }

    #endregion

    /// <summary>
    /// 按模板创建一个提示节点，并初始化文本、位置与透明通道。
    /// </summary>
    /// <param name="content">提示文本。</param>
    /// <param name="tip">创建出的提示节点。</param>
    /// <param name="tipRect">提示节点的矩形变换。</param>
    /// <param name="canvasGroup">提示节点的透明通道组件。</param>
    /// <returns>创建成功返回 true。</returns>
    private bool CreateTipNode(string content, out GameObject tip, out RectTransform tipRect, out CanvasGroup canvasGroup)
    {
        tip = null;
        tipRect = null;
        canvasGroup = null;
        if (_tipTemplate == null || _root == null)
        {
            Debug.LogError("[UITipsPanel] 提示面板未就绪，无法创建提示");
            return false;
        }

        tip = Instantiate(_tipTemplate, _root);
        tip.SetActive(true);

        TMP_Text textComponent = tip.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = content;
        }

        tipRect = tip.GetComponent<RectTransform>();
        if (tipRect == null)
        {
            Debug.LogError($"[UITipsPanel] 提示模板缺少 RectTransform，模板名:{_tipTemplate.name}");
            Destroy(tip);
            return false;
        }
        tipRect.anchoredPosition = new Vector2(0, -200f);

        canvasGroup = tip.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = tip.AddComponent<CanvasGroup>();
        }
        canvasGroup.alpha = 0f;
        return true;
    }

    /// <summary>
    /// 构建一条提示的位移与淡入淡出动画序列。
    /// </summary>
    /// <param name="tipRect">提示节点的矩形变换。</param>
    /// <param name="canvasGroup">提示节点的透明通道组件。</param>
    /// <param name="targetY">提示停留位置的目标 Y 坐标。</param>
    /// <returns>尚未播放的动画序列。</returns>
    private Sequence BuildAnimation(RectTransform tipRect, CanvasGroup canvasGroup, float targetY)
    {
        Sequence sequence = DOTween.Sequence();

        sequence.Append(tipRect.DOAnchorPosY(targetY, ANIMATION_DURATION * 0.4f).SetEase(Ease.OutBack));
        sequence.Join(canvasGroup.DOFade(1f, ANIMATION_DURATION * 0.4f));

        sequence.AppendInterval(ANIMATION_DURATION * 0.2f);

        sequence.Append(tipRect.DOAnchorPosY(targetY + 100f, ANIMATION_DURATION * 0.4f).SetEase(Ease.InBack));
        sequence.Join(canvasGroup.DOFade(0f, ANIMATION_DURATION * 0.4f));

        return sequence;
    }

    /// <summary>
    /// 所有提示播放完毕后关闭面板。
    /// </summary>
    private void CheckClosePanel()
    {
        if (_singleTipCount == 0 && _activeListCount == 0)
        {
            UIManager.Instance.ClosePanel<UITipsPanel>();
        }
    }

    /// <summary>
    /// 关闭面板：终止所有提示动画并清理节点与计数。
    /// </summary>
    /// <param name="args">关闭参数。</param>
    public override void OnClose(params object[] args)
    {
        base.OnClose(args);

        // 先终止动画，避免其完成回调在数据清空后继续访问
        for (int i = 0; i < _runningSequences.Count; i++)
        {
            _runningSequences[i]?.Kill();
        }
        _runningSequences.Clear();

        if (_root != null)
        {
            foreach (Transform child in _root)
            {
                if (child.gameObject != _tipTemplate)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        _singleTipCount = 0;
        _listTips.Clear();
        _activeListCount = 0;
    }
}
