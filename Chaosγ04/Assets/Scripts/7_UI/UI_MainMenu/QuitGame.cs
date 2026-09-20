using UnityEngine;

public class QuitGame : MonoBehaviour
{
    /// <summary>
    /// 退出游戏的方法
    /// </summary>
    public void QuitTheGame()
    {
        Debug.Log("游戏正在退出...");

#if UNITY_EDITOR
        // 如果当前在 Unity 编辑器中运行，则停止播放模式
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 如果是打包后的正式发布版本，则直接关闭应用程序
        Application.Quit();
#endif
    }
}