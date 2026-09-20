using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 全局怪物管理系统（单例）。
/// 负责维护"场上当前有哪些怪物"的总列表，供其他系统（UI、任务系统、CellManager等）查询。
/// </summary>
public class MonsterIdentitySystem : MonoBehaviour
{
    private static MonsterIdentitySystem _instance;

    // 标记程序是否退出
    private static bool _isQuitting = false;
    // 【新增】标记当前场景的单例是否正在被销毁（用于防止切换场景时生成幽灵物体）
    private static bool _isDestroyed = false;

    public static MonsterIdentitySystem Instance
    {
        get
        {
            // 【修改】如果程序退出，或者当前场景的实例正在被销毁，直接返回null，不再新建
            if (_isQuitting || _isDestroyed)
            {
                return null;
            }

            if (_instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                _instance = FindFirstObjectByType<MonsterIdentitySystem>();
#else
                _instance = FindObjectOfType<MonsterIdentitySystem>();
#endif
                if (_instance == null)
                {
                    GameObject go = new GameObject("MonsterSystem");
                    _instance = go.AddComponent<MonsterIdentitySystem>();
                }
            }
            return _instance;
        }
    }

    private readonly HashSet<MonsterIdentityManager> allMonsters = new HashSet<MonsterIdentityManager>();
    private readonly Dictionary<MonsterIdentityManager.MonsterType, int> typeCounters = new Dictionary<MonsterIdentityManager.MonsterType, int>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        DontDestroyOnLoad(gameObject);

        // 【新增】每次重新进入战斗场景时，重置销毁状态，让单例满血复活
        _isQuitting = false;
        _isDestroyed = false;
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            // 【新增】标记当前实例已被销毁，拦截其他物体在销毁时调用 Instance
            _isDestroyed = true;
            _instance = null;
        }
    }

    public void RegisterMonster(MonsterIdentityManager monster)
    {
        if (monster == null) return;
        if (string.IsNullOrEmpty(monster.monsterId))
        {
            monster.monsterId = GenerateMonsterId(monster.type);
        }
        allMonsters.Add(monster);
    }

    private string GenerateMonsterId(MonsterIdentityManager.MonsterType type)
    {
        if (!typeCounters.ContainsKey(type)) typeCounters[type] = 0;
        typeCounters[type]++;
        return $"{type}_{typeCounters[type]}";
    }

    public void UnregisterMonster(MonsterIdentityManager monster)
    {
        if (monster == null) return;
        allMonsters.Remove(monster);
    }

    public IReadOnlyCollection<MonsterIdentityManager> GetAllMonsters() => allMonsters;
    public List<MonsterIdentityManager> GetMonstersByType(MonsterIdentityManager.MonsterType type) => allMonsters.Where(m => m.type == type).ToList();
    public MonsterIdentityManager GetMonsterById(string monsterId) => allMonsters.FirstOrDefault(m => m.monsterId == monsterId);
    public int MonsterCount => allMonsters.Count;
}