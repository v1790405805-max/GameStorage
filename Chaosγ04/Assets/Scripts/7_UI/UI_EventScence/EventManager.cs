using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// 事件选项数据定义
/// </summary>
[System.Serializable]
public class EventChoice
{
    [TextArea(1, 2)]
    public string buttonDescription; // 按钮上显示的文字说明
    public int maxHPCost;            // 扣除的最大生命上限 (0 代表不扣)
    public int goldReward;           // 获得的金币/晶石数量
    [TextArea(2, 4)]
    public string resultStoryText;   // 做出选择后追加的反馈故事文本
}

/// <summary>
/// 事件场景总控制器 (4_Event_Scene)
/// 负责事件文本展现、三选一利益抉择、全局数据结算与安全离开。
/// </summary>
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [Header("--- 1. 文本与插画组件 ---")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI storyText;
    [SerializeField] private Image illustrationImage;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("--- 2. 故事文本预设 ---")]
    [SerializeField] private string defaultTitle = "深渊贪婪的契约";
    [TextArea(3, 6)]
    [SerializeField] private string initialStory = "你穿过碎石嶙峋的裂谷，前方的石壁下蜷缩着一位披着破旧兜帽的行商。\n他面前摆放着一盏幽绿色的天平，天平一端是叮当作响的晶石，另一端则是一具枯竭的心脏容器。\n\n「旅人，世间万物皆有定价。献出你的生命力，我便给你带去财富……」他沙哑地低语道。";

    [Header("--- 3. 三个选项按钮与离开按钮 ---")]
    [SerializeField] private List<Button> optionButtons = new List<Button>();
    [SerializeField] private List<TextMeshProUGUI> optionTexts = new List<TextMeshProUGUI>();
    [SerializeField] private Button leaveButton;

    [Header("--- 4. 场景转场配置 ---")]
    [SerializeField] private string mapSceneName = "1_Map_Scene";

    [Header("--- 5. 三个选项的收益与代价配置 ---")]
    [SerializeField]
    private List<EventChoice> choices = new List<EventChoice>()
    {
        new EventChoice
        {
            buttonDescription = "【拾取碎屑】从地上捡走散落的晶石 (+10 晶石)",
            maxHPCost = 0,
            goldReward = 10,
            resultStoryText = "你没有理会商人充满诱惑的低语，只是弯腰捡起了散落在地上的几块碎晶石。商人发出一声不屑的嗤笑，缓缓隐入阴影中……"
        },
        new EventChoice
        {
            buttonDescription = "【轻触天平】献出部分生机 (-10 生命上限，+50 晶石)",
            maxHPCost = 10,
            goldReward = 50,
            resultStoryText = "你将手掌按在天平上，一股冰凉的抽离感划过四肢。一袋沉甸甸的晶石落入你的怀中，你的脚步沉重了几分，但钱包充实了。"
        },
        new EventChoice
        {
            buttonDescription = "【深渊豪赌】饮下暗影契约 (-20 生命上限，+150 晶石)",
            maxHPCost = 20,
            goldReward = 150,
            resultStoryText = "「明智的抉择！」行商眼中爆发出异样的光彩。恐怖的暗影撕扯着你的灵魂，你感到生命本源被永久剥离，换来的是近乎狂暴的财富喷涌！"
        }
    };

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        InitEventUI();

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(false); // 刚进事件未做选择时隐藏离开按钮
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }

        UpdateTopBarData();
    }

    /// <summary>
    /// 初始化界面与选项状态
    /// </summary>
    private void InitEventUI()
    {
        if (titleText != null) titleText.text = defaultTitle;
        if (storyText != null) storyText.text = initialStory;

        int currentMaxHP = RunDataManager.Instance != null ? RunDataManager.Instance.playerMaxHP : 50;

        for (int i = 0; i < optionButtons.Count; i++)
        {
            if (i >= choices.Count)
            {
                optionButtons[i].gameObject.SetActive(false);
                continue;
            }

            EventChoice choice = choices[i];
            Button btn = optionButtons[i];
            TextMeshProUGUI txt = (i < optionTexts.Count) ? optionTexts[i] : btn.GetComponentInChildren<TextMeshProUGUI>();

            if (txt != null) txt.text = choice.buttonDescription;

            // 如果玩家扣除上限后最大生命值 <= 0，则置灰不可点
            bool canAffordHP = (currentMaxHP - choice.maxHPCost) > 0;
            btn.interactable = canAffordHP;

            int choiceIndex = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnOptionSelected(choiceIndex));

            // 按钮悬停轻微动画
            BindButtonHover(btn.gameObject);
        }
    }

    /// <summary>
    /// 处理选项点击逻辑
    /// </summary>
    private void OnOptionSelected(int index)
    {
        if (index < 0 || index >= choices.Count) return;
        EventChoice selectedChoice = choices[index];

        // 1. 结算全局资产 (扣生命上限、扣当前生命并补齐、加金币)
        if (RunDataManager.Instance != null)
        {
            if (selectedChoice.maxHPCost > 0)
            {
                RunDataManager.Instance.playerMaxHP = Mathf.Max(1, RunDataManager.Instance.playerMaxHP - selectedChoice.maxHPCost);
                RunDataManager.Instance.playerCurrentHP = Mathf.Min(RunDataManager.Instance.playerCurrentHP, RunDataManager.Instance.playerMaxHP);
            }

            if (selectedChoice.goldReward > 0)
            {
                RunDataManager.Instance.AddCurrency(selectedChoice.goldReward);
            }
        }

        // 2. 更新 TopBar 显示
        UpdateTopBarData();

        // 3. 将剧情文本切换为结果反馈
        if (storyText != null)
        {
            storyText.text = selectedChoice.resultStoryText;
        }

        // 4. 禁用所有选项按钮，并弹出“离开/继续”按钮
        foreach (var btn in optionButtons)
        {
            btn.gameObject.SetActive(false);
        }

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(true);
            leaveButton.transform.localScale = Vector3.one * 0.8f;
            leaveButton.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
        }

        Debug.Log($"[EventManager] 玩家选择了：{selectedChoice.buttonDescription}");
    }

    private void UpdateTopBarData()
    {
        if (RunDataManager.Instance == null) return;

        if (currencyText != null)
        {
            currencyText.text = RunDataManager.Instance.currency.ToString();
        }

        if (hpText != null)
        {
            hpText.text = $"{RunDataManager.Instance.playerCurrentHP} / {RunDataManager.Instance.playerMaxHP}";
        }
    }

    private void BindButtonHover(GameObject btnObj)
    {
        Vector3 baseScale = Vector3.one;
        btnObj.transform.localScale = baseScale;

        // 简单的按压/悬停轻微微调
        var trigger = btnObj.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = btnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        trigger.triggers.Clear();

        var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enter.callback.AddListener((d) =>
        {
            if (btnObj.GetComponent<Button>().interactable)
            {
                btnObj.transform.DOScale(baseScale * 1.03f, 0.15f).SetEase(Ease.OutCubic);
            }
        });
        trigger.triggers.Add(enter);

        var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exit.callback.AddListener((d) =>
        {
            btnObj.transform.DOScale(baseScale, 0.15f).SetEase(Ease.OutCubic);
        });
        trigger.triggers.Add(exit);
    }

    private void OnLeaveClicked()
    {
        SceneManager.LoadScene(mapSceneName);
    }
}