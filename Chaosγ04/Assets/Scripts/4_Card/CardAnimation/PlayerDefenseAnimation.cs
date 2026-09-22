using UnityEngine;

/// <summary>播放玩家防御动画。</summary>
public class PlayerDefenseAnimation : CardAnimationCore
{
    private static readonly int DefenseHash = Animator.StringToHash("Defense");

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return false;

        PlayOneShotTrigger(animator, DefenseHash);
        return true;
    }
}
