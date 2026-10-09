using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂载在 DogLair 上，标记 DogLair 是否已经被破坏。
/// 该组件存在时，SummonDogAction 会在 CanExecute 阶段直接失效，
/// 不会进入召唤逻辑，也不会中断其他 Action 的执行链。
/// </summary>
[DisallowMultipleComponent]
public sealed class WhenDogLairDestroyed : MonoBehaviour
{
    private const string DogLairObjectName = "DogLair";

    private static bool dogLairDestroyed;

    public static bool IsDestroyed => dogLairDestroyed;

    public static bool IsDogLair(GameObject gameObject)
    {
        return gameObject != null &&
               gameObject.name.IndexOf(
                   DogLairObjectName,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static WhenDogLairDestroyed EnsureOn(GameObject dogLair)
    {
        if (!IsDogLair(dogLair))
        {
            return null;
        }

        WhenDogLairDestroyed component =
            dogLair.GetComponent<WhenDogLairDestroyed>();
        if (component == null)
        {
            component = dogLair.AddComponent<WhenDogLairDestroyed>();
        }

        return component;
    }

    public static void MarkDestroyed(GameObject dogLair)
    {
        if (IsDogLair(dogLair))
        {
            dogLairDestroyed = true;
        }
    }

    private void OnEnable()
    {
        dogLairDestroyed = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        dogLairDestroyed = false;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        dogLairDestroyed = false;
    }
}
