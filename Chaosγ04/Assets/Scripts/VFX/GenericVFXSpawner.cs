using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用动画特效生成器，挂载在拥有 Animator 的角色身上
/// </summary>
public class GenericVFXSpawner : MonoBehaviour
{
    [System.Serializable]
    public class VFXEntry
    {
        [Tooltip("特效标识符，需与动画事件中填写的 String 一致")]
        public string vfxName;

        [Tooltip("特效预制体")]
        public GameObject vfxPrefab;

        [Tooltip("生成位置的骨骼节点（如剑刃、手掌）。如果留空，则默认在角色脚底/中心生成")]
        public Transform customSpawnPoint;

        [Tooltip("是否作为子物体生成？（如果是光环、护盾，需要跟随角色移动，请勾选；如果是留在原地的刀光/爆炸，不勾选）")]
        public bool attachToTransform = false;

        [Tooltip("多少秒后自动销毁该特效？")]
        public float destroyTime = 2f;
    }

    [Header("角色的特效库")]
    public List<VFXEntry> vfxList = new List<VFXEntry>();

    // 存储当前打出卡牌所登记的待播特效队列
    private readonly List<string> pendingVFXQueue = new List<string>();
    private Coroutine fallbackCoroutine;

    /// <summary>
    /// 供 CardVFXCore 注册待播放的特效
    /// </summary>
    public void RegisterPendingVFX(string vfxName)
    {
        if (string.IsNullOrEmpty(vfxName)) return;

        pendingVFXQueue.Add(vfxName);

        // 启动 0.5s 保底：如果动画片段忘了加事件，0.5秒后强制触发，防止特效丢失
        if (fallbackCoroutine != null) StopCoroutine(fallbackCoroutine);
        fallbackCoroutine = StartCoroutine(FallbackTriggerRoutine());
    }

    /// <summary>
    /// 清空待播放队列
    /// </summary>
    public void ClearPendingVFX()
    {
        pendingVFXQueue.Clear();
        if (fallbackCoroutine != null)
        {
            StopCoroutine(fallbackCoroutine);
            fallbackCoroutine = null;
        }
    }

    /// <summary>
    /// 供动画事件（Animation Event）调用的通用方法
    /// </summary>
    /// <param name="eventName">动画帧填写的特效标识符或通用信号（如 "OnHit", "OnAction"）</param>
    public void PlayVFX(string eventName)
    {
        // 1. 如果有卡牌登记的待播特效，优先消耗卡牌队列
        if (pendingVFXQueue.Count > 0)
        {
            if (fallbackCoroutine != null)
            {
                StopCoroutine(fallbackCoroutine);
                fallbackCoroutine = null;
            }

            List<string> toPlay = new List<string>(pendingVFXQueue);
            pendingVFXQueue.Clear();

            foreach (string vfxName in toPlay)
            {
                SpawnVFX(vfxName);
            }
            return;
        }

        // 2. 如果队列为空，且动画事件指定了独立特效名，直接播放
        if (!string.IsNullOrEmpty(eventName) && eventName != "OnHit" && eventName != "OnAction")
        {
            SpawnVFX(eventName);
        }
    }

    private IEnumerator FallbackTriggerRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (pendingVFXQueue.Count > 0)
        {
            Debug.LogWarning("[GenericVFXSpawner] 角色动作已播放，但未检测到 Animation Event！已自动保底播放特效。请检查当前动作切片是否添加了事件。");
            PlayVFX("Fallback");
        }
    }

    private void SpawnVFX(string vfxName)
    {
        foreach (var entry in vfxList)
        {
            if (entry.vfxName == vfxName)
            {
                if (entry.vfxPrefab == null) return;

                Transform spawnTransform = entry.customSpawnPoint != null ? entry.customSpawnPoint : transform;
                GameObject vfxInstance;

                if (entry.attachToTransform)
                {
                    vfxInstance = Instantiate(entry.vfxPrefab, spawnTransform.position, spawnTransform.rotation, spawnTransform);
                }
                else
                {
                    vfxInstance = Instantiate(entry.vfxPrefab, spawnTransform.position, spawnTransform.rotation);
                }

                if (entry.destroyTime > 0)
                {
                    Destroy(vfxInstance, entry.destroyTime);
                }
                return;
            }
        }

        Debug.LogWarning($"[GenericVFXSpawner] 未能在特效库中找到名为 '{vfxName}' 的特效配置！");
    }
}