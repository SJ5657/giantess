using UnityEngine;

// A persistent, gently pulsing/spinning ring shown on the ground under whichever tiny
// person/soldier the giant would grab right now (the nearest one inside the left-click
// grab range). Purely visual -- tells the player who's about to get snatched before they
// click. Follows its target every frame and destroys itself once told to release it or
// once its target is gone.
public class GrabTargetIndicator : MonoBehaviour
{
    private Transform target;
    private Renderer rend;
    private Color baseColor;
    private float pulseTimer;

    public static GrabTargetIndicator Create(Transform target, float radius)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "GrabTargetIndicator";
        Object.Destroy(go.GetComponent<Collider>());

        float diameter = Mathf.Max(0.3f, radius) * 2f;
        go.transform.localScale = new Vector3(diameter, 0.015f, diameter);

        Renderer rend = go.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color c = new Color(1f, 0.9f, 0.15f, 0.55f);
        mat.color = c;
        rend.material = mat;

        GrabTargetIndicator indicator = go.AddComponent<GrabTargetIndicator>();
        indicator.target = target;
        indicator.rend = rend;
        indicator.baseColor = c;
        return indicator;
    }

    // Re-point this same indicator at a different (now-nearest) target instead of
    // destroying/recreating it, so it doesn't flicker as the nearest candidate changes.
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public bool IsTarget(Transform t)
    {
        return target == t;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = target.position + Vector3.up * 0.05f;
        transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);

        pulseTimer += Time.deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTimer * 5f);

        if (rend != null)
        {
            Color c = baseColor;
            c.a = Mathf.Lerp(0.35f, 0.8f, pulse);
            rend.material.color = c;
        }
    }
}
