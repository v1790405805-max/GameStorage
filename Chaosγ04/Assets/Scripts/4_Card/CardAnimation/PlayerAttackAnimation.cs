using UnityEngine;

/// <summary>播放玩家攻击动画。</summary>
public class PlayerAttackAnimation : CardAnimationCore
{
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        Animator animator = GetPlayerAnimator();
        if (animator == null) return false;

        UpdateAttackDirection(animator, targetGrid);
        PlayOneShotTrigger(animator, AttackHash);
        return true;
    }

    private static void UpdateAttackDirection(Animator animator, Vector2Int targetGrid)
    {
        PlayerMoveController moveController = UnityEngine.Object.FindFirstObjectByType<PlayerMoveController>();
        if (moveController == null) return;

        Vector2Int playerGrid = moveController.PlayerGridPos;
        int dx = targetGrid.x - playerGrid.x;
        int dy = targetGrid.y - playerGrid.y;

        if (dx < 0)
        {
            animator.SetFloat(HorizontalHash, -1f);
            animator.SetFloat(VerticalHash, -1f);
        }
        else if (dx > 0)
        {
            animator.SetFloat(HorizontalHash, 1f);
            animator.SetFloat(VerticalHash, 1f);
        }
        else if (dy < 0)
        {
            animator.SetFloat(HorizontalHash, -1f);
            animator.SetFloat(VerticalHash, 1f);
        }
        else if (dy > 0)
        {
            animator.SetFloat(HorizontalHash, 1f);
            animator.SetFloat(VerticalHash, -1f);
        }
    }
}
