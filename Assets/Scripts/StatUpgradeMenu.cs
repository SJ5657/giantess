using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Z-toggled popup that spends skill points (earned from GiantProgression level-ups) on permanent
// giant stat upgrades: max HP, move speed, attack damage, damage reduction ("defense"), and
// max stamina. Mirrors EscMenu's open/close/pause pattern (Time.timeScale = 0 while open,
// cursor freed), but builds its own panel procedurally at runtime instead of needing a
// hand-authored panel wired up in the Inspector, so it's fully self-contained.
public class StatUpgradeMenu : MonoBehaviour
{
    public static StatUpgradeMenu Instance { get; private set; }

    [Header("References (auto-found if left empty)")]
    public GiantController giant;
    public GiantHealth giantHealth;
    public GiantProgression progression;
    public Canvas targetCanvas;

    [Header("Upgrade Cost")]
    [Tooltip("Coin cost of the FIRST upgrade of any stat.")]
    public int baseCost = 1;
    [Tooltip("Percent more expensive each further upgrade of the SAME stat is, compounding (matches the XP curve's compounding growth).")]
    public float costGrowthPercent = 30f;

    // One upgradeable stat: how much each purchase adds, how many times it's been bought this
    // run, and the live UI pieces that need refreshing after a purchase.
    private class StatDef
    {
        public string displayName;
        public float perLevelIncrease;
        public int level;
        public System.Func<float> getValue;
        public System.Action<float> apply;
        public TMP_Text infoText;
        public TMP_Text buttonLabel;
        public Button button;
    }

    private StatDef[] stats;
    private GameObject panel;
    private TMP_Text skillPointText;
    private bool isOpen;

    // Used by PauseMenu so Escape can close this popup instead of stacking the pause
    // menu on top of it.
    public bool IsOpen => isOpen;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (giant == null) giant = FindObjectOfType<GiantController>();
        if (giantHealth == null) giantHealth = giant != null ? giant.GetComponent<GiantHealth>() : FindObjectOfType<GiantHealth>();
        if (progression == null) progression = GiantProgression.Instance != null ? GiantProgression.Instance : FindObjectOfType<GiantProgression>();
        if (targetCanvas == null) targetCanvas = FindObjectOfType<Canvas>();

        stats = new StatDef[]
        {
            new StatDef
            {
                displayName = "HP 용량",
                perLevelIncrease = 50f,
                getValue = () => giantHealth != null ? giantHealth.maxHP : 0f,
                apply = amount =>
                {
                    if (giantHealth == null) return;
                    giantHealth.maxHP += amount;
                    giantHealth.currentHP += amount;
                    giantHealth.UpdateUI();
                }
            },
            new StatDef
            {
                displayName = "스피드",
                perLevelIncrease = 0.5f,
                getValue = () => giant != null ? giant.moveSpeed : 0f,
                apply = amount =>
                {
                    if (giant == null) return;
                    giant.moveSpeed += amount;
                }
            },
            new StatDef
            {
                displayName = "공격력",
                perLevelIncrease = 5f,
                getValue = () => giant != null ? giant.attackDamage : 0f,
                apply = amount =>
                {
                    if (giant == null) return;
                    giant.attackDamage += amount;
                }
            },
            new StatDef
            {
                displayName = "방어력",
                perLevelIncrease = 5f,
                getValue = () => giantHealth != null ? giantHealth.damageReductionPercent : 0f,
                apply = amount =>
                {
                    if (giantHealth == null) return;
                    giantHealth.damageReductionPercent = Mathf.Min(
                        giantHealth.damageReductionPercent + amount,
                        giantHealth.maxDamageReductionPercent);
                }
            },
            new StatDef
            {
                displayName = "스테미나 용량",
                perLevelIncrease = 10f,
                getValue = () => giant != null ? giant.maxStamina : 0f,
                apply = amount =>
                {
                    if (giant == null) return;
                    giant.maxStamina += amount;
                    giant.currentStamina += amount;
                }
            },
        };

