using UnityEngine;

// Simple expanding-fireball destroy effect shared by vehicles (police car / tank / helicopter).
public static class Explosion
{
    public static void Spawn(Vector3 position, float scale = 1f)
    {
        GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.name = "ExplosionFX";
        fx.transform.position = position;
        fx.transform.localScale = Vector3.one * 0.3f * scale;
        Object.Destroy(fx.GetComponent<Collider>());

        Renderer rend = fx.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.5f, 0.1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1f, 0.4f, 0.05f) * 6f);
        rend.material = mat;

        ImpactFlash flash = fx.AddComponent<ImpactFlash>();
        flash.duration = 0.4f;
        flash.maxScale = 9f * scale;
    }
}
