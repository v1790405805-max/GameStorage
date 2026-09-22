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
        return PlayConfiguredVFX();
    }

    public static void PlayAll(CardData card, Vector2Int targetGrid)
    {
        if (card == null || card.vfxEffects == null) return;

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
                Debug.LogWarning(
                    $"[CardVFXCore] 卡牌 [{card.cardName}] 的特效 [{reference.effectTypeName}] 执行失败。");
            }
        }
    }

    private bool PlayConfiguredVFX()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return false;

        GenericVFXSpawner vfxSpawner = playerObj.GetComponent<GenericVFXSpawner>();
        if (vfxSpawner == null) return false;

        vfxSpawner.PlayVFX(VFXName);
        return true;
    }
}
