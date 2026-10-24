using UnityEngine;

/// <summary>播放玩家护甲动画。</summary>
public class PlayerArmorAnimation : CardAnimationCore
{
    private static readonly int ArmorHash = Animator.StringToHash("Armor");

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return false;

        PlayOneShotTrigger(animator, ArmorHash);
        return true;
    }
}
