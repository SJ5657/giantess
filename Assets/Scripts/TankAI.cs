using UnityEngine;

public class TankAI : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 180f;
    private float health;
    private bool isDestroyed;
    private EnemyHealthBar healthBar;

    public float moveSpeed = 4f;
    public float attackRange = 35f;
    public float rotateSpeed = 3f;

    [Header("Firing")]
    public float fireInterval = 1.5f;
    public float shellDamage = 22f;
    public float shellSpeed = 30f;
    [Tooltip("Max random aim offset (world units) applied to each shot, so shells can miss.")]
    public float shellSpread = 1.8f;
    public Transform muzzle;
    public Transform barrel;

    [Header("Performance LOD")]
    [Tooltip("Beyond this distance from the giant, this tank's chase/fire logic updates only " +
        "a few times a second instead of every frame.")]
    public float lodFarDistance = 45f;
    [Tooltip("How often (seconds) a far-away tank's logic still updates.")]
    public float lodFarTickInterval = 0.25f;

    [Header("Knockback")]
    [Tooltip("How quickly an applied knockback velocity decays back to zero (higher = stops sooner.")]
    public float knockbackDrag = 3f;
    [Tooltip("While the current knockback speed is above this, this tick is a stagger: only the knockback displacement applies and the enemy's own movement/aim/fire logic is skipped, so a solid punch reads as a clean shove instead of being fought by the enemy's own AI movement.")]
    public float knockbackStunThreshold = 3f;

    private Transform giant;
    private GiantHealth giantHealth;
    private float fireTimer;
    private Vector3 barrelRestLocalPos;
    private float recoilTimer = -1f;
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

        if (barrel != null)
        {
            barrelRestLocalPos = barrel.localPosition;
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
        TickKnockback(dt, 1.3f, 0.3f);
        if (knockbackVelocity.sqrMagnitude > knockbackStunThreshold * knockbackStunThreshold) return;

        Vector3 toGiant = giant.position - transform.position;
        toGiant.y = 0f;
        float dist = toGiant.magnitude;

        if (toGiant.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toGiant.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
        }

        bool inRange = dist <= attackRange;

        if (!inRange)
        {
            Vector3 move = toGiant.normalized * moveSpeed * dt;
            move = BuildingCollision.ClampAgainstBuildingsSliding(transform.position, move, 1.3f, 0.3f);
            transform.position += move;
            fireTimer = 0f;
        }
        else
        {
            fireTimer -= dt;
            if (fireTimer <= 0f)
            {
                Fire();
                fireTimer = fireInterval;
            }
        }

        UpdateRecoil(dt);
    }

    // Shoves this tank away, called by the giant's left-click punch. Adds onto any existing
    // knockback rather than overwriting it, so a second hit during recovery still stacks.
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
        recoilTimer = 0f;

        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.7f;
        Vector3 spread = new Vector3(Random.Range(-shellSpread, shellSpread), Random.Range(-shellSpread * 0.4f, shellSpread * 0.4f), Random.Range(-shellSpread, shellSpread));
        Vector3 targetPos = giant.position + Vector3.up * 5f + spread;

        GameObject shellObj = new GameObject("Shell");
        shellObj.transform.position = origin;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.SetParent(shellObj.transform, false);
        visual.transform.localScale = Vector3.one * 0.22f;
        Destroy(visual.GetComponent<Collider>());
        Renderer rend = visual.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.05f, 0.05f, 0.05f);
        rend.material = mat;

        TrailRenderer trail = shellObj.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.startWidth = 0.12f;
        trail.endWidth = 0.01f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1f, 0.8f, 0.4f, 0.8f);
        trail.endColor = new Color(1f, 0.8f, 0.4f, 0f);

        TankShell shell = shellObj.AddComponent<TankShell>();
        shell.Init(origin, targetPos, shellSpeed, shellDamage, giantHealth);
    }

    void UpdateRecoil(float dt)
    {
        if (barrel == null || recoilTimer < 0f) return;

        recoilTimer += dt;
        float t = Mathf.Clamp01(recoilTimer / 0.25f);
        float kick = Mathf.Sin(t * Mathf.PI) * 0.3f;
        barrel.localPosition = barrelRestLocalPos - new Vector3(0f, 0f, kick);

        if (t >= 1f)
        {
            recoilTimer = -1f;
            barrel.localPosition = barrelRestLocalPos;
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

        Explosion.Spawn(transform.position + Vector3.up * 0.5f, 1.3f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyTankDestroyed();
        }
        if (GiantProgression.Instance != null)
        {
            GiantProgression.Instance.AddXP(10f);
        }

        Destroy(gameObject);
    }
}
