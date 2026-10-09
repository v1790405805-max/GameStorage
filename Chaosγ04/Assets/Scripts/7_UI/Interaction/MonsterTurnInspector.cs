using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 怪物回合相机导演。
/// 玩家回合结束时保存相机状态；怪物回合期间聚焦到正在执行 Action 的怪物；
/// 玩家回合重新开始时恢复进入怪物回合前的相机状态。
/// </summary>
[DisallowMultipleComponent]
public class MonsterTurnInspector : MonoBehaviour
{
    [Header("怪物回合相机")]
    [Tooltip("相机持续跟随怪物时的平滑时间（秒）。数值越小，相机越紧跟怪物。")]
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.15f;

    [Tooltip("怪物回合结束时，相机恢复到进入怪物回合时状态所需的时间（秒）。")]
    [SerializeField, Min(0.01f)] private float restoreDuration = 0.4f;

    [Tooltip("怪物回合结束时，相机恢复位移和镜头尺寸使用的 DOTween 缓动曲线。")]
    [SerializeField] private Ease restoreEase = Ease.OutCubic;

    // 当前怪物回合中已订阅的 ActionManager，防止重复添加事件。
    private readonly List<MonsterActionManager> subscribedManagers =
        new List<MonsterActionManager>();

    // 记录上一次扫描时处于激活状态的怪物，用于识别本回合中新生成或重新激活的怪物。
    private readonly HashSet<MonsterActionManager> knownActiveManagers =
        new HashSet<MonsterActionManager>();
    private readonly HashSet<MonsterActionManager> activeManagersThisScan =
        new HashSet<MonsterActionManager>();

    // 当前怪物回合使用的相机，以及需要恢复的完整镜头状态。
    private Camera mainCamera;
    private CameraSnapshot turnStartSnapshot;
    private Vector3 focusOffset;
    private Vector3 followVelocity;
    private Transform currentFocusTarget;
    private bool hasCameraSnapshot;
    private bool isTrackingMonsterTurn;

    private struct CameraSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool orthographic;
        public float orthographicSize;
        public float fieldOfView;

        public static CameraSnapshot Capture(Camera camera)
        {
            return new CameraSnapshot
            {
                position = camera.transform.position,
                rotation = camera.transform.rotation,
                orthographic = camera.orthographic,
                orthographicSize = camera.orthographicSize,
                fieldOfView = camera.fieldOfView
            };
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[MonsterTurnInspector] 未找到 Camera.main，无法执行怪物 Action 聚焦。");
        }

        if (TurnManager.Instance == null)
        {
            Debug.LogError("[MonsterTurnInspector] 未找到 TurnManager，无法监听怪物回合。");
            return;
        }

