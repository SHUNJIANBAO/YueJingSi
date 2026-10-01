using UnityEngine;

/// <summary>
/// 对局断线处理，判定当前是否处于对局阶段并弹出退出面板。
/// </summary>
public static class MatchDisconnectHandler
{
    /// <summary>
    /// 处理客户端在对局中的掉线，对局界面仍在时弹出退出面板。
    /// </summary>
    public static void HandleDisconnected()
    {
        // 界面管理器随启动壳常驻，取不到说明启动流程未完成，此时没有界面可弹
        UIManager uiManager = UIManager.GetInstance();
        if (uiManager == null)
        {
            Debug.LogError("[Network] 界面管理器不存在，无法弹出退出面板");
            return;
        }

        // 对局界面开着才算对局阶段，房间与大厅阶段的掉线沿用各自的既有流程
        if (!uiManager.IsOpen<UIGamePanel>())
            return;

        uiManager.OpenPanel<UIEnterPanel>(false);
    }
}
