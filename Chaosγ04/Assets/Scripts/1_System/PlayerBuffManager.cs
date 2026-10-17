using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家关卡内 Buff 的统一管理器。目前只管理坚固值。
/// 与 CombatStatsManager 一样自动创建并跨场景保留实例；
/// 每次进入新场景时清零，存档恢复时再由存档桥接写回。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBuffManager : MonoBehaviour
{
    private static PlayerBuffManager _instance;
    private static bool _isQuitting;

    public static PlayerBuffManager Instance
    {
        get
        {
            if (_isQuitting)
            {
                return null;
            }

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PlayerBuffManager>();
                if (_instance == null)
                {
                    GameObject managerGo = new GameObject("[PlayerBuffManager]");
                    _instance = managerGo.AddComponent<PlayerBuffManager>();
                }
            }

            return _instance;
        }
    }

    [Header("--- 玩家关卡内 Buff ---")]
    [SerializeField, Min(0)] private int sturdy;

    public int Sturdy => sturdy;

    public event Action<int> SturdyChanged;

    private void Awake()
    {
        _isQuitting = false;

        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _isQuitting = true;
            _instance = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        _ = Instance;
    }

    public void AddSturdy(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        SetSturdy(sturdy + amount);
    }

    public void RemoveSturdy(int amount)
    {
        SetSturdy(sturdy - amount);
    }

    public void SetSturdy(int value)
    {
        int nextValue = Mathf.Max(0, value);
        if (nextValue == sturdy)
        {
            return;
        }

        sturdy = nextValue;
        SturdyChanged?.Invoke(sturdy);
    }

    /// <summary>
    /// 对一次格挡获取应用坚固加成。负值（移除格挡）不受影响。
    /// </summary>
    public int ApplyBlockGainBonus(int baseBlock)
    {
        if (baseBlock <= 0)
        {
            return baseBlock;
        }

        return Mathf.Max(0, baseBlock + sturdy);
    }

    public void ResetForNewLevel()
    {
        SetSturdy(0);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || _instance == null)
        {
            return;
        }

        _instance.ResetForNewLevel();
    }
}
