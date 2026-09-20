using UnityEngine;

/// <summary>
/// 所有怪物的基类。挂在每一个怪物预制体上。
/// 负责：定义自身类型、生成唯一ID、在生命周期内自动向MonsterSystem注册/注销。
/// 如果不同怪物之间行为差异很大（技能、AI逻辑不同），
/// 可以让具体怪物（如Slime、Goblin、Boss）继承此类并重写Attack等方法。
/// </summary>
public class MonsterIdentityManager : MonoBehaviour
{
    public enum MonsterType
    {
        Slime,
        Goblin,
        Skeleton,
        Boss
        // 后续新增怪物类型直接在这里加
    }

    [Header("怪物基础信息")]
    public MonsterType type;

    [Tooltip("业务层唯一标识，格式为 类型_序号（如 Skeleton_1），由MonsterSystem在注册时自动生成，无需手动填写。")]
    public string monsterId;

    protected virtual void OnEnable()
    {
        // 自动向系统注册；monsterId 会在注册过程中由MonsterSystem按类型分配序号
        MonsterIdentitySystem.Instance?.RegisterMonster(this);
    }

    protected virtual void OnDisable()
    {
        MonsterIdentitySystem.Instance?.UnregisterMonster(this);
    }
}