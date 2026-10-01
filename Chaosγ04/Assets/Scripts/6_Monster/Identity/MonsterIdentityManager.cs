using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 所有怪物的身份与阵营数据。
/// 负责定义怪物阵营、生成唯一 ID，并在启用时向 MonsterIdentitySystem 注册/注销。
/// 当前只有 Dog 和 Crocodile 两个怪物阵营，具体敌对关系由 Hostile Flags 配置。
/// </summary>
public class MonsterIdentityManager : MonoBehaviour
{
    public enum MonsterFaction
    {
        Dog = 0,
        Crocodile = 1
    }

    /// <summary>
    /// 可参与敌对关系的阵营集合，包含两个怪物阵营和玩家。
    /// 与 CardData.effectFlags 一样使用位标志，便于在 Inspector 中多选。
    /// </summary>
    [Flags]
    public enum HostileFactionFlags
    {
        None = 0,
        Dog = 1 << 0,
        Crocodile = 1 << 1,
        Player = 1 << 2
    }

    [Header("怪物基础信息")]
    [Tooltip("怪物所属阵营。")]
    [FormerlySerializedAs("type")]
    public MonsterFaction faction = MonsterFaction.Dog;

    [Tooltip("业务层唯一标识，格式为 类型_序号（如 Skeleton_1），由MonsterSystem在注册时自动生成，无需手动填写。")]
    public string monsterId;

    [Header("敌对阵营")]
    [Tooltip("勾选后的阵营是本怪物的敌对阵营。当前怪物自身阵营会由编辑器锁定，不能勾选。")]
    [SerializeField]
    private HostileFactionFlags hostileFactions =
        HostileFactionFlags.Dog | HostileFactionFlags.Crocodile | HostileFactionFlags.Player;

    [Header("特殊地形通行")]
    [Tooltip("允许该怪物进入并穿过特殊地形格。")]
    [SerializeField] private bool canTraverseSpecialTerrain = false;

    [SerializeField, HideInInspector]
    private bool hostileFactionsInitialized;

    public bool CanTraverseSpecialTerrain => canTraverseSpecialTerrain;
    public HostileFactionFlags HostileFactions => hostileFactions;

    /// <summary>
    /// 把单个怪物阵营转换为敌对集合中的位标志。
    /// </summary>
    public static HostileFactionFlags GetFactionFlag(MonsterFaction targetFaction)
    {
        switch (targetFaction)
        {
            case MonsterFaction.Dog:
                return HostileFactionFlags.Dog;
            case MonsterFaction.Crocodile:
                return HostileFactionFlags.Crocodile;
            default:
                return HostileFactionFlags.None;
        }
    }

    /// <summary>
    /// 当前怪物是否敌对指定怪物。
    /// </summary>
    public bool IsHostileTo(MonsterIdentityManager other)
    {
        return other != null && IsHostileTo(other.faction);
    }

    /// <summary>
    /// 当前怪物是否敌对指定阵营。
    /// </summary>
    public bool IsHostileTo(MonsterFaction otherFaction)
    {
        if (otherFaction == faction)
        {
            return false;
        }

        HostileFactionFlags targetFlag = GetFactionFlag(otherFaction);
        return targetFlag != HostileFactionFlags.None && (hostileFactions & targetFlag) != 0;
    }

    /// <summary>
    /// 当前怪物是否敌对玩家。
    /// </summary>
    public bool IsHostileToPlayer()
    {
        return (hostileFactions & HostileFactionFlags.Player) != 0;
    }

    protected virtual void Awake()
    {
        MigrateLegacyFaction();
        EnsureHostileFactionConfiguration();
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        MigrateLegacyFaction();
        EnsureHostileFactionConfiguration();
    }
#endif

    private void Reset()
    {
        hostileFactions =
            HostileFactionFlags.Dog | HostileFactionFlags.Crocodile | HostileFactionFlags.Player;
        hostileFactionsInitialized = true;
        EnsureHostileFactionConfiguration();
    }

    protected virtual void OnEnable()
    {
        // 自动向系统注册；monsterId 会在注册过程中由MonsterSystem按类型分配序号
        MonsterIdentitySystem.Instance?.RegisterMonster(this);
    }

    protected virtual void OnDisable()
    {
        MonsterIdentitySystem.Instance?.UnregisterMonster(this);
    }

    /// <summary>
    /// 旧场景中的 type 值来自已经移除的 MonsterType 枚举。无法直接映射时，
    /// 按对象名恢复为 Dog 或 Crocodile，保证现有怪物实例无需逐个重建。
    /// </summary>
    private void MigrateLegacyFaction()
    {
        if (faction == MonsterFaction.Dog || faction == MonsterFaction.Crocodile)
        {
            return;
        }

        faction = gameObject.name.IndexOf("Crocodile", StringComparison.OrdinalIgnoreCase) >= 0
            ? MonsterFaction.Crocodile
            : MonsterFaction.Dog;
    }

    /// <summary>
    /// 为已有怪物补上默认敌对关系，并始终清除自身阵营对应的标志位。
    /// </summary>
    public void EnsureHostileFactionConfiguration()
    {
        if (!hostileFactionsInitialized)
        {
            hostileFactions =
                HostileFactionFlags.Dog | HostileFactionFlags.Crocodile | HostileFactionFlags.Player;
            hostileFactionsInitialized = true;
        }

        hostileFactions &= ~GetFactionFlag(faction);
    }
}
