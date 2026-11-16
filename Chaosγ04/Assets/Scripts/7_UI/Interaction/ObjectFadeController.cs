using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(110)]
public class ObjectFadeController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("检查视线的相机。留空时使用 MainCameraAppoint.Current。")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("需要保持可见的玩家 Transform。Monster 会通过 MonsterIdentitySystem 自动加入。")]
    [SerializeField] private Transform playerTarget;
    [Tooltip("用于自动解析玩家 Transform。留空时在场景中自动查找。")]
    [SerializeField] private PlayerMoveController playerMoveController;

    [Header("遮挡虚化")]
    [Tooltip("阻挡物虚化时保持的透明度。1 为实体，0 为完全透明。")]
    [Range(0f, 1f)] public float occludedAlpha = 0.25f;
    [Tooltip("变透明的速度（每秒 alpha 变化量）。数值越大，虚化越快。")]
    [Min(0f)] public float fadeOutSpeed = 12f;
    [Tooltip("恢复不透明的速度。建议比变透明慢一些。")]
    [Min(0f)] public float fadeInSpeed = 6f;
    [Tooltip("参与视线检测的物理层。")]
    public LayerMask occlusionLayers = ~0;

    [Header("防闪烁")]
    [Tooltip("不再被遮挡后，延迟多久才开始恢复。用于消除逐帧命中抖动。")]
    [Min(0f)] public float releaseDelay = 0.2f;
    [Tooltip("检测角色时，射线在目标前提前停止的距离。用于避开脚下台阶。")]
    [Min(0f)] public float playerRayEndMargin = 0.4f;
    [Tooltip("检测悬停地格时，射线在目标前提前停止的距离。")]
    [Min(0f)] public float cellRayEndMargin = 0.05f;
    [Tooltip("角色检测点相对碰撞体底部的抬高量。")]
    [Min(0f)] public float playerTargetHeightOffset = 0.3f;

    [Header("支撑面过滤")]
    [Tooltip("判定为支撑面所需的最低向上法线。数值越高，只忽略越平的地面。")]
    [Range(0f, 1f)] public float supportSurfaceMinNormalY = 0.7f;
    [Tooltip("命中点与脚底允许的最大高度差。用于覆盖角色轻微陷入平台的情况。")]
    [Min(0f)] public float supportSurfaceHeightTolerance = 0.3f;
    [Tooltip("在角色碰撞体水平尺寸之外额外允许的支撑面检测半径。")]
    [Min(0f)] public float supportSurfaceRadiusPadding = 0.25f;

    [Header("调试")]
    [Tooltip("在 Scene 视图中绘制相机到所有角色的检查线。")]
    public bool drawDebugLine = true;

    private const int HitCapacity = 32;
    private static readonly int OcclusionAlphaId = Shader.PropertyToID("_OcclusionAlpha");

    private sealed class FadeState
    {
        public float alpha = 1f;
        public float lastBlockedTime;
    }

    private sealed class OcclusionTarget
    {
        public Transform root;
        public Collider targetCollider;
    }

    private readonly HashSet<Renderer> blockers = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, FadeState> fadeStates = new Dictionary<Renderer, FadeState>();
    private readonly List<OcclusionTarget> occlusionTargets = new List<OcclusionTarget>();
    private readonly Dictionary<Transform, OcclusionTarget> occlusionTargetCache =
        new Dictionary<Transform, OcclusionTarget>();
    private readonly HashSet<Transform> characterRoots = new HashSet<Transform>();
    private readonly List<Transform> staleTargetRoots = new List<Transform>();
    private readonly List<Material> materialBuffer = new List<Material>();
    private readonly List<Renderer> removalBuffer = new List<Renderer>();
    private readonly List<Renderer> rendererSnapshot = new List<Renderer>();
    private readonly RaycastHit[] hits = new RaycastHit[HitCapacity];
    private MaterialPropertyBlock propertyBlock;

    private Camera ActiveCamera => targetCamera != null
        ? targetCamera
        : MainCameraAppoint.Current;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolvePlayerTarget();
    }

    private void OnDisable()
    {
        RestoreAllRenderers();
    }

    private void OnDestroy()
    {
        RestoreAllRenderers();
    }

    private void LateUpdate()
    {
        ResolvePlayerTarget();
        RefreshOcclusionTargets();
        blockers.Clear();

        Camera activeCamera = ActiveCamera;
        if (activeCamera != null)
        {
            foreach (OcclusionTarget target in occlusionTargets)
            {
                if (TryGetTargetPoint(target, out Vector3 targetPoint))
                {
                    CollectBlockers(
                        activeCamera,
                        targetPoint,
                        playerRayEndMargin,
                        target.targetCollider);
                }
            }

            GridHoverController hoverController = GridHoverController.Instance;
            if (hoverController != null &&
                hoverController.TryGetCurrentOcclusionTargetCell(out CellManager hoverCell))
            {
                CollectBlockers(
                    activeCamera,
                    GetCellTargetPoint(hoverCell),
                    cellRayEndMargin,
                    null);
            }
        }

        UpdateRendererFades();
    }

    private void CollectBlockers(
        Camera activeCamera,
        Vector3 target,
        float endMargin,
        Collider targetCollider)
    {
        Vector3 viewportPoint = activeCamera.WorldToViewportPoint(target);
        if (viewportPoint.z <= 0f)
        {
            return;
        }

        Ray viewRay = activeCamera.ViewportPointToRay(viewportPoint);
        float targetDistance =
            Vector3.Dot(target - viewRay.origin, viewRay.direction) - endMargin;
        if (targetDistance <= 0.001f)
        {
            return;
        }

        int hitCount = Physics.RaycastNonAlloc(
            viewRay.origin,
            viewRay.direction,
            hits,
            targetDistance,
            occlusionLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null ||
                hitCollider.GetComponentInParent<CellManager>() != null)
            {
                continue;
            }

            Transform hitTransform = hitCollider.transform;
            if (IsCharacterTransform(hitTransform) ||
                IsPartOf(hitTransform, activeCamera.transform))
            {
                continue;
            }

            if (IsTargetSupportSurfaceHit(hits[i], targetCollider))
            {
                continue;
            }

            Renderer blocker = FindRenderer(hitCollider);
            if (blocker != null &&
                blocker.enabled &&
                SupportsOcclusionFade(blocker))
            {
                blockers.Add(blocker);
            }
        }
    }

    private bool IsTargetSupportSurfaceHit(RaycastHit hit, Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        Bounds targetBounds = targetCollider.bounds;
        Vector3 targetFootPoint = new Vector3(
            targetBounds.center.x,
            targetBounds.min.y,
            targetBounds.center.z);

        if (hit.normal.y < supportSurfaceMinNormalY ||
            hit.point.y < targetFootPoint.y - supportSurfaceHeightTolerance ||
            hit.point.y > targetFootPoint.y + supportSurfaceHeightTolerance)
        {
            return false;
        }

        Vector3 hitOffset = hit.point - targetFootPoint;
        hitOffset.y = 0f;

        Vector3 colliderExtents = targetBounds.extents;
        float horizontalRadius =
            supportSurfaceRadiusPadding + Mathf.Max(colliderExtents.x, colliderExtents.z);

        return hitOffset.sqrMagnitude <= horizontalRadius * horizontalRadius;
    }

    private bool SupportsOcclusionFade(Renderer renderer)
    {
        materialBuffer.Clear();
        renderer.GetSharedMaterials(materialBuffer);

        foreach (Material material in materialBuffer)
        {
            if (material != null && material.HasProperty(OcclusionAlphaId))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 GetCellTargetPoint(CellManager cell)
    {
        Collider cellCollider = cell.GetComponent<Collider>();
        if (cellCollider == null)
        {
            cellCollider = cell.GetComponentInChildren<Collider>();
        }

        if (cellCollider == null)
        {
            return cell.transform.position;
        }

        Bounds bounds = cellCollider.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget == null && playerMoveController == null)
        {
            playerMoveController = FindFirstObjectByType<PlayerMoveController>();
        }

        if (playerTarget == null && playerMoveController != null)
        {
            playerTarget = playerMoveController.PlayerTransform;
        }
    }

    private void RefreshOcclusionTargets()
    {
        occlusionTargets.Clear();
        characterRoots.Clear();

        AddOcclusionTarget(playerTarget);

        MonsterIdentitySystem monsterSystem = MonsterIdentitySystem.Instance;
        if (monsterSystem != null)
        {
            foreach (MonsterIdentityManager monster in monsterSystem.GetAllMonsters())
            {
                if (monster == null ||
                    !monster.isActiveAndEnabled ||
                    !monster.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (gameObject.scene.IsValid() &&
                    monster.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                AddOcclusionTarget(monster.transform);
            }
        }

        staleTargetRoots.Clear();
        foreach (KeyValuePair<Transform, OcclusionTarget> pair in occlusionTargetCache)
        {
            if (pair.Key == null || !characterRoots.Contains(pair.Key))
            {
                staleTargetRoots.Add(pair.Key);
            }
        }

        foreach (Transform staleRoot in staleTargetRoots)
        {
            occlusionTargetCache.Remove(staleRoot);
        }
    }

    private void AddOcclusionTarget(Transform root)
    {
        if (root == null || !root.gameObject.activeInHierarchy)
        {
            return;
        }

        if (!characterRoots.Add(root))
        {
            return;
        }

        if (!occlusionTargetCache.TryGetValue(root, out OcclusionTarget target) ||
            target == null)
        {
            target = new OcclusionTarget();
            occlusionTargetCache[root] = target;
        }

        target.root = root;
        if (!IsColliderUsable(target.targetCollider))
        {
            target.targetCollider = FindTargetCollider(root);
        }

        if (target.targetCollider != null)
        {
            occlusionTargets.Add(target);
        }
    }

    private bool TryGetTargetPoint(OcclusionTarget target, out Vector3 targetPoint)
    {
        targetPoint = default;
        if (target == null ||
            target.root == null ||
            !IsColliderUsable(target.targetCollider))
        {
            return false;
        }

        Bounds bounds = target.targetCollider.bounds;
        targetPoint = new Vector3(
            bounds.center.x,
            bounds.min.y + playerTargetHeightOffset,
            bounds.center.z);
        return true;
    }

    private static Collider FindTargetCollider(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Collider rootCollider = target.GetComponent<Collider>();
        if (rootCollider != null &&
            rootCollider.enabled &&
            rootCollider.gameObject.activeInHierarchy)
        {
            return rootCollider;
        }

        Collider[] childColliders = target.GetComponentsInChildren<Collider>(true);
        Collider triggerFallback = null;

        foreach (Collider childCollider in childColliders)
        {
            if (childCollider == null ||
                !childCollider.enabled ||
                !childCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!childCollider.isTrigger)
            {
                return childCollider;
            }

            if (triggerFallback == null)
            {
                triggerFallback = childCollider;
            }
        }

        return triggerFallback;
    }

    private bool IsCharacterTransform(Transform hitTransform)
    {
        foreach (Transform characterRoot in characterRoots)
        {
            if (IsPartOf(hitTransform, characterRoot))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsColliderUsable(Collider collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.activeInHierarchy;
    }

    private static bool IsPartOf(Transform transform, Transform root)
    {
        return transform == root || transform.IsChildOf(root);
    }

    private static Renderer FindRenderer(Collider collider)
    {
        Renderer renderer = collider.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = collider.GetComponentInParent<Renderer>();
        }

        return renderer != null
            ? renderer
            : collider.GetComponentInChildren<Renderer>();
    }

    private void UpdateRendererFades()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float targetAlpha = Mathf.Clamp01(occludedAlpha);

        // 刷新“最后被遮挡时间”
        foreach (Renderer blocker in blockers)
        {
            if (!fadeStates.TryGetValue(blocker, out FadeState state))
            {
                state = new FadeState();
                fadeStates.Add(blocker, state);
            }

            state.lastBlockedTime = now;
        }

        removalBuffer.Clear();
        rendererSnapshot.Clear();
        rendererSnapshot.AddRange(fadeStates.Keys);

        foreach (Renderer r in rendererSnapshot)
        {
            if (r == null)
            {
                removalBuffer.Add(r);
                continue;
            }

            FadeState state = fadeStates[r];

            // 滞后判定：短时间内丢失命中仍视为遮挡，避免逐帧抖动
            bool isBlocking = now - state.lastBlockedTime <= releaseDelay;

            float target = isBlocking ? targetAlpha : 1f;
            float speed = isBlocking ? fadeOutSpeed : fadeInSpeed;
            float step = speed <= 0f ? Mathf.Infinity : speed * dt;

            state.alpha = Mathf.MoveTowards(state.alpha, target, step);
            ApplyAlpha(r, state.alpha);

            if (!isBlocking && state.alpha >= 1f)
            {
                removalBuffer.Add(r);
            }
        }

        foreach (Renderer r in removalBuffer)
        {
            // 销毁的 Renderer 作为字典键仍可被移除（Unity 对象的引用比较）
            fadeStates.Remove(r);
        }
    }

    private void ApplyAlpha(Renderer renderer, float alpha)
    {
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(OcclusionAlphaId, Mathf.Clamp01(alpha));
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void RestoreAllRenderers()
    {
        if (propertyBlock != null)
        {
            rendererSnapshot.Clear();
            rendererSnapshot.AddRange(fadeStates.Keys);

            foreach (Renderer r in rendererSnapshot)
            {
                if (r != null)
                {
                    ApplyAlpha(r, 1f);
                }
            }
        }

        blockers.Clear();
        fadeStates.Clear();
        materialBuffer.Clear();
        removalBuffer.Clear();
        rendererSnapshot.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugLine)
        {
            return;
        }

        Camera activeCamera = ActiveCamera;
        if (activeCamera == null)
        {
            return;
        }

        Transform debugPlayer = playerTarget;
        if (debugPlayer == null)
        {
            PlayerMoveController controller = playerMoveController != null
                ? playerMoveController
                : FindFirstObjectByType<PlayerMoveController>();
            debugPlayer = controller != null ? controller.PlayerTransform : null;
        }

        List<Transform> debugTargets = new List<Transform>();
        if (debugPlayer != null)
        {
            debugTargets.Add(debugPlayer);
        }

        MonsterIdentitySystem monsterSystem = Application.isPlaying
            ? MonsterIdentitySystem.Instance
            : FindFirstObjectByType<MonsterIdentitySystem>();
        if (monsterSystem != null)
        {
            foreach (MonsterIdentityManager monster in monsterSystem.GetAllMonsters())
            {
                if (monster == null ||
                    !monster.gameObject.activeInHierarchy ||
                    debugTargets.Contains(monster.transform))
                {
                    continue;
                }

                if (gameObject.scene.IsValid() &&
                    monster.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                debugTargets.Add(monster.transform);
            }
        }

        foreach (Transform debugTarget in debugTargets)
        {
            Collider debugCollider = FindTargetCollider(debugTarget);
            if (debugCollider == null)
            {
                continue;
            }

            Bounds debugBounds = debugCollider.bounds;
            Vector3 targetPoint = new Vector3(
                debugBounds.center.x,
                debugBounds.min.y + playerTargetHeightOffset,
                debugBounds.center.z);
            Color debugColor = debugTarget == debugPlayer
                ? new Color(1f, 0.6f, 0.1f, 0.9f)
                : new Color(1f, 0.25f, 0.2f, 0.9f);
            DrawOcclusionRay(
                activeCamera,
                targetPoint,
                playerRayEndMargin,
                debugColor);
        }

        GridHoverController hoverController = GridHoverController.Instance;
        if (hoverController != null &&
            hoverController.TryGetCurrentOcclusionTargetCell(out CellManager hoverCell))
        {
            DrawOcclusionRay(
                activeCamera,
                GetCellTargetPoint(hoverCell),
                cellRayEndMargin,
                new Color(0.1f, 0.9f, 1f, 0.9f));
        }
    }

    private static void DrawOcclusionRay(
        Camera activeCamera,
        Vector3 target,
        float endMargin,
        Color color)
    {
        Vector3 viewportPoint = activeCamera.WorldToViewportPoint(target);
        if (viewportPoint.z <= 0f)
        {
            return;
        }

        Ray viewRay = activeCamera.ViewportPointToRay(viewportPoint);
        float targetDistance =
            Vector3.Dot(target - viewRay.origin, viewRay.direction) - endMargin;
        if (targetDistance <= 0.001f)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawLine(
            viewRay.origin,
            viewRay.origin + viewRay.direction * targetDistance);
    }
}
