using System;
using UnityEngine;

/// <summary>
/// 卡牌动画效果基类。
/// 负责管理 CardData.animationEffects 中挂载的动画脚本。
/// </summary>
public abstract class CardAnimationCore : ScriptableObject
{
    public abstract bool Execute(CardData card, Vector2Int targetGrid);

    public static void PlayAll(CardData card, Vector2Int targetGrid)
    {
        if (card == null || card.animationEffects == null) return;

        foreach (CardPresentationEffectReference reference in card.animationEffects)
        {
            if (string.IsNullOrEmpty(reference.effectTypeName)) continue;

            Type effectType = Type.GetType(reference.effectTypeName);
            if (effectType == null || effectType.IsAbstract || !typeof(CardAnimationCore).IsAssignableFrom(effectType))
                continue;

            CardAnimationCore instance = CreateInstance(effectType) as CardAnimationCore;
            if (instance == null) continue;

            bool success = instance.Execute(card, targetGrid);
            UnityEngine.Object.Destroy(instance);

            if (!success)
            {
                Debug.LogWarning(
                    $"[CardAnimationCore] 卡牌 [{card.cardName}] 的动画效果 [{reference.effectTypeName}] 执行失败。");
            }
        }
    }

    protected static Animator GetPlayerAnimator()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        return playerObj != null ? playerObj.GetComponentInChildren<Animator>() : null;
    }

    protected static void PlayOneShotTrigger(Animator animator, int triggerHash)
    {
        animator.ResetTrigger(triggerHash);
        animator.SetTrigger(triggerHash);
    }
}
