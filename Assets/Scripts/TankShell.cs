using UnityEngine;

// A fired tank shell: arcs from muzzle to the target position, then explodes and applies damage
// only if the impact point actually lands on the giant's body (otherwise it's a visual miss).
public class TankShell : MonoBehaviour
{
    private Vector3 start;
    private Vector3 target;
    private float speed;
    private float damage;
    private GiantHealth giantHealth;
    private float flightTime;
    private float elapsed;
    private float arcHeight;

    public void Init(Vector3 startPos, Vector3 targetPos, float shellSpeed, float shellDamage, GiantHealth health)
    {
        start = startPos;
        target = targetPos;
        speed = Mathf.Max(1f, shellSpeed);
        damage = shellDamage;
        giantHealth = health;

        float dist = Vector3.Distance(start, target);
        flightTime = Mathf.Max(0.12f, dist / speed);
        arcHeight = Mathf.Clamp(dist * 0.1f, 0.4f, 3f);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightTime);

        Vector3 pos = Vector3.Lerp(start, target, t);
        pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = pos;

        if (t >= 1f)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (giantHealth != null && giantHealth.IsPointInHitbox(transform.position))
        {
            giantHealth.TakeDamage(damage);
        }

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "ShellImpact";
        flash.transform.position = transform.position;
        flash.transform.localScale = Vector3.one * 0.35f;
        Destroy(flash.GetComponent<Collider>());

        Renderer rend = flash.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.5f, 0.1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 3f);
        rend.material = mat;

        ImpactFlash anim = flash.AddComponent<ImpactFlash>();
        anim.duration = 0.25f;
        anim.maxScale = 3f;

        Destroy(gameObject);
    }
}
