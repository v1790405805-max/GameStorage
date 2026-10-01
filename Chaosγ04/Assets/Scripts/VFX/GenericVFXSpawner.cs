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
        [Tooltip("特效标识符，需与卡牌或动画事件中填写的名称一致")]
        public string vfxName;

        [Tooltip("特效预制体")]
        public GameObject vfxPrefab;

        [Tooltip("生成位置的挂点（如 Sword Slash Shooter / Stab Shooter）。仅取其世界坐标")]
        public Transform customSpawnPoint;

        [Tooltip("针对该特效的旋转角度微调（应对不同美术粒子的初始朝向差异）")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("是否作为子物体跟随角色？")]
        public bool attachToTransform = false;

        [Tooltip("多少秒后自动销毁该特效？")]
        public float destroyTime = 2f;
    }

    [Header("角色的特效库")]
    public List<VFXEntry> vfxList = new List<VFXEntry>();

    private struct PendingVFXData
    {
        public string vfxName;
        public Vector2Int? targetGrid;
    }

    private readonly List<PendingVFXData> pendingVFXQueue = new List<PendingVFXData>();
    private Coroutine fallbackCoroutine;

    public void RegisterPendingVFX(string vfxName, Vector2Int? targetGrid = null)
    {
        if (string.IsNullOrEmpty(vfxName)) return;

        pendingVFXQueue.Add(new PendingVFXData { vfxName = vfxName, targetGrid = targetGrid });

        if (fallbackCoroutine != null) StopCoroutine(fallbackCoroutine);
        fallbackCoroutine = StartCoroutine(FallbackTriggerRoutine());
    }

    public void ClearPendingVFX()
    {
        pendingVFXQueue.Clear();
        if (fallbackCoroutine != null)
        {
            StopCoroutine(fallbackCoroutine);
            fallbackCoroutine = null;
        }
    }

    public void PlayVFX(string eventName)
    {
        if (pendingVFXQueue.Count > 0)
        {
            if (fallbackCoroutine != null)
            {
                StopCoroutine(fallbackCoroutine);
                fallbackCoroutine = null;
            }

            List<PendingVFXData> toPlay = new List<PendingVFXData>(pendingVFXQueue);
            pendingVFXQueue.Clear();

            foreach (var item in toPlay)
            {
                SpawnVFX(item.vfxName, item.targetGrid);
            }
            return;
        }

        if (!string.IsNullOrEmpty(eventName) && eventName != "OnHit" && eventName != "OnAction")
        {
            SpawnVFX(eventName, null);
        }
    }

    private IEnumerator FallbackTriggerRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (pendingVFXQueue.Count > 0)
        {
            Debug.LogWarning("[GenericVFXSpawner] 角色动作已播放，但未检测到 Animation Event！已自动保底播放特效。");
            PlayVFX("Fallback");
        }
    }

    private void SpawnVFX(string vfxName, Vector2Int? targetGrid)
    {
        foreach (var entry in vfxList)
        {
            if (entry.vfxName == vfxName)
            {
                if (entry.vfxPrefab == null) return;

                Transform spawnPoint = entry.customSpawnPoint != null ? entry.customSpawnPoint : transform;

                Vector3 finalPosition = spawnPoint.position;
                Quaternion finalRotation = spawnPoint.rotation;

                // 统一四向朝向计算
                if (targetGrid.HasValue)
                {
                    float yawAngle = Calculate4DirectionYaw(targetGrid.Value);
                    Quaternion yawRotation = Quaternion.Euler(0f, yawAngle, 0f);

                    // 1. 发射点位置绕角色中心做步进旋转
                    Vector3 worldOffset = spawnPoint.position - transform.position;
                    finalPosition = transform.position + (yawRotation * worldOffset);

                    // 2. 特效朝向：纯净对齐四向，并叠加该特效专用的 rotationOffset
                    finalRotation = yawRotation * Quaternion.Euler(entry.rotationOffset);
                }

                GameObject vfxInstance;
                if (entry.attachToTransform)
                {
                    vfxInstance = Instantiate(entry.vfxPrefab, finalPosition, finalRotation, spawnPoint);
                }
                else
                {
                    vfxInstance = Instantiate(entry.vfxPrefab, finalPosition, finalRotation);
                }

                // 保持层级高于角色，防止被遮挡
                Renderer[] renderers = vfxInstance.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    r.sortingOrder = Mathf.Max(150, r.sortingOrder + 105);
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

    private float Calculate4DirectionYaw(Vector2Int targetGrid)
    {
        GridManager gridMgr = FindFirstObjectByType<GridManager>();
        if (gridMgr == null) return 0f;

        CellManager targetCell = gridMgr.GetCellManagerAt(targetGrid.x, targetGrid.y);
        if (targetCell == null) return 0f;

        Vector3 dir = targetCell.transform.position - transform.position;
        dir.y = 0f;

        if (dir == Vector3.zero) return 0f;

        // 斜45度标准四向步进
        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.z))
        {
            return dir.x > 0 ? 0f : 180f;   // 右下 (+X) / 左上 (-X)
        }
        else
        {
            return dir.z > 0 ? 270f : 90f;  // 右上 (+Z) / 左下 (-Z)
        }
    }
}