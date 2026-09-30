using System;
using UnityEngine;

/// <summary>
/// 卡牌特效基类。
/// 负责管理 CardData.vfxEffects 中挂载的特效脚本。
/// </summary>
public abstract class CardVFXCore : ScriptableObject
{
    protected abstract string VFXName { get; }

    public virtual bool Execute(CardData card, Vector2Int targetGrid)
    {
        // 核心解耦：只要卡牌配置了任何角色动作（如挥刀、举盾、施法），
        // 特效便自动进入队列，等待动作播放到发力帧击发！
        bool hasAnimation = card != null && card.animationEffects != null && card.animationEffects.Count > 0;

        if (hasAnimation)
        {
            return RegisterToSpawnerQueue();
        }
        else
        {
            // 没有任何角色动作的卡（纯即时法术/抽牌），出牌瞬间直接播放
            return PlayConfiguredVFXImmediately();
        }
    }

    public static void PlayAll(CardData card, Vector2Int targetGrid)
    {
        if (card == null || card.vfxEffects == null) return;

        // 若当前卡牌含有动作，打出前先重置上一次动作可能残留的特效队列
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

    private bool RegisterToSpawnerQueue()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return false;

        GenericVFXSpawner vfxSpawner = playerObj.GetComponent<GenericVFXSpawner>();
        if (vfxSpawner == null) return false;

        vfxSpawner.RegisterPendingVFX(VFXName);
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