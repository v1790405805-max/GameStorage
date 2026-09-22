using UnityEngine;

/// <summary>播放玩家攻击动画。</summary>
public class PlayerAttackAnimation : CardAnimationCore
{
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return false;

        animator.SetTrigger(AttackHash);
        return true;
    }
}
