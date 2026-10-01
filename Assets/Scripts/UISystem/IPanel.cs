using System.Collections.Generic;


public interface IPanel
{
    T GetUI<T>(string uiName) where T : UnityEngine.Object;
    List<T> GetUIList<T>(string uiName) where T : UnityEngine.Object;
    void Open(params object[] args);
    void Close(params object[] args);
    void Refresh(params object[] args);
}