using UnityEngine;

// Drives a police car toward the giant. When close enough, it stops and two officers
// get out and approach/fire (using TinySoldierAI). If the giant gets far away again,
// the officers stop fighting, walk back to the car, and the car resumes the chase.
// If every officer that got out is killed, the car gives up and stops chasing entirely.
public class PoliceCarAI : MonoBehaviour
{
    public enum State { Approaching, Deployed, Reboarding, Abandoned }

    [Header("Health")]
    public float maxHealth = 100f;
    private float health;
    private bool isDestroyed;
    private EnemyHealthBar healthBar;

    [Header("Movement")]
    public float moveSpeed = 10f;
    public float rotateSpeed = 3f;
    [Tooltip("Car stops and deploys officers once within this distance of the giant.")]
    public float disembarkRange = 22f;
    [Tooltip("Once deployed, officers re-board and the car resumes chasing once the giant is farther than this.")]
    public float reboardRange = 34f;

    [Header("Officers")]
    public TinySoldierAI[] officers = new TinySoldierAI[2];
    public Transform[] officerSeats = new Transform[2];

    [Header("Performance LOD")]
    [Tooltip("Beyond this distance from the giant, this car's chase/deploy logic updates only " +
        "a few times a second instead of every frame. Above reboardRange, so an actively " +
        "engaged car always stays at full fidelity.")]
    public float lodFarDistance = 45f;
    [Tooltip("How often (seconds) a far-away car's logic still updates.")]
    public float lodFarTickInterval = 0.25f;

    public State CurrentState { get; private set; } = State.Approaching;

    [Header("Knockback")]
    [Tooltip("How quickly an applied knockback velocity decays back to zero (higher = stops sooner.")]
    public float knockbackDrag = 3f;
    [Tooltip("While the current knockback speed is above this, this tick is a stagger: only the knockback displacement applies and the enemy's own movement/aim/fire logic is skipped, so a solid punch reads as a clean shove instead of being fought by the enemy's own AI movement.")]
    public float knockbackStunThreshold = 3f;

    private Transform giant;
    private bool anyOfficerEverDeployed;
    private float lodAccumDt;
    private Vector3 knockbackVelocity;

    void Start()
    {
        health = maxHealth;

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null)
        {
            giant = giantObj.transform;
        }

        foreach (var officer in officers)
        {
            if (officer != null) officer.gameObject.SetActive(false);
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
        TickKnockback(dt, 1f, 0.3f);
        if (knockbackVelocity.sqrMagnitude > knockbackStunThreshold * knockbackStunThreshold) return;

        // Once officers have gotten out at least once, if they've all since died, the car
        // has no one left to fight with, so it gives up and stops following the giant.
        if (anyOfficerEverDeployed && AllOfficersDead())
        {
            CurrentState = State.Abandoned;
            return;
        }

        Vector3 toGiant = giant.position - transform.position;
        toGiant.y = 0f;
        float dist = toGiant.magnitude;

        switch (CurrentState)
        {
            case State.Approaching:
                if (toGiant.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toGiant.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
                }

                if (dist > disembarkRange)
                {
                    Vector3 move = toGiant.normalized * moveSpeed * dt;
                    move = BuildingCollision.ClampAgainstBuildingsSliding(transform.position, move, 1f, 0.3f);
                    transform.position += move;
                }
                else
                {
                    Deploy();
                }
                break;

            case State.Deployed:
                if (dist > reboardRange)
                {
                    Reboard();
                }
                break;

            case State.Reboarding:
                TickReboarding(dt);
                break;
        }
    }

    // Shoves this car away, called by the giant's left-click punch. Adds onto any existing
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

    bool AllOfficersDead()
    {
        foreach (var officer in officers)
        {
            if (officer != null) return false;
        }
        return true;
    }

    void Deploy()
    {
        CurrentState = State.Deployed;
        anyOfficerEverDeployed = true;

        for (int i = 0; i < officers.Length; i++)
        {
            TinySoldierAI officer = officers[i];
            if (officer == null) continue;

            Transform seat = i < officerSeats.Length ? officerSeats[i] : transform;
            officer.transform.SetParent(null, true);
            officer.transform.position = seat.position;
            officer.transform.rotation = seat.rotation;
            officer.gameObject.SetActive(true);
            officer.enabled = true;
        }
    }

    void Reboard()
    {
        CurrentState = State.Reboarding;

        foreach (var officer in officers)
        {
            if (officer == null) continue;
            officer.enabled = false;
        }
    }

    void TickReboarding(float dt)
    {
        bool allArrived = true;

        for (int i = 0; i < officers.Length; i++)
        {
            TinySoldierAI officer = officers[i];
            if (officer == null || !officer.gameObject.activeSelf) continue;

            Transform seat = i < officerSeats.Length ? officerSeats[i] : transform;
            Vector3 targetPos = seat.position;

            officer.transform.position = Vector3.MoveTowards(officer.transform.position, targetPos, moveSpeed * dt);

            Vector3 toSeat = targetPos - officer.transform.position;
            toSeat.y = 0f;
            if (toSeat.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation((transform.position - officer.transform.position).normalized);
                officer.transform.rotation = Quaternion.Slerp(officer.transform.rotation, lookRot, rotateSpeed * dt);
            }

            if (officer.animator != null)
            {
                officer.animator.SetFloat("Speed", moveSpeed);
                officer.animator.SetBool("Aiming", false);
            }

            if (Vector3.Distance(officer.transform.position, targetPos) > 0.3f)
            {
                allArrived = false;
            }
        }

        if (allArrived)
        {
            foreach (var officer in officers)
            {
                if (officer == null) continue;
                officer.transform.SetParent(transform, true);
                officer.gameObject.SetActive(false);
            }
            CurrentState = State.Approaching;
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

        Explosion.Spawn(transform.position + Vector3.up * 0.4f, 1f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyPoliceDestroyed();
        }
        if (GiantProgression.Instance != null)
        {
            GiantProgression.Instance.AddXP(5f);
        }

        Destroy(gameObject);
    }
}
