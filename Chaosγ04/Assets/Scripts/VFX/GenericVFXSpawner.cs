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

        [Tooltip("是否作为子物体生成？（如果特效是光环、护盾，需要跟随角色移动，请勾选；如果是留在原地的刀光/爆炸，不勾选）")]
        public bool attachToTransform = false;

        [Tooltip("多少秒后自动销毁该特效？")]
        public float destroyTime = 2f;
    }

    [Header("角色的特效库")]
    public List<VFXEntry> vfxList = new List<VFXEntry>();

    /// <summary>
    /// 供动画事件（Animation Event）调用的通用方法
    /// </summary>
    /// <param name="eventName">在动画帧上填写的特效标识符</param>
    public void PlayVFX(string eventName)
    {
        // 遍历特效库，寻找名字匹配的特效
        foreach (var entry in vfxList)
        {
            if (entry.vfxName == eventName)
            {
                if (entry.vfxPrefab == null) return;

                // 确定生成位置（如果有自定义节点就用节点的，没有就用角色自身的）
                Transform spawnTransform = entry.customSpawnPoint != null ? entry.customSpawnPoint : transform;

                GameObject vfxInstance;

                if (entry.attachToTransform)
                {
                    // 生成并作为子物体跟随（适用于：身上持续燃烧的火、护盾、光环）
                    vfxInstance = Instantiate(entry.vfxPrefab, spawnTransform.position, spawnTransform.rotation, spawnTransform);
                }
                else
                {
                    // 仅在目标位置生成，不跟随移动（适用于：挥出的刀光、砸在地上的落雷）
                    vfxInstance = Instantiate(entry.vfxPrefab, spawnTransform.position, spawnTransform.rotation);
                }

                // 定时销毁以释放内存
                if (entry.destroyTime > 0)
                {
                    Destroy(vfxInstance, entry.destroyTime);
                }
                return;
            }
        }

        Debug.LogWarning($"[GenericVFXSpawner] 未能在特效库中找到名为 '{eventName}' 的特效配置！");
    }
}