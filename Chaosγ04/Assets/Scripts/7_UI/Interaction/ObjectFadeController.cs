using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(110)]
public class ObjectFadeController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("检查视线的相机。留空时使用 MainCameraAppoint.Current。")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("需要保持可见的玩家 Transform。留空时自动查找 PlayerMoveController。")]
    [SerializeField] private Transform playerTarget;
    [Tooltip("用于自动解析玩家 Transform。留空时在场景中自动查找。")]
    [SerializeField] private PlayerMoveController playerMoveController;

    [Header("Occlusion Fade")]
    [Tooltip("阻挡物虚化时保持的透明度。1 为实体，0 为完全透明。")]
    [Range(0f, 1f)] public float occludedAlpha = 0.25f;
    [Tooltip("虚化与恢复速度。数值越大，过渡越快。")]
    [Min(0f)] public float fadeSpeed = 10f;
    [Tooltip("参与视线检测的物理层。")]
    public LayerMask occlusionLayers = ~0;

    [Header("Support Surface Filter")]
    [Tooltip("判定为支撑面所需的最低向上法线。数值越高，只忽略越平的地面。")]
    [Range(0f, 1f)] public float supportSurfaceMinNormalY = 0.7f;
    [Tooltip("命中点与脚底允许的最大高度差。用于覆盖角色轻微陷入平台的情况。")]
    [Min(0f)] public float supportSurfaceHeightTolerance = 0.3f;
    [Tooltip("在角色碰撞体水平尺寸之外额外允许的支撑面检测半径。")]
    [Min(0f)] public float supportSurfaceRadiusPadding = 0.25f;

    [Header("Debug")]
    [Tooltip("在 Scene 视图中绘制相机到玩家的检查线。")]
    public bool drawDebugLine = true;

    private const int HitCapacity = 32;
    private static readonly int OcclusionAlphaId = Shader.PropertyToID("_OcclusionAlpha");

    private readonly HashSet<Renderer> blockers = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, float> rendererAlphas = new Dictionary<Renderer, float>();
    private readonly List<Material> materialBuffer = new List<Material>();
    private readonly List<Renderer> removalBuffer = new List<Renderer>();
    private readonly List<Renderer> rendererSnapshot = new List<Renderer>();
    private readonly RaycastHit[] hits = new RaycastHit[HitCapacity];
    private MaterialPropertyBlock propertyBlock;
    private Transform cachedPlayerTarget;
    private Collider targetCollider;
    private float globalOcclusionAlpha = 1f;

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
        blockers.Clear();

        Camera activeCamera = ActiveCamera;
        if (activeCamera != null)
        {
            if (TryGetTargetPoint(out Vector3 playerTargetPoint))
            {
                CollectBlockers(activeCamera, playerTargetPoint);
            }

            GridHoverController hoverController = GridHoverController.Instance;
            if (hoverController != null &&
                hoverController.TryGetCurrentOcclusionTargetCell(out CellManager hoverCell))
            {
                CollectBlockers(activeCamera, GetCellTargetPoint(hoverCell));
            }
        }

        UpdateRendererFades();
    }

    private void CollectBlockers(Camera activeCamera, Vector3 target)
    {
        Vector3 viewportPoint = activeCamera.WorldToViewportPoint(target);
        if (viewportPoint.z <= 0f)
        {
            return;
        }

        Ray viewRay = activeCamera.ViewportPointToRay(viewportPoint);
        float targetDistance = Vector3.Dot(target - viewRay.origin, viewRay.direction);
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
            if (IsPartOf(hitTransform, playerTarget) ||
                IsPartOf(hitTransform, activeCamera.transform))
            {
                continue;
            }

            if (IsPlayerSupportSurfaceHit(hits[i]))
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

    private bool IsPlayerSupportSurfaceHit(RaycastHit hit)
    {
        if (targetCollider == null)
        {
            return false;
        }

        Bounds playerBounds = targetCollider.bounds;
        Vector3 playerFootPoint = new Vector3(
            playerBounds.center.x,
            playerBounds.min.y,
            playerBounds.center.z);

        if (hit.normal.y < supportSurfaceMinNormalY ||
            hit.point.y < playerFootPoint.y - supportSurfaceHeightTolerance ||
            hit.point.y > playerFootPoint.y + supportSurfaceHeightTolerance)
        {
            return false;
        }

        Vector3 hitOffset = hit.point - playerFootPoint;
        hitOffset.y = 0f;

        Vector3 colliderExtents = playerBounds.extents;
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

        if (cachedPlayerTarget != playerTarget)
        {
            cachedPlayerTarget = playerTarget;
            targetCollider = null;
        }

        if (targetCollider == null ||
            !targetCollider.enabled ||
            !targetCollider.gameObject.activeInHierarchy)
        {
            targetCollider = FindTargetCollider(playerTarget);
        }
    }

    private bool TryGetTargetPoint(out Vector3 targetPoint)
    {
        targetPoint = default;
        if (playerTarget == null || targetCollider == null)
        {
            return false;
        }

        Bounds bounds = targetCollider.bounds;
        targetPoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
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
        float targetAlpha = Mathf.Clamp01(occludedAlpha);
        float alphaStep = fadeSpeed <= 0f
            ? Mathf.Infinity
            : fadeSpeed * Time.unscaledDeltaTime;
        bool hasBlockers = blockers.Count > 0;
        globalOcclusionAlpha = Mathf.MoveTowards(
            globalOcclusionAlpha,
            hasBlockers ? targetAlpha : 1f,
            alphaStep);

        foreach (Renderer blocker in blockers)
        {
            if (!rendererAlphas.ContainsKey(blocker))
            {
                rendererAlphas.Add(blocker, globalOcclusionAlpha);
            }
        }

        removalBuffer.Clear();
        rendererSnapshot.Clear();
        rendererSnapshot.AddRange(rendererAlphas.Keys);

        foreach (Renderer blocker in rendererSnapshot)
        {
            if (!rendererAlphas.TryGetValue(blocker, out float currentValue))
            {
                continue;
            }

            if (blocker == null)
            {
                removalBuffer.Add(blocker);
                continue;
            }

            bool isBlocking = blockers.Contains(blocker);
            float currentAlpha = isBlocking
                ? globalOcclusionAlpha
                : Mathf.MoveTowards(currentValue, 1f, alphaStep);

            rendererAlphas[blocker] = currentAlpha;
            ApplyAlpha(blocker, currentAlpha);

            if (!isBlocking && Mathf.Approximately(currentAlpha, 1f))
            {
                removalBuffer.Add(blocker);
            }
        }

        foreach (Renderer blocker in removalBuffer)
        {
            rendererAlphas.Remove(blocker);
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
            rendererSnapshot.AddRange(rendererAlphas.Keys);

            foreach (Renderer blocker in rendererSnapshot)
            {
                if (blocker != null)
                {
                    ApplyAlpha(blocker, 1f);
                }

            }
        }

        blockers.Clear();
        rendererAlphas.Clear();
        globalOcclusionAlpha = 1f;
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

        if (debugPlayer != null)
        {
            Collider debugCollider = debugPlayer == playerTarget && targetCollider != null
                ? targetCollider
                : FindTargetCollider(debugPlayer);

            if (debugCollider != null)
            {
                Bounds debugBounds = debugCollider.bounds;
                Vector3 playerPoint = new Vector3(
                    debugBounds.center.x,
                    debugBounds.min.y,
                    debugBounds.center.z);
                DrawOcclusionRay(
                    activeCamera,
                    playerPoint,
                    new Color(1f, 0.6f, 0.1f, 0.9f));
            }
        }

        GridHoverController hoverController = GridHoverController.Instance;
        if (hoverController != null &&
            hoverController.TryGetCurrentOcclusionTargetCell(out CellManager hoverCell))
        {
            DrawOcclusionRay(
                activeCamera,
                GetCellTargetPoint(hoverCell),
                new Color(0.1f, 0.9f, 1f, 0.9f));
        }
    }

    private static void DrawOcclusionRay(Camera activeCamera, Vector3 target, Color color)
    {
        Vector3 viewportPoint = activeCamera.WorldToViewportPoint(target);
        if (viewportPoint.z <= 0f)
        {
            return;
        }

        Ray viewRay = activeCamera.ViewportPointToRay(viewportPoint);
        float targetDistance = Vector3.Dot(target - viewRay.origin, viewRay.direction);
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
