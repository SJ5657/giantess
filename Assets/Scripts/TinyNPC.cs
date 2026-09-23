using UnityEngine;

public class TinyNPC : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 30f;
    private float hp;
    private bool isDead;
    private EnemyHealthBar healthBar;

    [Header("Giant Interaction")]
    [Tooltip("When true, this NPC is completely unaffected by the giant: can't be grabbed, damaged, knocked back, or squashed, and never flees from the giant's presence either. Used for named background characters (e.g. BlackMan) that should just exist in the world without being part of giant-vs-city gameplay.")]
    public bool giantImmune = false;

    [Header("Wander")]
    public float moveSpeed = 1.2f;
    public float wanderRadius = 15f;
    public float changeDirectionInterval = 3f;

    [Header("Flee")]
    public float fleeSpeed = 3.5f;
    public float fleeDistance = 10f;
    [Tooltip("Once already fleeing, the NPC keeps running until it's actually this far from the giant (not just fleeDistance) before calming back down to normal wandering -- so a scared NPC gains real distance instead of stopping the instant it barely clears the trigger radius.")]
    public float fleeExitDistance = 20f;

    [Header("Animation")]
    public Animator animator;
    public float animSpeedDamping = 8f;

    [Header("Performance LOD")]
    [Tooltip("Beyond this distance from the giant, this NPC switches to throttled logic and " +
        "freezes its Animator to save CPU. Imperceptible at this range, but adds up once many " +
        "tiny people are wandering the city at once.")]
    public float lodFarDistance = 45f;
    [Tooltip("How often (seconds) a far-away NPC's wander/flee logic still updates, e.g. 0.25 " +
        "= about 4 times/sec instead of every frame.")]
    public float lodFarTickInterval = 0.25f;

    [Header("Knockback")]
    [Tooltip("How quickly an applied knockback velocity decays back to zero (higher = stops sooner.")]
    public float knockbackDrag = 4f;
    [Tooltip("While the current knockback speed is above this, this tick is a stagger: only the knockback displacement applies and the enemy's own movement/aim/fire logic is skipped, so a solid punch reads as a clean shove instead of being fought by the enemy's own AI movement.")]
    public float knockbackStunThreshold = 3f;

    private Vector3 startPos;
    private Vector3 targetDir;
    private float timer;
    private float currentAnimSpeed;
    private Transform giant;
    private bool isFleeing;
    private float lodAccumDt;
    private Vector3 knockbackVelocity;

    void Start()
    {
        hp = maxHP;

        startPos = transform.position;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null)
        {
            giant = giantObj.transform;
        }

        PickNewDirection();
    }

    void Update()
    {
        bool isFar = giant != null && Vector3.Distance(transform.position, giant.position) > lodFarDistance;

        // Freezing the Animator entirely (rather than just skipping SetFloat/SetBool calls)
        // is what actually saves the per-frame bone-evaluation cost — the pose just holds
        // still, which is unnoticeable at LOD distance.
        if (animator != null) animator.enabled = !isFar;

        if (!isFar)
        {
            TickBehavior(Time.deltaTime);
            lodAccumDt = 0f;
        }
        else
        {
            lodAccumDt += Time.deltaTime;
            if (lodAccumDt >= lodFarTickInterval)
            {
                TickBehavior(lodAccumDt);
                lodAccumDt = 0f;
            }
        }
    }

    // The original per-frame Update() body, now driven by an explicit dt instead of always
    // Time.deltaTime: when throttled (far away), dt is the accumulated time since the last
    // tick, so movement/timers still cover the right amount of time overall — the NPC just
    // advances in fewer, bigger steps, which reads as normal motion from a distance.
    void TickBehavior(float dt)
    {
        TickKnockback(dt, 0.4f, 1f);
        if (knockbackVelocity.sqrMagnitude > knockbackStunThreshold * knockbackStunThreshold) return;

        bool wasFleeing = isFleeing;
        isFleeing = false;

        if (giant != null)
        {
            Vector3 toSelf = transform.position - giant.position;
            toSelf.y = 0f;
            float dist = toSelf.magnitude;

            // Hysteresis: a calm NPC starts fleeing once the giant closes to within
            // fleeDistance, but once already fleeing, it keeps running until it's actually
            // fleeExitDistance away before settling back down -- otherwise it would stop the
            // instant it barely cleared fleeDistance, never gaining any real distance.
            float triggerThreshold = wasFleeing ? fleeExitDistance : fleeDistance;
            if (!giantImmune && dist < triggerThreshold)
            {
                isFleeing = true;
                targetDir = dist > 0.0001f ? toSelf.normalized : targetDir;
                timer = 0f;
            }
        }

        if (!isFleeing)
        {
            timer -= dt;
            if (timer <= 0f)
            {
                PickNewDirection();
            }
        }

        float speed = isFleeing ? fleeSpeed : moveSpeed;
        Vector3 move = targetDir * speed * dt;
        move = BuildingCollision.ClampAgainstBuildingsSliding(transform.position, move, 0.4f, 1f);
        transform.position += move;

        if (move.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), 5f * dt);
        }

        if (!isFleeing && Vector3.Distance(transform.position, startPos) > wanderRadius)
        {
            targetDir = (startPos - transform.position).normalized;
        }

        float targetAnimSpeed = move.sqrMagnitude > 0.0001f ? 1f : 0f;
        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetAnimSpeed, animSpeedDamping * dt);
        if (animator != null && animator.enabled)
        {
            animator.SetFloat("Speed", currentAnimSpeed);
            animator.SetBool("Flee", isFleeing);
        }
    }

    // Shoves this NPC away, called by the giant's left-click punch. Adds onto any existing
    // knockback rather than overwriting it, so a second hit during recovery still stacks.
    public void ApplyKnockback(Vector3 velocity)
    {
        if (giantImmune) return;
        knockbackVelocity += velocity;
    }

    void TickKnockback(float dt, float radius, float heightOffset)
    {
        if (knockbackVelocity.sqrMagnitude < 0.0001f) return;

        Vector3 kb = BuildingCollision.ClampAgainstBuildingsSliding(transform.position, knockbackVelocity * dt, radius, heightOffset);
        transform.position += kb;

        knockbackVelocity *= Mathf.Exp(-knockbackDrag * dt);
        if (knockbackVelocity.sqrMagnitude < 0.01f) knockbackVelocity = Vector3.zero;
    }

    void PickNewDirection()
    {
        timer = changeDirectionInterval;
        Vector2 rnd = Random.insideUnitCircle;
        targetDir = new Vector3(rnd.x, 0f, rnd.y).normalized;
    }

    // Damage from the giant's punch attack. Depletes HP and squashes once it runs out.
    public void TakeDamage(float amount)
    {
        if (giantImmune || isDead || amount <= 0f) return;

        hp -= amount;

        if (healthBar == null)
        {
            healthBar = EnemyHealthBar.Attach(transform);
        }
        healthBar.SetFill(Mathf.Max(hp, 0f), maxHP);

        if (hp <= 0f)
        {
            Squash();
        }
    }

    // Instant kill used by StompZone (walking over the NPC) and by TakeDamage once HP hits 0.
    public void Squash()
    {
        if (giantImmune || isDead) return;
        isDead = true;

        if (healthBar != null)
        {
            Destroy(healthBar.gameObject);
        }

        Blood.Spawn(transform.position + Vector3.up * 0.6f, 1f);

        if (GiantProgression.Instance != null)
        {
            GiantProgression.Instance.AddXP(1f);
        }
        Destroy(gameObject);
    }
}
