using System.Collections;
using UnityEngine;

/// <summary>
/// 星辰折跃纯表现脚本（继承自 CardVFXCore）：
/// 负责起点坍缩、短时间隐身、终点星芒破空。不干预任何坐标计算与底层位移。
/// </summary>
public class AstralTeleportVFX : CardVFXCore
{
    // 实现基类要求的特效名称属性
    protected override string VFXName => "星辰折跃";

    [Header("折跃特效预制体 (放置于 Resources 目录下)")]
    public GameObject departVFXPrefab;
    public GameObject arriveVFXPrefab;

    [Header("折跃时序控制")]
    [Tooltip("虚空中隐身持续时长（秒），建议 0.05 ~ 0.08")]
    public float voidDuration = 0.06f;

    public override bool Execute(CardData card, Vector2Int targetGrid)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return false;

        // 借用主角身上的 MonoBehaviour 驱动表现协程
        MonoBehaviour runner = playerObj.GetComponentInParent<MonoBehaviour>();
        if (runner != null)
        {
            runner.StartCoroutine(AstralVisualSequence(playerObj.transform.root));
            return true;
        }

        return false;
    }

    private IEnumerator AstralVisualSequence(Transform playerRoot)
    {
        EnsureVFXLoaded();

        // 1. 起点：记录出招瞬间的位置，生成星蚀坍缩消散特效
        Vector3 departPos = playerRoot.position;
        SpawnVFX(departVFXPrefab, departPos);

        // 2. 刺客身体隐入虚空（此时位移脚本会在同一帧内将角色移走）
        SpriteRenderer[] renderers = playerRoot.GetComponentsInChildren<SpriteRenderer>();
        SetRenderersVisible(renderers, false);

        // 3. 虚空穿梭停留
        if (voidDuration > 0f)
        {
            yield return new WaitForSeconds(voidDuration);
        }

        // 4. 终点：在角色当前已经到达的最新位置生成星芒破空降临特效
        SpawnVFX(arriveVFXPrefab, playerRoot.position);

        // 5. 刺客破空现身
        SetRenderersVisible(renderers, true);
    }

    private void EnsureVFXLoaded()
    {
        if (departVFXPrefab == null)
        {
            departVFXPrefab = Resources.Load<GameObject>("VFX_Astral_Depart")
                ?? Resources.Load<GameObject>("VFX/VFX_Astral_Depart");
        }

        if (arriveVFXPrefab == null)
        {
            arriveVFXPrefab = Resources.Load<GameObject>("VFX_Astral_Step")
                ?? Resources.Load<GameObject>("VFX/VFX_Astral_Step");
        }
    }

    private void SpawnVFX(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        GameObject vfx = Object.Instantiate(prefab, position, Quaternion.identity);
        Object.Destroy(vfx, 0.6f);
    }

    private void SetRenderersVisible(SpriteRenderer[] renderers, bool visible)
    {
        if (renderers == null) return;
        foreach (var sr in renderers)
        {
            if (sr != null) sr.enabled = visible;
        }
    }
}