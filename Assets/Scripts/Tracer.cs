using UnityEngine;

// Short-lived machine gun tracer line; destroys itself after `lifetime` seconds.
public class Tracer : MonoBehaviour
{
    public float lifetime = 0.06f;
    private float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
