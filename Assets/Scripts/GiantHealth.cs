using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GiantHealth : MonoBehaviour
{
    public float maxHP = 500f;
    public float currentHP;
    [Tooltip("Percent of incoming damage ignored. Bought via the stat upgrade menu (Z). Clamped so damage never gets reduced all the way to zero.")]
    public float damageReductionPercent = 0f;
    [Tooltip("Upper bound for damageReductionPercent, so upgrades can never make the giant fully invincible.")]
    public float maxDamageReductionPercent = 80f;
    public TextMeshProUGUI hpText;
    [Tooltip("The gauge's fill Image, shown as the HP bar. Its RectTransform width is driven " +
        "directly (left edge fixed, right edge moves) rather than relying on Image.fillAmount, " +
        "so the bar unambiguously shrinks/grows and isn't just the number changing.")]
    public Image hpFillImage;

    private RectTransform hpFillRect;
    private float hpFillMaxWidth;

    [Header("Hit Detection")]
    [Tooltip("Extra margin (world units) added to the body hitbox radius when checking if a shot actually hit.")]
    public float hitboxMargin = 0.4f;

    private bool isDead;

    void Awake()
    {
        currentHP = maxHP;
    }

    void Start()
    {
        if (hpFillImage != null)
        {
            hpFillRect = hpFillImage.rectTransform;
            // Whatever width the bar was authored at (full HP) is the max — read it once rather
            // than hardcoding it, so resizing the bar in the UI still works correctly.
            hpFillMaxWidth = hpFillRect.sizeDelta.x;
        }

        UpdateUI();
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

        float reduction = Mathf.Clamp(damageReductionPercent, 0f, maxDamageReductionPercent);
        amount *= (1f - reduction / 100f);

        currentHP -= amount;
        if (currentHP <= 0f)
        {
            currentHP = 0f;
            isDead = true;
            UpdateUI();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerGameOver();
            }
            return;
        }

        UpdateUI();
    }

    // Returns true if the given world-space point actually lies within the giant's body hitbox.
    // Used so that shells/bullets that visually miss (spread pushed the impact point away from
    // the body) do not apply damage, instead of always guaranteeing a hit.
    public bool IsPointInHitbox(Vector3 point)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            // Fallback: rough capsule around the transform if no CharacterController is present.
            Vector3 fallbackCenter = transform.position + Vector3.up * 5f;
            return Vector3.Distance(point, fallbackCenter) <= 3f + hitboxMargin;
        }

        Vector3 worldCenter = transform.TransformPoint(cc.center);
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float radius = cc.radius * scale + hitboxMargin;
        float halfCyl = Mathf.Max(0f, cc.height * 0.5f * transform.lossyScale.y - cc.radius * scale);

        Vector3 axis = transform.up;
        Vector3 segA = worldCenter - axis * halfCyl;
        Vector3 segB = worldCenter + axis * halfCyl;

        Vector3 ab = segB - segA;
        float t = ab.sqrMagnitude > 0.0001f ? Vector3.Dot(point - segA, ab) / ab.sqrMagnitude : 0f;
        t = Mathf.Clamp01(t);
        Vector3 closest = segA + ab * t;

        return Vector3.Distance(point, closest) <= radius;
    }

    // Restores HP, e.g. from a HealthPickup. Clamped to maxHP and ignored once dead.
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;

        currentHP = Mathf.Min(currentHP + amount, maxHP);
        UpdateUI();
    }

    // Public so external systems (the stat upgrade menu) can refresh the bar right after
    // directly changing maxHP/currentHP, without needing to go through TakeDamage.
    public void UpdateUI()
    {
        if (hpText != null)
        {
            hpText.text = "HP: " + Mathf.CeilToInt(currentHP) + " / " + Mathf.CeilToInt(maxHP);
        }

        if (hpFillRect != null)
        {
            float ratio = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
            Vector2 sd = hpFillRect.sizeDelta;
            sd.x = hpFillMaxWidth * ratio;
            hpFillRect.sizeDelta = sd;
        }
    }
}
