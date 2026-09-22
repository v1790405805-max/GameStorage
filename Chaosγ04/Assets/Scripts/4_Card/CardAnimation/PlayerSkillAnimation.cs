using UnityEngine;

/// <summary>播放玩家技能动画。</summary>
public class PlayerSkillAnimation : CardAnimationCore
{
    private static readonly int SkillHash = Animator.StringToHash("Skill");

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return false;

        animator.SetTrigger(SkillHash);
        return true;
    }
}
