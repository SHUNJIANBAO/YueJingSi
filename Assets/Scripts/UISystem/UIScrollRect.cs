using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 循环列表
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class UIScrollRect : MonoBehaviour
{
    [SerializeField] private bool _horizontal;
    [SerializeField] private bool _vertical;
    [SerializeField] private int _column;
    [SerializeField] private int _row;
    [SerializeField] private Vector2 _cellSize; //单元格宽高
    [SerializeField] private float _spacingX; //间隔
    [SerializeField] private float _spacingY; //间隔
    [SerializeField] private UIItemBase _itemTemplate; //预制体

    private RectTransform _content; //父物体
    private int _createCount; //显示创建数
    private int _totalCount; //Content显示总数量
    private int _showCount; //实际显示数量
    private int _lastStartIndex = 0; //上次初始序号
    private int _startIndex = 0; //初始序号
    private int _endIndex = 0; //结束序号

    private ScrollRect _scrollRect; //滑动组件
    private RectTransform _rectTrans;
    private Dictionary<int, UIItemBase> _itemIndexDict = new Dictionary<int, UIItemBase>(); //Item对应得序号
    private List<int> _newIndexList = new List<int>();
    private List<int> _changeIndexList = new List<int>();



    private void Awake()
    {
        if (_scrollRect == null)
        {
            _scrollRect = GetComponent<ScrollRect>();
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.AddListener(OnScrollRectChange);
                _scrollRect.horizontal = _horizontal;
                _scrollRect.vertical = _vertical;
            }
        }


        _rectTrans = GetComponent<RectTransform>();
    }
    List<object> _dataList;
    /// <summary>
    /// 初始化并构建
    /// </summary>
    /// <param name="totalCount"></param>
    public void Init(List<object> dataList, UIItemBase itemTemplate = null)
    {
        if (dataList == null)
        {
            Debug.LogWarning("[UIScrollRect] 数据列表为空，跳过初始化");
            return;
        }

        if (_scrollRect == null)
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        if (itemTemplate != null)
        {
            _itemTemplate = itemTemplate;
        }

        if (_scrollRect == null || _scrollRect.content == null || _itemTemplate == null)
        {
            Debug.LogError($"[UIScrollRect] 列表组件未就绪，节点名:{name}");
            return;
        }

        _dataList = dataList;
        _scrollRect.normalizedPosition = Vector2.up;
        _content = _scrollRect.content;
        _itemTemplate.rectTransform.pivot = Vector2.up;
        OnScrollRectChange(Vector2.zero);
        if (_column <= 0 || _row <= 0)
            return;
        _createCount = _column * _row;
        _totalCount = dataList.Count;
        _showCount = Mathf.Min(_totalCount, _createCount);

        int rectWidth, rectHeight;
        GetContentRect(out rectWidth, out rectHeight);
        _content.sizeDelta = new Vector2(rectWidth, rectHeight);
        for (int index = 0; index < _showCount; ++index)
        {
            UIItemBase item = GetItem(index);
            SetItemTransform(item, index);
        }

        ShowOrHideItem(_content, _showCount);
        OnScrollRectChange(Vector2.zero);

    }

    /// <summary>
    /// 获取Content宽高
    /// </summary>
    private void GetContentRect(out int rectWidth, out int rectHeight)
    {
        if (_horizontal)
        {
            rectHeight = (int)(_row * _cellSize.y + (_row - 1) * _spacingY);
            _column = _totalCount / _row + (_totalCount % _row > 0 ? 1 : 0); //计算有多少行，用于计算出总高度
            rectWidth = (int)Mathf.Max(0, _column * _cellSize.x + (_column - 1) * _spacingX);
        }
        else if (_vertical)
        {
            rectWidth = (int)(_column * _cellSize.x +
                               (_column - 1) *
                               _spacingX); //计算横向列宽                                                                  
            _row = _totalCount / _column + (_totalCount % _column > 0 ? 1 : 0);
            rectHeight = (int)Mathf.Max(0, _row * _cellSize.y + (_row - 1) * _spacingY); //计算行数及Content的高度
        }
        else
        {
            rectWidth = 0;
            rectHeight = 0;
        }
    }

    /// <summary>
    /// 获取Item
    /// 有即复用，无则创建
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    private UIItemBase GetItem(int index)
    {
        UIItemBase item = null;

        //回滚(已有，复用)
        if (index < _content.childCount)
        {
            item = _content.GetChild(index).GetComponent<UIItemBase>();
        }

        if (item == null)
        {
            item = Instantiate(_itemTemplate);
        }

        item.gameObject.name = index.ToString();
        item.transform.SetParent(_content);
        item.transform.localScale = Vector3.one;
        return item;
    }

    /// <summary>
    /// 赋值Item
    /// </summary>
    /// <param name="item"></param>
    /// <param name="index"></param>
    private void SetItemTransform(UIItemBase item, int index)
    {
        if (_itemIndexDict.ContainsKey(index))
            _itemIndexDict[index] = item;
        else
            _itemIndexDict.Add(index, item);
        item.transform.localPosition = GetItemPos(index);
        //更新属性
        item.name = "Item_" + index;

        item.SetData(index, _dataList[index]);
    }

    /// <summary>
    /// 获取Item位置
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    private Vector2 GetItemPos(int index)
    {
        Vector2 tmp = Vector2.zero;
        if (_horizontal)
            tmp = new Vector2(index / _row * (_cellSize.y + _spacingX), -index % _row * (_cellSize.x + _spacingY));
        else if (_vertical)
            tmp = new Vector2(index % _column * (_spacingX + _cellSize.x),
                -index / _column * (_spacingY + _cellSize.y));
        return tmp;
    }

    /// <summary>
    /// Item显隐
    /// </summary>
    /// <param name="parent"></param>
    /// <param name="count"></param>
    private void ShowOrHideItem(Transform parent, int count)
    {
        if (parent.childCount < count)
            return;
        for (int index = 0; index < parent.childCount; index++)
        {
            if (index < count)
            {
                parent.GetChild(index).gameObject.SetActive(true);
            }
            else
            {
                parent.GetChild(index).gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 检测滑动
    /// </summary>
    /// <param name="Pos"></param>
    private void OnScrollRectChange(Vector2 Pos)
    {
        if (_totalCount < _createCount)
            return;
        _startIndex = GetStartIndex();
        if (_startIndex + _createCount >= _totalCount)
        {
            _startIndex = _totalCount - _createCount;
            _endIndex = _totalCount - 1;
        }
        else
        {
            _endIndex = _startIndex + _createCount - 1;
        }

        if (_startIndex == _lastStartIndex)
            return;
        _lastStartIndex = _startIndex;
        _newIndexList.Clear();
        _changeIndexList.Clear();
        for (int index = _startIndex; index <= _endIndex; ++index)
        {
            _newIndexList.Add(index);
        }

        foreach (var item in _itemIndexDict)
        {
            if (item.Key >= _startIndex && item.Key <= _endIndex)
            {
                if (_newIndexList.Contains(item.Key))
                {
                    _newIndexList.Remove(item.Key);
                }
            }
            else
            {
                _changeIndexList.Add(item.Key);
            }
        }

        for (int index = 0; index < _newIndexList.Count && index < _changeIndexList.Count; index++)
        {
            int oldIndex = _changeIndexList[index];
            int newIndex = _newIndexList[index];
            if (newIndex >= 0 && newIndex < _totalCount)
            {
                UIItemBase item = _itemIndexDict[oldIndex];
                _itemIndexDict.Remove(oldIndex);
                SetItemTransform(item, newIndex);
            }
        }
    }

    /// <summary>
    /// 获取起始下标
    /// </summary>
    /// <returns></returns>
    private int GetStartIndex()
    {
        if (_horizontal)
        {
            float x = -_content.localPosition.x; //将负坐标转正 
            if (x <= _cellSize.x)
                return 0;
            float scrollWidth = _rectTrans.sizeDelta.x;
            if (x >= (_content.sizeDelta.x - scrollWidth)) //拉到底部了
            {
                if (_totalCount <= _createCount)
                    return 0;
                else
                    return _totalCount - _createCount;
            }

            return (int)((x / (_cellSize.x + _spacingX)) + (x % (_cellSize.x + _spacingX) > 0 ? 1 : 0) - 1) * _row;
        }
        else if (_vertical)
        {
            float y = _content.localPosition.y;
            if (y <= _cellSize.y)
                return 0;
            float scrollHeight = _rectTrans.sizeDelta.y;
            if (y >= (_content.sizeDelta.y - scrollHeight))
            {
                //显示全部了
                if (_totalCount <= _createCount)
                    return 0;
                else
                {
                    //未显示结束
                    return _totalCount - _createCount;
                }
            }

            return (int)((y / (_cellSize.y + _spacingY)) + (y % (_cellSize.y + _spacingY) > 0 ? 1 : 0) - 1) * _column;
        }

        return 0;
    }
}