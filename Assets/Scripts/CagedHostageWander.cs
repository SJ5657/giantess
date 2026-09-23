using UnityEngine;

// Makes a caged hostage wander gently within the interior of the birdcage instead of
// standing frozen at one fixed spot, once GiantController.CageHeldTarget parents them
// inside cageInsidePoint. Purely cosmetic: it only moves this transform directly (no
// physics), and both the hostage's own AI script (TinyNPC/TinySoldierAI) and Collider stay
// disabled exactly as they were while held in-hand (see GiantController.AttachToHand), so a
// wandering hostage still can't escape, block movement, or be re-grabbed. Picks a random
// point within wanderRadius (world units) of the spot it was caged at, walks there at a
// slow pace, waits briefly, then picks a new point -- forever. Movement is done via
// transform.position (world space) rather than localPosition so wanderRadius means the same
// real-world distance regardless of how small the cage's own scale is.
public class CagedHostageWander : MonoBehaviour
{
    public float wanderRadius = 1f;
    public float moveSpeed = 0.5f;
    public float waitTimeMin = 0.4f;
    public float waitTimeMax = 1.6f;
    public float turnSpeed = 6f;

    private Vector3 worldCenter;
    private Vector3 worldTarget;
    private float waitUntil;
    private Animator animator;

    // Called by GiantController right after AddComponent, before Start() runs, so the
    // radius the cage's own interior size implies is already in place on the very first
    // frame this component ticks.
    public void Initialize(float radius)
    {
        wanderRadius = Mathf.Max(radius, 0.1f);
    }

    void Start()
    {
        worldCenter = transform.position;
        animator = GetComponentInChildren<Animator>();
        PickNewTarget();
    }

    void Update()
    {
        bool moving = Time.time >= waitUntil;
        if (moving)
        {
            Vector3 toTarget = worldTarget - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist < 0.03f)
            {
                waitUntil = Time.time + Random.Range(waitTimeMin, waitTimeMax);
                moving = false;
                PickNewTarget();
            }
            else
            {
                Vector3 dir = toTarget.normalized;
                transform.position += dir * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), turnSpeed * Time.deltaTime);
            }
        }

        // Reuse the same Animator params TinyNPC already drives (Speed/Flee), so a caged
        // hostage's own walk cycle plays while it wanders instead of it sliding around in
        // its idle pose.
        if (animator != null)
        {
            animator.SetFloat("Speed", moving ? 1f : 0f);
            animator.SetBool("Flee", false);
        }
    }

    void PickNewTarget()
    {
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        worldTarget = worldCenter + new Vector3(offset.x, 0f, offset.y);
    }
}
