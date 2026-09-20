using UnityEngine;
using UnityEngine.SceneManagement; // 引入场景管理命名空间

public class SceneLoader : MonoBehaviour
{
    [Header("场景跳转配置")]
    [Tooltip("在这里输入你想跳转的目标场景名称（必须与 Build Settings 里的名称完全一致）")]
    public string targetSceneName;

    /// <summary>
    /// 通用的跳转方法：只要点击挂载了此脚本的物体上的按钮，就会跳转到 targetSceneName 指定的场景
    /// </summary>
    public void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError($"【错误】物体 {gameObject.name} 上的 SceneLoader 没有设置目标场景名称！");
            return;
        }

        Debug.Log($"正在从大地图跳转到场景: {targetSceneName}");
        SceneManager.LoadScene(targetSceneName);
    }
}