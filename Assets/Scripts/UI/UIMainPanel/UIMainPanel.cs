using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主界面面板，提供开始游戏、设置与退出入口。
/// </summary>
public class UIMainPanel : UIPanelBase
{
    // Steam 联机子场景名
    private const string STEAM_SCENE = "SteamScene";

    // Mirror 本地联机子场景名
    private const string MIRROR_SCENE = "GameScene";

    // 开始游戏按钮
    private Button Button_StartGame;

    // 退出游戏按钮
    private Button Button_ExitGame;

    // 本地开局按钮，不依赖 Steam 建立本地 Host 会话
    private Button Button_LocalHost;

    /// <summary>
    /// 获取并缓存面板内 UI 组件引用。
    /// </summary>
    protected override void GetUIComponents()
    {
        Button_StartGame = GetUI<Button>("Button_StartGame");
        Button_ExitGame = GetUI<Button>("Button_ExitGame");
        Button_LocalHost = GetUI<Button>("Button_LocalHost");
    }

    /// <summary>
    /// 绑定各按钮点击事件。
    /// </summary>
    protected override void AddUIListeners()
    {
        base.AddUIListeners();
        if (Button_StartGame != null) Button_StartGame.onClick.AddListener(OnClickStartGame);
        if (Button_ExitGame != null) Button_ExitGame.onClick.AddListener(OnClickExitGame);
        if (Button_LocalHost != null) Button_LocalHost.onClick.AddListener(OnClickLocalHost);
    }

    /// <summary>
    /// 解绑各按钮点击事件，避免面板重复启用后回调叠加。
    /// </summary>
    protected override void RemoveUIListeners()
    {
        base.RemoveUIListeners();
        if (Button_StartGame != null) Button_StartGame.onClick.RemoveListener(OnClickStartGame);
        if (Button_ExitGame != null) Button_ExitGame.onClick.RemoveListener(OnClickExitGame);
        if (Button_LocalHost != null) Button_LocalHost.onClick.RemoveListener(OnClickLocalHost);
    }

    /// <summary>
    /// 点击开始游戏：黑屏遮罩完全变黑后叠加加载 Steam 场景，再打开房间列表界面。
    /// </summary>
    private void OnClickStartGame()
    {
        EnterSubScene(STEAM_SCENE, () => UIManager.Instance.OpenPanel<UIRoomListPanel>(false));
    }

    /// <summary>
    /// 点击本地开局：黑屏遮罩完全变黑后叠加加载 Mirror 联机场景，再打开本地联机界面。
    /// </summary>
    private void OnClickLocalHost()
    {
        EnterSubScene(MIRROR_SCENE, () => UIManager.Instance.OpenPanel<UIMirrorPanel>(false));
    }

    /// <summary>
    /// 叠加加载指定子场景：先拉起黑屏遮罩，遮罩完全变黑后才开始加载与切界面，全部就绪后再揭示遮罩。
    /// </summary>
    /// <param name="sceneName">要叠加加载的子场景名。</param>
    /// <param name="onSceneReady">子场景加载完成后的界面打开回调。</param>
    private void EnterSubScene(string sceneName, System.Action onSceneReady)
    {
        // 遮罩的打开动画回调在 alpha 到达 1 时触发，此处以它为起点，
        // 保证加载与界面切换都发生在屏幕已完全变黑之后
        UIManager.Instance.OpenPanel<UIMaskPanel>(true, () => StartCoroutine(LoadSubScene(sceneName, onSceneReady)));
    }

    /// <summary>
    /// 黑屏期间叠加加载子场景，加载完成并打开目标界面后请求遮罩淡出。
    /// </summary>
    /// <param name="sceneName">要叠加加载的子场景名。</param>
    /// <param name="onSceneReady">子场景加载完成后的界面打开回调。</param>
    private IEnumerator LoadSubScene(string sceneName, System.Action onSceneReady)
    {
        // 重复进入时场景已叠加，直接复用，避免同一场景被加载两次
        if (!SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError($"[UIMainPanel] 子场景加载失败，场景名:{sceneName}");
                UIManager.Instance.GetPanel<UIMaskPanel>()?.RequestReveal();
                yield break;
            }

            yield return operation;
        }

        // 界面打开先于主界面关闭：主界面为临时面板，关闭会连同本协程宿主一起销毁
        onSceneReady?.Invoke();

        // 场景与界面均已就绪，此时才允许遮罩淡出
        UIManager.Instance.GetPanel<UIMaskPanel>()?.RequestReveal();
        UIManager.Instance.ClosePanel<UIMainPanel>(false);
    }

    /// <summary>
    /// 点击退出：关闭游戏进程。
    /// </summary>
    private void OnClickExitGame()
    {
        Application.Quit();
    }
}
