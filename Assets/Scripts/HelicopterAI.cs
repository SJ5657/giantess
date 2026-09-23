using UnityEngine;

public class HelicopterAI : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 70f;
    private float health;
    private bool isDestroyed;
    private EnemyHealthBar healthBar;

    public float moveSpeed = 8f;
    public float attackRange = 30f;
    public float hoverHeight = 7f;
    public float rotateSpeed = 3f;

    [Header("Firing")]
    public float fireRate = 10f; // shots per second
    public float bulletDamage = 1f;
    public Transform muzzle;

    [Header("Performance LOD")]
    [Tooltip("Beyond this distance from the giant, this helicopter's chase/fire logic updates " +
        "only a few times a second instead of every frame.")]
    public float lodFarDistance = 45f;
    [Tooltip("How often (seconds) a far-away helicopter's logic still updates.")]
    public float lodFarTickInterval = 0.25f;

    [Header("Knockback")]
    [Tooltip("How quickly an applied knockback velocity decays back to zero (higher = stops sooner.")]
    public float knockbackDrag = 3f;
    [Tooltip("While the current knockback speed is above this, this tick is a stagger: only the knockback displacement applies and the enemy's own movement/aim/fire logic is skipped, so a solid punch reads as a clean shove instead of being fought by the enemy's own AI movement.")]
    public float knockbackStunThreshold = 3f;

    private Transform giant;
    private GiantHealth giantHealth;
    private float fireTimer;
    private float lodAccumDt;
    private Vector3 knockbackVelocity;

    void Start()
    {
        health = maxHealth;

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null)
        {
            giant = giantObj.transform;
            giantHealth = giantObj.GetComponent<GiantHealth>();
        }
    }

    void Update()
    {
        if (giant == null || isDestroyed) return;

        bool isFar = Vector3.Distance(transform.position, giant.position) > lodFarDistance;

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
        TickKnockback(dt, 1f, 0f);
        if (knockbackVelocity.sqrMagnitude > knockbackStunThreshold * knockbackStunThreshold) return;

        Vector3 flatToGiant = giant.position - transform.position;
        flatToGiant.y = 0f;
        float flatDist = flatToGiant.magnitude;
        bool inRange = flatDist <= attackRange;

        Vector3 targetPos = giant.position + Vector3.up * hoverHeight;
        Vector3 moveDest = !inRange
            ? targetPos
            : new Vector3(transform.position.x, targetPos.y, transform.position.z);
        Vector3 move = Vector3.MoveTowards(transform.position, moveDest, moveSpeed * dt) - transform.position;
        move = BuildingCollision.ClampAgainstBuildingsSliding(transform.position, move, 1f, 0f);
        transform.position += move;

        if (flatToGiant.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(flatToGiant.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
        }

        if (inRange)
        {
            fireTimer -= dt;
            if (fireTimer <= 0f)
            {
                FireBullet();
                fireTimer = 1f / Mathf.Max(0.01f, fireRate);
            }
        }
        else
        {
            fireTimer = 0f;
        }
    }

    // Shoves this helicopter away, called by the giant's left-click punch. Adds onto any
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

    void FireBullet()
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector3 spread = new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.5f, 0.5f), Random.Range(-0.7f, 0.7f));
        Vector3 targetPos = giant.position + Vector3.up * 5f + spread;

        // Muzzle flash
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "MuzzleFlash";
        flash.transform.position = origin;
        flash.transform.localScale = Vector3.one * 0.15f;
        Destroy(flash.GetComponent<Collider>());
        Renderer frend = flash.GetComponent<Renderer>();
        Material fmat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fmat.color = Color.yellow;
        fmat.EnableKeyword("_EMISSION");
        fmat.SetColor("_EmissionColor", Color.yellow * 4f);
        frend.material = fmat;
        ImpactFlash flashAnim = flash.AddComponent<ImpactFlash>();
        flashAnim.duration = 0.08f;
        flashAnim.maxScale = 1.8f;

        // Tracer streak toward the target (aim point, which may miss)
        GameObject tracerObj = new GameObject("Tracer");
        LineRenderer lr = tracerObj.AddComponent<LineRenderer>();
        lr.material = VehicleSpawner.GetTracerMaterial();
        lr.startColor = new Color(1f, 0.9f, 0.4f, 0.9f);
        lr.endColor = new Color(1f, 0.9f, 0.4f, 0f);
        lr.startWidth = 0.07f;
        lr.endWidth = 0.02f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, targetPos);
        Tracer tracer = tracerObj.AddComponent<Tracer>();
        tracer.lifetime = 0.06f;

        // Only apply damage if the (possibly spread-out) aim point actually lands on the giant's body.
        if (giantHealth != null && giantHealth.IsPointInHitbox(targetPos))
        {
            giantHealth.TakeDamage(bulletDamage);
        }
    }

    // Damage from the giant's punch attack. Explodes once HP runs out.
    public void TakeDamage(float amount)
    {
        if (isDestroyed || amount <= 0f) return;

        health -= amount;

        if (healthBar == null)
        {
            healthBar = EnemyHealthBar.Attach(transform);
        }
        healthBar.SetFill(Mathf.Max(health, 0f), maxHealth);

        if (health <= 0f)
        {
            Explode();
        }
    }

    void Explode()
    {
        isDestroyed = true;

        if (healthBar != null)
        {
            Destroy(healthBar.gameObject);
        }

        Explosion.Spawn(transform.position, 1f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyHelicopterDestroyed();
        }
        if (GiantProgression.Instance != null)
        {
            GiantProgression.Instance.AddXP(10f);
        }

        Destroy(gameObject);
    }
}
