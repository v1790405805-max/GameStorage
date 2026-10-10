using UnityEngine;

/// <summary>
/// 统一管理 Player_WhenMove 预览实例，供手动移动和 Movement 卡牌共用。
/// 预览动画方向始终以场景中的 Player Animator 为准。
/// </summary>
public class PlayerMovementPreviewer : MonoBehaviour
{
    private const float PreviewHeightOffset = 0.653f;

    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");

    [SerializeField] private GameObject previewPrefab;

    private Transform playerTransform;
    private Animator playerAnimator;

    private GameObject previewInstance;
    private Animator previewAnimator;
    private CellManager previewCell;
    private object activeOwner;

    private bool hasDirectionOverride = false;
    private float directionOverrideHorizontal;
    private float directionOverrideVertical;

    public bool IsVisible => previewInstance != null && previewInstance.activeSelf;

    public void Initialize(GameObject prefab, Transform player, Animator playerCharacterAnimator)
    {
        previewPrefab = prefab;
        playerTransform = player;
        playerAnimator = playerCharacterAnimator;

        ResolvePlayerAnimator();
        if (IsVisible)
            SyncPreview();
    }

    public void Show(CellManager targetCell, object owner)
    {
        if (targetCell == null)
        {
            Hide(owner);
            return;
        }

        if (activeOwner != null && !ReferenceEquals(activeOwner, owner))
            HideAll();

        if (previewPrefab == null)
            return;

        EnsurePreviewInstance();
        if (previewInstance == null)
            return;

        activeOwner = owner;

        if (previewCell == targetCell && previewInstance.activeSelf)
        {
            SyncPreview();
            return;
        }

        previewCell = targetCell;

        Vector3 previewPosition = targetCell.transform.position;
        previewPosition.y = targetCell.transform.position.y + PreviewHeightOffset;
        previewInstance.transform.position = previewPosition;
        previewInstance.SetActive(true);

        SyncPreview();
    }

    public void Hide(object owner)
    {
        if (activeOwner != null && !ReferenceEquals(activeOwner, owner))
            return;

        HideAll();
    }

    public void HideAll()
    {
        activeOwner = null;
        previewCell = null;
        hasDirectionOverride = false;

        if (previewInstance != null)
            previewInstance.SetActive(false);
    }

    public void PreviewDirection(float horizontal, float vertical, object owner)
    {
        if (!ReferenceEquals(activeOwner, owner) || !IsVisible)
            return;

        hasDirectionOverride = true;
        directionOverrideHorizontal = horizontal;
        directionOverrideVertical = vertical;
        ApplyDirection(horizontal, vertical);
    }

    public void RestoreDirection(object owner)
    {
        if (!ReferenceEquals(activeOwner, owner) || !hasDirectionOverride)
            return;

        hasDirectionOverride = false;
        SyncPreview();
    }

    private void LateUpdate()
    {
        if (activeOwner != null)
            SyncPreview();
    }

    private void OnDestroy()
    {
        if (previewInstance != null)
            Destroy(previewInstance);
    }

    private void EnsurePreviewInstance()
    {
        if (previewInstance != null)
            return;

        previewInstance = Instantiate(previewPrefab);
        previewInstance.name = $"{previewPrefab.name} (Runtime Preview)";
        previewAnimator = previewInstance.GetComponentInChildren<Animator>(true);
        previewInstance.SetActive(false);
    }

    private void SyncPreview()
    {
        if (!IsVisible || previewAnimator == null)
            return;

        if (hasDirectionOverride)
        {
            ApplyDirection(directionOverrideHorizontal, directionOverrideVertical);
            return;
        }

        ResolvePlayerAnimator();
        if (playerAnimator == null)
            return;

        ApplyDirection(
            playerAnimator.GetFloat(HorizontalHash),
            playerAnimator.GetFloat(VerticalHash));
    }

    private void ResolvePlayerAnimator()
    {
        if (playerAnimator != null)
            return;

        if (playerTransform != null)
            playerAnimator = playerTransform.GetComponentInChildren<Animator>(true);

        if (playerAnimator != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        playerTransform = playerObject.transform;
        playerAnimator = playerObject.GetComponentInChildren<Animator>(true);
    }

    private void ApplyDirection(float horizontal, float vertical)
    {
        previewAnimator.SetFloat(HorizontalHash, horizontal);
        previewAnimator.SetFloat(VerticalHash, vertical);
        previewAnimator.Update(0f);
    }
}