        // 提前订阅全场怪物，避免 TurnManager 已经启动第一个 Action 后才开始监听。
        SyncMonsterRoster(false);
    }

    private void OnDestroy()
    {
        ClearMonsterSubscriptions();
        knownActiveManagers.Clear();
        activeManagersThisScan.Clear();
        DOTween.Kill(this);
    }

    private void Update()
    {
        if (TurnManager.Instance == null)
        {
            return;
        }

        if (!isTrackingMonsterTurn &&
            TurnManager.Instance.CurrentTurn == TurnState.Enemy)
        {
            BeginMonsterTurn();
        }
        else if (isTrackingMonsterTurn &&
                 TurnManager.Instance.CurrentTurn == TurnState.Player)
        {
            EndMonsterTurn();
        }

        if (isTrackingMonsterTurn)
        {
            // 每帧检查新生成的怪物；新怪物出现后会立即抢到当前聚焦权。
            SyncMonsterRoster(true);
        }
    }

    private void BeginMonsterTurn()
    {
        if (isTrackingMonsterTurn)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogError("[MonsterTurnInspector] 没有可用的主相机，怪物回合聚焦已跳过。");
            return;
        }

        // 清掉本脚本上一次尚未结束的移动，避免两次聚焦互相叠加。
        DOTween.Kill(this);

        // 记录进入怪物回合时的镜头状态，回合结束后原样恢复。
        turnStartSnapshot = CameraSnapshot.Capture(mainCamera);
        focusOffset = CalculateFocusOffset(mainCamera);
        followVelocity = Vector3.zero;
        currentFocusTarget = null;
        hasCameraSnapshot = true;
        isTrackingMonsterTurn = true;

        // 先记录怪物回合开始时的已有怪物，避免把它们的首次扫描误判为新生成。
        SyncMonsterRoster(false);
    }

    private void EndMonsterTurn()
    {
        if (!isTrackingMonsterTurn)
        {
            return;
        }

        currentFocusTarget = null;
        followVelocity = Vector3.zero;

        RestoreCameraState();

        isTrackingMonsterTurn = false;
        hasCameraSnapshot = false;
    }

    private void HandleActionStarted(MonsterActionBase action)
    {
        if (action == null)
        {
            return;
        }

        // 兼容第一个 Action 在 Update 察觉敌方回合之前启动的情况。
        if (!isTrackingMonsterTurn &&
            TurnManager.Instance != null &&
            TurnManager.Instance.CurrentTurn == TurnState.Enemy)
        {
            BeginMonsterTurn();
        }

        if (!isTrackingMonsterTurn || mainCamera == null)
        {
            return;
        }

        Transform focusTarget = ResolveFocusTarget(action);
        if (focusTarget == null)
        {
            return;
        }

        // Action 开始后立即切换到对应怪物。
        SwitchFocusTarget(focusTarget);

        SyncMonsterRoster(false);
    }

    private void HandleActionFinished(MonsterActionBase action)
    {
        // 怪物可能在 Action 执行期间被召唤出来，结束时刷新一次即可纳入后续行动。
        SyncMonsterRoster(false);
    }

    private void LateUpdate()
    {
        if (!isTrackingMonsterTurn || mainCamera == null)
        {
            return;
        }

        FollowCurrentTarget();
    }

    private void SwitchFocusTarget(Transform focusTarget)
    {
        if (focusTarget == currentFocusTarget)
        {
            return;
        }

        currentFocusTarget = focusTarget;
        followVelocity = Vector3.zero;
    }

    private void FollowCurrentTarget()
    {
        if (currentFocusTarget == null)
        {
            return;
        }

        Vector3 targetCameraPosition =
            ResolveFocusPoint(currentFocusTarget) + focusOffset;

        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetCameraPosition,
            ref followVelocity,
            followSmoothTime);
    }

    private static Vector3 ResolveFocusPoint(Transform focusTarget)
    {
        // 与 MapInspector 的聚焦规则保持一致：优先使用目标及其子物体上的第一个 Collider。
        Collider targetCollider = focusTarget.GetComponentInChildren<Collider>();

        return targetCollider != null
            ? targetCollider.bounds.center
            : focusTarget.position;
    }

    private Transform ResolveFocusTarget(MonsterActionBase action)
    {
        MonsterActionManager actionManager = action.GetComponentInParent<MonsterActionManager>();
        Transform managerFocusTarget = ResolveFocusTarget(actionManager);
        if (managerFocusTarget != null)
        {
            return managerFocusTarget;
        }

        MonsterIdentityManager actionIdentity =
            action.GetComponentInParent<MonsterIdentityManager>();

        return actionIdentity != null ? actionIdentity.transform : action.transform;
    }

    private static Transform ResolveFocusTarget(MonsterActionManager actionManager)
    {
        if (actionManager == null)
        {
            return null;
        }

        MonsterIdentityManager identity =
            actionManager.GetComponentInParent<MonsterIdentityManager>();

        return identity != null ? identity.transform : actionManager.transform;
    }

    private void SyncMonsterRoster(bool focusNewMonsters)
    {
        MonsterActionManager[] managers = FindObjectsByType<MonsterActionManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        activeManagersThisScan.Clear();

        foreach (MonsterActionManager manager in managers)
        {
            if (manager == null)
            {
                continue;
            }

            if (!subscribedManagers.Contains(manager))
            {
                manager.OnActionStarted += HandleActionStarted;
                manager.OnActionFinished += HandleActionFinished;
                subscribedManagers.Add(manager);
            }

            if (!manager.isActiveAndEnabled)
            {
                continue;
            }

            activeManagersThisScan.Add(manager);

            if (focusNewMonsters &&
                isTrackingMonsterTurn &&
                !knownActiveManagers.Contains(manager))
            {
                FocusNewlySpawnedMonster(manager);
            }
        }

        knownActiveManagers.Clear();
        knownActiveManagers.UnionWith(activeManagersThisScan);
    }

    private void FocusNewlySpawnedMonster(MonsterActionManager actionManager)
    {
        if (!isTrackingMonsterTurn || mainCamera == null || actionManager == null)
        {
            return;
        }

        Transform focusTarget = ResolveFocusTarget(actionManager);
        if (focusTarget == null)
        {
            return;
        }

        // 新怪物优先级最高，立即聚焦到新怪物。
        SwitchFocusTarget(focusTarget);
    }

    private void ClearMonsterSubscriptions()
    {
        foreach (MonsterActionManager manager in subscribedManagers)
        {
            if (manager == null)
            {
                continue;
            }

            manager.OnActionStarted -= HandleActionStarted;
            manager.OnActionFinished -= HandleActionFinished;
        }

        subscribedManagers.Clear();
    }

    private static Vector3 CalculateFocusOffset(Camera camera)
    {
        // 沿用 MapInspector 的屏幕中心偏移方式，保证聚焦点在画面中心。
        Ray centerRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(centerRay, out float enter))
        {
            Vector3 screenCenterGroundPosition = centerRay.GetPoint(enter);
            return camera.transform.position - screenCenterGroundPosition;
        }

        return -camera.transform.forward * Mathf.Max(10f, camera.nearClipPlane + 1f);
    }

    private void RestoreCameraState()
    {
        if (mainCamera == null || !hasCameraSnapshot)
        {
            return;
        }

        DOTween.Kill(this);

        // 位置、旋转和镜头尺寸分别恢复，避免怪物回合期间被其他操作改变。
        mainCamera.transform
            .DOMove(turnStartSnapshot.position, restoreDuration)
            .SetEase(restoreEase)
            .SetId(this);

        mainCamera.transform
            .DORotateQuaternion(turnStartSnapshot.rotation, restoreDuration)
            .SetEase(restoreEase)
            .SetId(this);

        if (turnStartSnapshot.orthographic)
        {
            mainCamera
                .DOOrthoSize(turnStartSnapshot.orthographicSize, restoreDuration)
                .SetEase(restoreEase)
                .SetId(this);
        }
        else
        {
            mainCamera
                .DOFieldOfView(turnStartSnapshot.fieldOfView, restoreDuration)
                .SetEase(restoreEase)
                .SetId(this);
        }
    }
}
