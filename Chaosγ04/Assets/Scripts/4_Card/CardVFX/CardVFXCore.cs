using System;
using UnityEngine;

/// <summary>
/// 特效触发时机枚举
/// </summary>
public enum VFXTiming
{
    Auto,           // 自动判断：卡牌带动作则等动作发力点，无动作则立即播放
    Instant,        // 强制立即触发（出牌瞬间生成，适用于护盾、回血）
    OnAnimationHit  // 强制动作发力点触发（等待动画事件）
}

/// <summary>
/// 卡牌特效基类。
/// 负责管理 CardData.vfxEffects 中挂载的特效脚本。
/// </summary>
public abstract class CardVFXCore : ScriptableObject
{
    protected abstract string VFXName { get; }

    public virtual VFXTiming Timing => VFXTiming.Auto;

    public virtual bool Execute(CardData card, Vector2Int targetGrid)
    {
        bool hasAnimation = card != null && card.animationEffects != null && card.animationEffects.Count > 0;
        bool isHitTiming = Timing == VFXTiming.OnAnimationHit ||
                           (Timing == VFXTiming.Auto && hasAnimation);

        if (isHitTiming)
        {
            // 将特效与目标格子一同登记到角色的待命队列
            return RegisterToSpawnerQueue(targetGrid);
        }
        else
        {
            return PlayConfiguredVFXImmediately();
        }
    }

    public static void PlayAll(CardData card, Vector2Int targetGrid)
    {
        if (card == null || card.vfxEffects == null) return;

        bool hasAnimation = card.animationEffects != null && card.animationEffects.Count > 0;
        if (hasAnimation)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            GenericVFXSpawner vfxSpawner = playerObj != null ? playerObj.GetComponent<GenericVFXSpawner>() : null;
            if (vfxSpawner != null)
            {
                vfxSpawner.ClearPendingVFX();
            }
        }

        foreach (CardPresentationEffectReference reference in card.vfxEffects)
        {
            if (string.IsNullOrEmpty(reference.effectTypeName)) continue;

            Type effectType = Type.GetType(reference.effectTypeName);
            if (effectType == null || effectType.IsAbstract || !typeof(CardVFXCore).IsAssignableFrom(effectType))
                continue;

            CardVFXCore instance = CreateInstance(effectType) as CardVFXCore;
            if (instance == null) continue;

            bool success = instance.Execute(card, targetGrid);
            UnityEngine.Object.Destroy(instance);

            if (!success)
            {
                Debug.LogWarning($"[CardVFXCore] 卡牌 [{card.cardName}] 的特效 [{reference.effectTypeName}] 执行失败。");
            }
        }
    }

    private bool RegisterToSpawnerQueue(Vector2Int targetGrid)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return false;

        GenericVFXSpawner vfxSpawner = playerObj.GetComponent<GenericVFXSpawner>();
        if (vfxSpawner == null) return false;

        vfxSpawner.RegisterPendingVFX(VFXName, targetGrid);
        return true;
    }

    private bool PlayConfiguredVFXImmediately()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return false;

        GenericVFXSpawner vfxSpawner = playerObj.GetComponent<GenericVFXSpawner>();
        if (vfxSpawner == null) return false;

        vfxSpawner.PlayVFX(VFXName);
        return true;
    }
}