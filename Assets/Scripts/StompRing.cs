using UnityEngine;

// A flat, fast-fading disc used to show where/how large a stomp's impact area was.
public class StompRing : MonoBehaviour
{
    public float duration = 0.4f;

    private float timer;
    private float startRadius;
    private float endRadius;
    private Renderer rend;
    private Color baseColor;

    public static void Spawn(Vector3 groundPosition, float radius)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "StompRing";
        Object.Destroy(go.GetComponent<Collider>());

        go.transform.position = groundPosition + Vector3.up * 0.03f;

        float startRadius = Mathf.Max(0.15f, radius * 0.12f);
        go.transform.localScale = new Vector3(startRadius * 2f, 0.02f, startRadius * 2f);

        Renderer rend = go.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color c = new Color(1f, 0.55f, 0.2f, 0.5f);
        mat.color = c;
        rend.material = mat;

        StompRing ring = go.AddComponent<StompRing>();
        ring.rend = rend;
        ring.baseColor = c;
        ring.startRadius = startRadius;
        ring.endRadius = Mathf.Max(startRadius, radius);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        float r = Mathf.Lerp(startRadius, endRadius, t);
        transform.localScale = new Vector3(r * 2f, transform.localScale.y, r * 2f);

        if (rend != null)
        {
            Color c = baseColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, t);
            rend.material.color = c;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
