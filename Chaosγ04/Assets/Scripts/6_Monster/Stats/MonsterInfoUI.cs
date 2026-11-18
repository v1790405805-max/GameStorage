using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonsterInfoUI : MonoBehaviour
{
    [Header("生命显示")]
    [Tooltip("当前生命值血条。")]
    [SerializeField] private Image currentHpBar;
    [Tooltip("伤害预览时显示预计损失血量的底层血条。")]
    [SerializeField] private Image previewHpBar;
    [Tooltip("生命值文本，例如 15/20。")]
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("意图显示")]
    [Tooltip("意图区域的根节点。")]
    [SerializeField] private GameObject intentRoot;
    [Tooltip("意图图标。")]
    [SerializeField] private Image intentIcon;
    [Tooltip("意图数值文本。")]
    [SerializeField] private TextMeshProUGUI intentValueText;

    [Header("伤害预览显示")]
    [Tooltip("伤害预览的根节点。")]
    [SerializeField] private GameObject previewDamageRoot;
    [Tooltip("预计伤害文本。")]
    [SerializeField] private TextMeshProUGUI previewDamageText;

    [Header("显示设置")]
    [Tooltip("是否始终朝向当前主摄像机。")]
    [SerializeField] private bool billboardToCamera = true;

    private MonsterStats monsterStats;

    private Camera MainCamera => MainCameraAppoint.Current;

    private void Awake()
    {
        monsterStats = GetComponentInParent<MonsterStats>();
    }

    private void Start()
    {
        if (monsterStats != null)
        {
            UpdateHpDisplay(monsterStats.currentHp, monsterStats.maxHp);
        }

        HideDamagePreview();
    }

    private void LateUpdate()
    {
        if (billboardToCamera && MainCamera != null)
        {
            transform.forward = MainCamera.transform.forward;
        }
    }

    public void UpdateHpDisplay(int currentHp, int maxHp)
    {
        if (maxHp <= 0)
        {
            return;
        }

        float fillRatio = (float)currentHp / maxHp;
        if (currentHpBar != null)
        {
            currentHpBar.fillAmount = fillRatio;
        }

        if (previewHpBar != null)
        {
            previewHpBar.fillAmount = fillRatio;
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHp}/{maxHp}";
        }
    }

    public void SetIntent(Sprite icon, int value, bool showValue = true)
    {
        if (intentRoot != null)
        {
            intentRoot.SetActive(true);
        }

        if (intentIcon != null && icon != null)
        {
            intentIcon.sprite = icon;
        }

        if (intentValueText != null)
        {
            intentValueText.gameObject.SetActive(showValue);
            intentValueText.text = value > 0 ? value.ToString() : string.Empty;
        }
    }

    public void HideIntent()
    {
        if (intentRoot != null)
        {
            intentRoot.SetActive(false);
        }
    }

    public void ShowDamagePreview(int estimatedDamage)
    {
        if (monsterStats == null || estimatedDamage <= 0)
        {
            return;
        }

        int currentHp = monsterStats.currentHp;
        int maxHp = monsterStats.maxHp;
        int currentArmor = monsterStats.currentArmor;
        int realHpDamage = Mathf.Max(0, estimatedDamage - currentArmor);
        int expectedHp = Mathf.Max(0, currentHp - realHpDamage);

        if (currentHpBar != null)
        {
            currentHpBar.fillAmount = (float)expectedHp / maxHp;
        }

        if (previewDamageRoot != null)
        {
            previewDamageRoot.SetActive(true);
        }

        if (previewDamageText != null)
        {
            previewDamageText.text = $"-{estimatedDamage}";
        }
    }

    public void HideDamagePreview()
    {
        if (monsterStats != null)
        {
            float fillRatio = (float)monsterStats.currentHp / monsterStats.maxHp;
            if (currentHpBar != null)
            {
                currentHpBar.fillAmount = fillRatio;
            }

            if (previewHpBar != null)
            {
                previewHpBar.fillAmount = fillRatio;
            }
        }

        if (previewDamageRoot != null)
        {
            previewDamageRoot.SetActive(false);
        }
    }
}
