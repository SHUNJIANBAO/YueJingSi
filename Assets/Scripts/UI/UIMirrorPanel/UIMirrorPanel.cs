using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 本地联机面板，提供 Mirror 本地开局入口与返回主界面入口。
/// </summary>
public class UIMirrorPanel : UIPanelBase
{
    // 本面板所属的 Mirror 联机子场景名
    private const string MIRROR_SCENE = "GameScene";

    // 返回主界面按钮
    private Button Button_Back;

    // 以主机身份开局的按钮
    private Button Button_Host;

    // 以客户端身份加入的按钮
    private Button Button_Client;

    /// <summary>
    /// 获取并缓存面板内 UI 组件引用。
    /// </summary>
    protected override void GetUIComponents()
    {
        Button_Back = GetUI<Button>("Button_Back");
        Button_Host = GetUI<Button>("Button_Host");
        Button_Client = GetUI<Button>("Button_Client");
    }

    /// <summary>
    /// 绑定面板内各按钮点击事件。
    /// </summary>
    protected override void AddUIListeners()
    {
        base.AddUIListeners();
        AddButtonListen(Button_Back, OnBackClicked);
        AddButtonListen(Button_Host, OnHostClicked);
        AddButtonListen(Button_Client, OnClientClicked);
    }

    /// <summary>
    /// 解绑面板内各按钮点击事件。
    /// </summary>
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();
        RemoveButtonListen(Button_Back, OnBackClicked);
        RemoveButtonListen(Button_Host, OnHostClicked);
        RemoveButtonListen(Button_Client, OnClientClicked);
    }

    /// <summary>
    /// 点击 Host：黑屏后以主机身份启动服务端与客户端，随后启动球体生成。
    /// </summary>
    private void OnHostClicked()
    {
        EnterNetwork(() =>
        {
            NetworkManager.singleton.StartHost();

            // 生成循环由主机驱动，客户端加入路径不会走到这里
            BallSpawner ballSpawner = BallSpawner.GetInstance();
            if (ballSpawner != null)
                ballSpawner.Begin();
        });
    }

    /// <summary>
    /// 点击 Client：黑屏后以客户端身份连接当前网络地址。
    /// </summary>
    private void OnClientClicked()
    {
        EnterNetwork(() => NetworkManager.singleton.StartClient());
    }

    /// <summary>
    /// 黑屏遮罩完全变黑后执行联机启动，随后揭示遮罩并关闭本面板。
    /// </summary>
    /// <param name="startAction">遮罩全黑后要执行的 Mirror 启动动作。</param>
    private void EnterNetwork(Action startAction)
    {
        // 遮罩的打开动画回调在 alpha 到达 1 时触发，此处以它为起点，
        // 保证联机启动与界面切换都发生在屏幕已完全变黑之后
        UIManager.Instance.OpenPanel<UIMaskPanel>(true, () =>
        {
            if (NetworkManager.singleton == null)
                Debug.LogError("[UIMirrorPanel] 场景中不存在 NetworkManager，无法启动联机");
            else
                startAction?.Invoke();

            // 启动动作已处理完毕，此时揭示遮罩并关闭本面板
            UIManager.Instance.GetPanel<UIMaskPanel>()?.RequestReveal();
            UIManager.Instance.ClosePanel<UIMirrorPanel>(false);
        });
    }

    /// <summary>
    /// 点击返回：卸载 Mirror 子场景并回到主界面。
    /// </summary>
    private void OnBackClicked()
    {
        // 遮罩的打开动画回调在 alpha 到达 1 时触发，此处以它为起点，
        // 保证卸载与界面切换都发生在屏幕已完全变黑之后
        UIManager.Instance.OpenPanel<UIMaskPanel>(true, () => StartCoroutine(ReturnToMainScene()));
    }

    /// <summary>
    /// 黑屏期间卸载本面板所属子场景，卸载完成并重开主界面后请求遮罩淡出。
    /// </summary>
    private IEnumerator ReturnToMainScene()
    {
        Scene mirrorScene = SceneManager.GetSceneByName(MIRROR_SCENE);
        if (mirrorScene.isLoaded)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(mirrorScene);
            if (operation != null)
            {
                yield return operation;
            }
        }

        UIManager.Instance.OpenPanel<UIMainPanel>(false);

        // 场景与界面均已就绪，此时才允许遮罩淡出
        UIManager.Instance.GetPanel<UIMaskPanel>()?.RequestReveal();
        UIManager.Instance.ClosePanel<UIMirrorPanel>(false);
    }
}
