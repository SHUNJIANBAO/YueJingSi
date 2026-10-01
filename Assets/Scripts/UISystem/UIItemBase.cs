using System;
using System.Collections.Generic;
using System.Text;


public abstract class UIItemBase : UIBase
{
    protected int m_Index;
    protected object m_Data;
    public int Index => m_Index;
    public void SetData(int index, object data)
    {
        m_Index = index;
        m_Data = data;
        SetData(m_Data);
    }

    protected abstract void SetData(object data);
}