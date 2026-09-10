using UnityEngine;

public class TinyNPC : MonoBehaviour
{
    public float moveSpeed = 1.2f;
    public float wanderRadius = 15f;
    public float changeDirectionInterval = 3f;

    private Vector3 startPos;
    private Vector3 targetDir;
    private float timer;

    void Start()
    {
        startPos = transform.position;
        PickNewDirection();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            PickNewDirection();
        }

        Vector3 move = targetDir * moveSpeed * Time.deltaTime;
        transform.position += move;

        if (move.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), 5f * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, startPos) > wanderRadius)
        {
            targetDir = (startPos - transform.position).normalized;
        }
    }

    void PickNewDirection()
    {
        timer = changeDirectionInterval;
        Vector2 rnd = Random.insideUnitCircle;
        targetDir = new Vector3(rnd.x, 0f, rnd.y).normalized;
    }

    public void Squash()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(1);
        }
        Destroy(gameObject);
    }
}
