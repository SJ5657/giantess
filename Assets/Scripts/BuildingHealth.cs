using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BuildingHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    private float health;
    private bool falling = false;
    private Rigidbody rb;

    void Start()
    {
        health = maxHealth;
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public void TakeDamage(float amount, Vector3 pushDir)
    {
        if (falling) return;

        health -= amount;
        if (health <= 0f)
        {
            Topple(pushDir);
        }
    }

    void Topple(Vector3 pushDir)
    {
        falling = true;
        rb.isKinematic = false;
        rb.AddForce(pushDir.normalized * 6f, ForceMode.VelocityChange);
        rb.AddTorque(new Vector3(pushDir.z, 0f, -pushDir.x) * 4f, ForceMode.VelocityChange);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(5);
        }

        Destroy(gameObject, 6f);
    }
}
