using UnityEngine;
using UnityEngine.UI;

// A small billboarded world-space HP bar that appears above an enemy once it has taken
// damage, and tracks its remaining HP from then on. Destroys itself automatically once
// its target is gone (killed/destroyed).
public class EnemyHealthBar : MonoBehaviour
{
    private Transform target;
    private Vector3 worldOffset;
    private Image fillImage;
    private Camera cam;

    public static EnemyHealthBar Attach(Transform target)
    {
        float heightOffset = ComputeHeightOffset(target);

        GameObject go = new GameObject("EnemyHealthBar");
        go.transform.position = target.position + Vector3.up * heightOffset;

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = go.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(140f, 18f);

        // Scale the whole bar down to a small, roughly size-appropriate world-unit width,
        // since the canvas RectTransform above is authored in pixel-like units.
        float worldWidth = Mathf.Clamp(heightOffset * 0.5f, 0.5f, 2.2f);
        go.transform.localScale = Vector3.one * (worldWidth / 140f);

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.75f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(go.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.85f, 0.22f, 0.22f, 1f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        EnemyHealthBar bar = go.AddComponent<EnemyHealthBar>();
        bar.target = target;
        bar.worldOffset = Vector3.up * heightOffset;
        bar.fillImage = fillImg;
        bar.cam = Camera.main;
        return bar;
    }

    static float ComputeHeightOffset(Transform target)
    {
        Renderer[] rends = target.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return 2f;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            b.Encapsulate(rends[i].bounds);
        }
        return Mathf.Max(0.3f, b.max.y - target.position.y) + 0.4f;
    }

    public void SetFill(float current, float max)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = target.position + worldOffset;

        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
