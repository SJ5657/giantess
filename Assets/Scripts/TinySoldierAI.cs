using UnityEngine;

// A tiny "soldier" NPC: approaches the giant, then stops, aims, and fires a pistol at it.
// Shots use the same hit-detection as the tank/helicopter (GiantHealth.IsPointInHitbox),
// so shots that miss don't apply damage.
public class TinySoldierAI : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 30f;
    private float hp;
    private EnemyHealthBar healthBar;

    public float moveSpeed = 3f;
    public float attackRange = 14f;
    public float rotateSpeed = 4f;

    [Header("Firing")]
    public float fireInterval = 1.4f;
    public float shotDamage = 4f;
    [Tooltip("Max random aim offset (world units) applied to each shot, so shots can miss.")]
    public float shotSpread = 0.9f;

    public Animator animator;
    public Transform muzzle;

    [Header("Performance LOD")]
    [Tooltip("Beyond this distance from the giant, this soldier switches to throttled logic " +
        "and freezes its Animator to save CPU. Well above attackRange, so anyone actually " +
        "fighting stays at full fidelity — this only kicks in while approaching from far away.")]
    public float lodFarDistance = 45f;
    [Tooltip("How often (seconds) a far-away soldier's movement/aim logic still updates.")]
    public float lodFarTickInterval = 0.25f;

    [Header("Knockback")]
    [Tooltip("How quickly an applied knockback velocity decays back to zero (higher = stops sooner.")]
    public float knockbackDrag = 4f;
    [Tooltip("While the current knockback speed is above this, this tick is a stagger: only the knockback displacement applies and the enemy's own movement/aim/fire logic is skipped, so a solid punch reads as a clean shove instead of being fought by the enemy's own AI movement.")]
    public float knockbackStunThreshold = 3f;

    private Transform giant;
    private GiantHealth giantHealth;
    private float fireTimer;
    private bool isAiming;
    private bool isDead;
    private float lodAccumDt;
    private Vector3 knockbackVelocity;

    void Start()
    {
        hp = maxHP;

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null)
        {
            giant = giantObj.transform;
            giantHealth = giantObj.GetComponent<GiantHealth>();
        }
        fireTimer = fireInterval;
    }

    void Update()
    {
        if (giant == null) return;

        bool isFar = Vector3.Distance(transform.position, giant.position) > lodFarDistance;

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

    // The original per-frame Update() body, now driven by an explicit dt so throttled
    // (far-away) ticks still cover the right amount of elapsed time in fewer, bigger steps.
    void TickBehavior(float dt)
    {
        TickKnockback(dt, 0.4f, 1f);
        if (knockbackVelocity.sqrMagnitude > knockbackStunThreshold * knockbackStunThreshold) return;

        Vector3 toGiant = giant.position - transform.position;
        toGiant.y = 0f;
        float dist = toGiant.magnitude;
        bool inRange = dist <= attackRange;

        if (toGiant.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toGiant.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
        }

        float currentSpeed = 0f;
        if (!inRange)
        {
            Vector3 move = toGiant.normalized * moveSpeed * dt;
            move = BuildingCollision.ClampAgainstBuildingsSliding(transform.position, move, 0.4f, 1f);
            transform.position += move;
            currentSpeed = moveSpeed;
            isAiming = false;
            fireTimer = fireInterval;
        }
        else
        {
            isAiming = true;
            fireTimer -= dt;
            if (fireTimer <= 0f)
            {
                Fire();
                fireTimer = fireInterval;
            }
        }

        if (animator != null && animator.enabled)
        {
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("Aiming", isAiming);
        }
    }

    // Shoves this soldier away, called by the giant's left-click punch. Adds onto any
    // existing knockback rather than overwriting it, so a second hit during recovery stacks.
    public void ApplyKnockback(Vector3 velocity)
    {
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

    void Fire()
    {
        if (animator != null) animator.SetTrigger("Shoot");

        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;
        Vector3 spread = new Vector3(Random.Range(-shotSpread, shotSpread), Random.Range(-shotSpread * 0.5f, shotSpread * 0.5f), Random.Range(-shotSpread, shotSpread));
        Vector3 targetPos = giant.position + Vector3.up * 5f + spread;

        // Muzzle flash
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "PistolFlash";
        flash.transform.position = origin;
        flash.transform.localScale = Vector3.one * 0.08f;
        Destroy(flash.GetComponent<Collider>());
        Renderer frend = flash.GetComponent<Renderer>();
        Material fmat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fmat.color = Color.yellow;
        fmat.EnableKeyword("_EMISSION");
        fmat.SetColor("_EmissionColor", Color.yellow * 4f);
        frend.material = fmat;
        ImpactFlash flashAnim = flash.AddComponent<ImpactFlash>();
        flashAnim.duration = 0.06f;
        flashAnim.maxScale = 2f;

        // Tracer streak toward the aim point (which may miss)
        GameObject tracerObj = new GameObject("PistolTracer");
        LineRenderer lr = tracerObj.AddComponent<LineRenderer>();
        lr.material = VehicleSpawner.GetTracerMaterial();
        lr.startColor = new Color(1f, 0.95f, 0.6f, 0.9f);
        lr.endColor = new Color(1f, 0.95f, 0.6f, 0f);
        lr.startWidth = 0.035f;
        lr.endWidth = 0.01f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, targetPos);
        Tracer tracer = tracerObj.AddComponent<Tracer>();
        tracer.lifetime = 0.05f;

        if (giantHealth != null && giantHealth.IsPointInHitbox(targetPos))
        {
            giantHealth.TakeDamage(shotDamage);
        }
    }

    // Damage from the giant's punch attack. Depletes HP and squashes once it runs out.
    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

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

    public void Squash()
    {
        if (isDead) return;
        isDead = true;

        if (healthBar != null)
        {
            Destroy(healthBar.gameObject);
        }

        Blood.Spawn(transform.position + Vector3.up * 0.6f, 1f);

        if (GiantProgression.Instance != null) GiantProgression.Instance.AddXP(1f);
        Destroy(gameObject);
    }
}
