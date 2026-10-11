using UnityEngine;

/// <summary>
/// 全局主相机入口。
///
/// 约定：场景中名字为 "Main Camera" 的那台启用的相机就是主相机。
/// 需要主相机的脚本统一写成 MainCameraAppoint.Current，不要再用 Camera.main——
/// Camera.main 只认 Tag，双相机场景下（主相机 + UI 相机、相机叠加、启停切换视角等）
/// 它可能指向一台并不渲染地图的相机，导致悬停 / 点击落点 / 拖拽与画面错位。
///
/// 用法：Camera mainCamera = MainCameraAppoint.Current;
/// </summary>
public static class MainCameraAppoint
{
    /// <summary>主相机的物体名。</summary>
    public const string ObjectName = "Main Camera";

    private static int cachedFrame = -1;
    private static Camera cachedCamera;
    private static Camera cachedNamedCamera;
    private static bool warnedMissingNamedCamera;

    /// <summary>
    /// 当前主相机。同一帧内只解析一次；场景里有多个同名相机时取 Depth 最高的一台
    /// （Depth 越高越靠上，也就是玩家实际看到的那台）。
    /// </summary>
    public static Camera Current
    {
        get
        {
            if (cachedFrame == Time.frameCount) return cachedCamera;
            cachedFrame = Time.frameCount;

            if (!IsNamedMainCamera(cachedNamedCamera))
            {
                cachedNamedCamera = FindNamedCamera();
            }

            if (cachedNamedCamera != null)
            {
                cachedCamera = cachedNamedCamera;
                return cachedCamera;
            }

            // 场景里没有按约定命名的相机时回退到 Camera.main，避免交互整体失效。
            if (!warnedMissingNamedCamera)
            {
                warnedMissingNamedCamera = true;
                Debug.LogWarning($"[MainCameraAppoint] 场景中没有名字为 \"{ObjectName}\" 的启用的相机，暂用 Camera.main 代替。");
            }

            cachedCamera = Camera.main;
            return cachedCamera;
        }
    }

    private static Camera FindNamedCamera()
    {
        Camera best = null;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (!IsNamedMainCamera(candidate)) continue;

            if (best == null || candidate.depth > best.depth)
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>是否是命名正确、启用、且渲染到屏幕（而非 RenderTexture）的相机。</summary>
    private static bool IsNamedMainCamera(Camera camera)
    {
        if (camera == null) return false;
        if (!camera.isActiveAndEnabled) return false;
        if (camera.targetTexture != null) return false;

        return camera.gameObject.name == ObjectName;
    }
}
