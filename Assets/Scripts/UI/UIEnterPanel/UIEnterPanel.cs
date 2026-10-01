using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIEnterPanel : UIPanelBase
{
    Button Button_Quit;
    protected override void GetUIComponents()
    {
        Button_Quit = GetUI<Button>("Button_Quit");
    }
    protected override void AddUIListeners()
    {
        base.AddUIListeners();
        AddButtonListen(Button_Quit, OnClickQuit);
    }
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();
        RemoveButtonListen(Button_Quit, OnClickQuit);
    }
    protected override void OnOpen(params object[] args)
    {
        base.OnOpen(args);

    }

    void OnClickQuit()
    {
        Application.Quit();
    }
}
