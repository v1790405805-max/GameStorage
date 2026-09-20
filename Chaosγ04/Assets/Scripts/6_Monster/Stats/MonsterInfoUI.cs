using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonsterInfoUI : MonoBehaviour
{
    [Header("血条绑定")]
    [SerializeField] private Image currentHpBar;     // 当前实际血条（绿色/红色）
    [SerializeField] private Image previewHpBar;     // 扣血预览条（黄色/半透明红）
    [SerializeField] private TextMeshProUGUI hpText; // 血量文本（例如 "15/20"）

    [Header("意图 (Intent) 绑定")]
    [SerializeField] private GameObject intentRoot;  // 意图整体父物体
    [SerializeField] private Image intentIcon;       // 动作图标（剑/盾/位移）
    [SerializeField] private TextMeshProUGUI intentValueText; // 攻击数值文本

    [Header("悬停伤害预览浮字")]
    [SerializeField] private GameObject previewDamageRoot;    // 浮字父物体
    [SerializeField] private TextMeshProUGUI previewDamageText;// 预计扣血文本（例如 "-6"）

    [Header("展示配置")]
    [Tooltip("是否始终朝向主摄像机。若勾选，血条不会因视角倾斜变形；若不勾选，可贴合地块平躺")]
    [SerializeField] private bool billboardToCamera = true;

    private MonsterStats monsterStats;
    private Camera mainCamera;

    private void Awake()
    {
        monsterStats = GetComponentInParent<MonsterStats>();
        mainCamera = Camera.main;
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
        if (billboardToCamera && mainCamera != null)
        {
            transform.forward = mainCamera.transform.forward;
        }
    }

    // ==================== 1. 基础血条与数值刷新 ====================

    public void UpdateHpDisplay(int currentHp, int maxHp)
    {
        if (maxHp <= 0) return;

        float fillRatio = (float)currentHp / maxHp;
        if (currentHpBar != null) currentHpBar.fillAmount = fillRatio;
        if (previewHpBar != null) previewHpBar.fillAmount = fillRatio;

        if (hpText != null)
        {
            hpText.text = $"{currentHp}/{maxHp}";
        }
    }

    // ==================== 2. 意图显示/隐藏 ====================

    public void SetIntent(Sprite icon, int value, bool showValue = true)
    {
        if (intentRoot != null) intentRoot.SetActive(true);
        if (intentIcon != null && icon != null) intentIcon.sprite = icon;

        if (intentValueText != null)
        {
            intentValueText.gameObject.SetActive(showValue);
            intentValueText.text = value > 0 ? value.ToString() : "";
        }
    }

    public void HideIntent()
    {
        if (intentRoot != null) intentRoot.SetActive(false);
    }

    // ==================== 3. 悬停卡牌时的伤害预估 ====================

    /// <summary>
    /// 当玩家拖拽卡牌悬停在攻击范围内的该怪物身上时调用
    /// </summary>
    public void ShowDamagePreview(int estimatedDamage)
    {
        if (monsterStats == null || estimatedDamage <= 0) return;

        int currentHp = monsterStats.currentHp;
        int maxHp = monsterStats.maxHp;
        int currentBlock = monsterStats.currentBlock;

        // 计算穿透护盾后的预计掉血
        int realHpDamage = Mathf.Max(0, estimatedDamage - currentBlock);
        int expectedHp = Mathf.Max(0, currentHp - realHpDamage);

        // 刷新预估血条：主血条缩到预期值，底层预览条保留当前血量，形成扣血空隙
        if (currentHpBar != null)
        {
            currentHpBar.fillAmount = (float)expectedHp / maxHp;
        }

        // 显示伤害预估浮字
        if (previewDamageRoot != null) previewDamageRoot.SetActive(true);
        if (previewDamageText != null)
        {
            previewDamageText.text = $"-{estimatedDamage}";
        }
    }

    /// <summary>
    /// 鼠标移开怪物、或结束拖拽卡牌时调用恢复
    /// </summary>
    public void HideDamagePreview()
    {
        if (monsterStats != null)
        {
            float fillRatio = (float)monsterStats.currentHp / monsterStats.maxHp;
            if (currentHpBar != null) currentHpBar.fillAmount = fillRatio;
            if (previewHpBar != null) previewHpBar.fillAmount = fillRatio;
        }

        if (previewDamageRoot != null) previewDamageRoot.SetActive(false);
    }
}