using UnityEngine;

/// <summary>播放玩家死亡动画。</summary>
public class PlayerDeathAnimation : CardAnimationCore
{
    private static readonly int DieHash = Animator.StringToHash("Die");

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return false;

        animator.SetBool(DieHash, true);
        return true;
    }
}
