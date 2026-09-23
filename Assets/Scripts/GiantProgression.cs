using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GiantProgression : MonoBehaviour
{
    public static GiantProgression Instance { get; private set; }

    [Header("Progression")]
    public int level = 1;
    public float currentXP = 0f;
    [Tooltip("XP required to go from level 1 to level 2. Each further level multiplies this by (1 + xpGrowthPercent/100), so it compounds rather than growing by a flat amount.")]
    public float baseXPToLevel = 10f;
    [Tooltip("Percent more XP required for each level up, compounding. 20 means level 2->3 costs 20% more XP than level 1->2, level 3->4 costs 20% more than that, and so on.")]
    public float xpGrowthPercent = 20f;
    public int coinsPerLevelUp = 1;
    public int coins = 0;
    [Tooltip("Skill points granted each time the giant levels up -- spent in the Stat Upgrade menu (Z) instead of coins.")]
    public int skillPointsPerLevelUp = 1;
    public int skillPoints = 0;

    [Header("UI")]
    public TMP_Text coinText;
    public TMP_Text levelText;
    public TMP_Text skillPointText;
    public Image xpFillImage;

    private RectTransform xpFillRect;
    private float xpFillMaxWidth;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (xpFillImage != null)
        {
            xpFillRect = xpFillImage.rectTransform;
            xpFillMaxWidth = xpFillRect.sizeDelta.x;
        }

        UpdateUI();
    }

    // How much XP is needed to go from the given level to the next one. Compounds by
    // xpGrowthPercent each level (e.g. 20% growth: level 1->2 costs baseXPToLevel, level 2->3
    // costs baseXPToLevel*1.2, level 3->4 costs baseXPToLevel*1.2^2, ...), rather than the old
    // flat-per-level increase, so the climb keeps accelerating instead of leveling off.
    public float RequiredXPForLevel(int forLevel)
    {
        return baseXPToLevel * Mathf.Pow(1f + xpGrowthPercent / 100f, forLevel - 1);
    }

    // Grants XP, handling multiple level-ups from a single large gain (mirrors
    // GameManager.CheckScoreMilestones' while-loop pattern for the same reason:
    // a big XP reward could cross more than one level threshold at once).
    public void AddXP(float amount)
    {
        if (amount <= 0f) return;

        currentXP += amount;

        while (currentXP >= RequiredXPForLevel(level))
        {
            currentXP -= RequiredXPForLevel(level);
            level++;
            skillPoints += skillPointsPerLevelUp;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelUp(level);
            }
        }

        UpdateUI();
    }

    // Public so external systems (the stat upgrade menu) can refresh the coin display
    // right after directly changing coins, without needing to go through AddXP.
    public void UpdateUI()
    {
        if (coinText != null) coinText.text = coins.ToString();
        if (skillPointText != null) skillPointText.text = skillPoints.ToString();
        if (levelText != null) levelText.text = "Lv." + level;

        if (xpFillRect != null)
        {
            float required = RequiredXPForLevel(level);
            float ratio = required > 0f ? Mathf.Clamp01(currentXP / required) : 0f;
            Vector2 size = xpFillRect.sizeDelta;
            size.x = xpFillMaxWidth * ratio;
            xpFillRect.sizeDelta = size;
        }
    }
}
