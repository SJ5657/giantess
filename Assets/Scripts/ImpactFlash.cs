using UnityEngine;

// Quick expand-and-destroy visual used for shell impacts and muzzle flashes.
public class ImpactFlash : MonoBehaviour
{
    public float duration = 0.25f;
    public float maxScale = 2.5f;

    private float timer;
    private Vector3 startScale;

    void Start()
    {
        startScale = transform.localScale;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        transform.localScale = startScale * Mathf.Lerp(1f, maxScale, t);

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
