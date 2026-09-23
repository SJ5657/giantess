using UnityEngine;

// A small heal item scattered around the city by HealthPickupSpawner. Walking the giant into
// one restores HP (capped at max) and removes the pickup; the spawner then drops a fresh one
// somewhere else after a short delay, so there's always a handful worth finding during a run.
public class HealthPickup : MonoBehaviour
{
    [Tooltip("How much HP this restores when picked up.")]
    public float healAmount = 60f;

    [HideInInspector] public HealthPickupSpawner spawner;

    [Header("Motion")]
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;
    public float spinSpeed = 90f;

    private Vector3 basePos;
    private float bobPhase;
    private bool collected;

    void Start()
    {
        basePos = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        float y = basePos.y + Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobHeight;
        transform.position = new Vector3(basePos.x, y, basePos.z);
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        GiantHealth health = other.GetComponentInParent<GiantHealth>();
        if (health == null) return;

        collected = true;
        health.Heal(healAmount);

        if (spawner != null) spawner.NotifyCollected();

        Destroy(gameObject);
    }

    // Procedurally builds a pickup at the given world position: a white base with a glowing
    // red cross on top (medkit-style), plus a soft light so it reads from a distance. Matches
    // VehicleSpawner's pattern of assembling GameObjects from primitives rather than needing
    // hand-authored prefabs.
    public static HealthPickup Spawn(Vector3 position, float healAmount, HealthPickupSpawner spawner)
    {
        GameObject go = new GameObject("HealthPickup");
        go.transform.position = position;

        GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        baseObj.name = "Base";
        baseObj.transform.SetParent(go.transform, false);
        baseObj.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
        baseObj.transform.localPosition = Vector3.zero;
        SetEmissiveColor(baseObj, Color.white, new Color(0.25f, 0.25f, 0.25f));
        Destroy(baseObj.GetComponent<Collider>());

        GameObject crossH = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crossH.name = "CrossH";
        crossH.transform.SetParent(go.transform, false);
        crossH.transform.localScale = new Vector3(0.6f, 0.14f, 0.14f);
        crossH.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        SetEmissiveColor(crossH, new Color(0.85f, 0.1f, 0.1f), new Color(0.9f, 0.15f, 0.15f));
        Destroy(crossH.GetComponent<Collider>());

        GameObject crossV = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crossV.name = "CrossV";
        crossV.transform.SetParent(go.transform, false);
        crossV.transform.localScale = new Vector3(0.14f, 0.6f, 0.14f);
        crossV.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        SetEmissiveColor(crossV, new Color(0.85f, 0.1f, 0.1f), new Color(0.9f, 0.15f, 0.15f));
        Destroy(crossV.GetComponent<Collider>());

        Light glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.45f, 0.45f);
        glow.range = 6f;
        glow.intensity = 1.2f;

        SphereCollider trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.2f;

        HealthPickup pickup = go.AddComponent<HealthPickup>();
        pickup.healAmount = healAmount;
        pickup.spawner = spawner;

        return pickup;
    }

    static void SetEmissiveColor(GameObject go, Color baseColor, Color emissive)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = baseColor;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissive);
        rend.material = mat;
    }
}