        BuildUI();
        panel.SetActive(false);
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Z)) return;
        if (!CanTogglePopup()) return;

        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    // Same gating rule as EscMenu/PauseMenu: only usable once the game has actually started
    // and isn't already over, and can't be opened on top of some other pause-driven popup.
    bool CanTogglePopup()
    {
        if (!isOpen)
        {
            if (GameFlowManager.Instance != null && !GameFlowManager.Instance.HasGameStarted) return false;
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return false;
            if (Time.timeScale == 0f) return false;
        }
        return true;
    }

    public void Open()
    {
        isOpen = true;
        panel.SetActive(true);
        RefreshAll();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        isOpen = false;
        panel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    float CostForLevel(int level)
    {
        return baseCost * Mathf.Pow(1f + costGrowthPercent / 100f, level);
    }

    void TryUpgrade(StatDef stat)
    {
        if (progression == null) return;

        int cost = Mathf.RoundToInt(CostForLevel(stat.level));
        if (progression.skillPoints < cost) return;

        progression.skillPoints -= cost;
        stat.level++;
        stat.apply(stat.perLevelIncrease);

        progression.UpdateUI();
        RefreshAll();
    }

    // Used by SaveSystem to persist/restore how many times each stat has been purchased,
    // so future upgrade costs stay consistent across a save/load (the actual stat VALUES are
    // saved/restored separately, directly on GiantHealth/GiantController).
    public int[] GetStatLevels()
    {
        if (stats == null) return new int[0];
        int[] result = new int[stats.Length];
        for (int i = 0; i < stats.Length; i++) result[i] = stats[i].level;
        return result;
    }

    public void SetStatLevels(int[] levels)
    {
        if (stats == null || levels == null) return;
        for (int i = 0; i < stats.Length && i < levels.Length; i++)
        {
            stats[i].level = levels[i];
        }
        RefreshAll();
    }

    void RefreshAll()
    {
        if (progression != null && skillPointText != null)
        {
            skillPointText.text = "보유 스킬 포인트: " + progression.skillPoints;
        }

        if (stats == null) return;

        foreach (var stat in stats)
        {
            int cost = Mathf.RoundToInt(CostForLevel(stat.level));
            float currentValue = stat.getValue != null ? stat.getValue() : 0f;

            if (stat.infoText != null)
            {
                stat.infoText.text = stat.displayName + ": " + currentValue.ToString("0.##")
                    + "  (Lv." + stat.level + ")";
            }

            if (stat.buttonLabel != null)
            {
                stat.buttonLabel.text = "강화 (" + cost + ")";
            }

            if (stat.button != null)
            {
                stat.button.interactable = progression != null && progression.skillPoints >= cost;
            }
        }
    }

    // ---------------------------------------------------------------------------------------
    // Procedural UI construction. Colors/sizes match the existing EscMenu popup's style
    // (dark semi-transparent backdrop + a solid dark box + orange-red buttons) so this reads
    // as part of the same UI family rather than a bolted-on new look.
    // ---------------------------------------------------------------------------------------

    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color BoxColor = new Color(0.13f, 0.13f, 0.16f, 0.97f);
    static readonly Color ButtonColor = new Color(0.85f, 0.35f, 0.25f, 1f);
    static readonly Color ButtonDisabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    static readonly Color CloseButtonColor = new Color(0.3f, 0.3f, 0.34f, 1f);

    void BuildUI()
    {
        Transform canvasTransform = targetCanvas != null ? targetCanvas.transform : null;

        RectTransform backdrop = CreateUIObject("StatUpgradePanel", canvasTransform);
        panel = backdrop.gameObject;
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.sizeDelta = Vector2.zero;
        backdrop.anchoredPosition = Vector2.zero;
        Image backdropImg = backdrop.gameObject.AddComponent<Image>();
        backdropImg.color = BackdropColor;

        RectTransform box = CreateUIObject("Box", backdrop);
        box.anchorMin = new Vector2(0.5f, 0.5f);
        box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = Vector2.zero;
        box.sizeDelta = new Vector2(560f, 500f);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = BoxColor;

        // Title.
        RectTransform title = CreateTopAnchored(box, "Title", -20f, 50f);
        TMP_Text titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "스테이터스 강화";
        titleText.fontSize = 30f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;

        // Coin balance.
        RectTransform coinRow = CreateTopAnchored(box, "SkillPointRow", -75f, 34f);
        skillPointText = coinRow.gameObject.AddComponent<TextMeshProUGUI>();
        skillPointText.fontSize = 22f;
        skillPointText.color = new Color(1f, 0.85f, 0.3f, 1f);
        skillPointText.alignment = TextAlignmentOptions.Center;

        // One row per stat.
        float rowStartY = -118f;
        float rowSpacing = 54f;
        for (int i = 0; i < stats.Length; i++)
        {
            StatDef stat = stats[i];
            float y = rowStartY - rowSpacing * i;

            RectTransform row = CreateTopAnchored(box, "Row_" + stat.displayName, y, 46f);

            RectTransform label = CreateUIObject("Info", row);
            label.anchorMin = new Vector2(0f, 0.5f);
            label.anchorMax = new Vector2(0f, 0.5f);
            label.pivot = new Vector2(0f, 0.5f);
            label.anchoredPosition = new Vector2(24f, 0f);
            label.sizeDelta = new Vector2(310f, 44f);
            TMP_Text infoText = label.gameObject.AddComponent<TextMeshProUGUI>();
            infoText.fontSize = 20f;
            infoText.color = Color.white;
            infoText.alignment = TextAlignmentOptions.MidlineLeft;
            stat.infoText = infoText;

            RectTransform buttonRect = CreateUIObject("UpgradeButton", row);
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-24f, 0f);
            buttonRect.sizeDelta = new Vector2(180f, 44f);
            Image buttonImg = buttonRect.gameObject.AddComponent<Image>();
            buttonImg.color = ButtonColor;
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImg;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.disabledColor = ButtonDisabledColor;
            button.colors = colors;

            RectTransform buttonLabelRect = CreateUIObject("Label", buttonRect);
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.sizeDelta = Vector2.zero;
            buttonLabelRect.anchoredPosition = Vector2.zero;
            TMP_Text buttonLabelText = buttonLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
            buttonLabelText.fontSize = 18f;
            buttonLabelText.color = Color.white;
            buttonLabelText.alignment = TextAlignmentOptions.Center;
            stat.buttonLabel = buttonLabelText;
            stat.button = button;

            StatDef capturedStat = stat; // local copy for the closure, one per loop iteration
            button.onClick.AddListener(() => TryUpgrade(capturedStat));
        }

        // Close button, centered at the bottom of the box.
        RectTransform closeRect = CreateUIObject("CloseButton", box);
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 24f);
        closeRect.sizeDelta = new Vector2(220f, 50f);
        Image closeImg = closeRect.gameObject.AddComponent<Image>();
        closeImg.color = CloseButtonColor;
        Button closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImg;
        closeButton.onClick.AddListener(Close);

        RectTransform closeLabelRect = CreateUIObject("Label", closeRect);
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.sizeDelta = Vector2.zero;
        closeLabelRect.anchoredPosition = Vector2.zero;
        TMP_Text closeLabelText = closeLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        closeLabelText.text = "닫기 (Z)";
        closeLabelText.fontSize = 20f;
        closeLabelText.color = Color.white;
        closeLabelText.alignment = TextAlignmentOptions.Center;
    }

    RectTransform CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    // A row/element anchored to the top edge of its parent and stretched horizontally,
    // matching the pattern EscMenu/PauseMenu already use for their own titles.
    RectTransform CreateTopAnchored(Transform parent, string name, float y, float height)
    {
        RectTransform rt = CreateUIObject(name, parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, height);
        return rt;
    }
}
