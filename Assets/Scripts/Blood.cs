using UnityEngine;

// Small red particle burst used whenever a tiny person or soldier dies -- stomped (see
// StompZone) or killed by a thrown object/vehicle (see GiantController.DestroyThrownOrHit) --
// deliberately understated (a quick handful of droplets, brief lifetime) rather than a big
// dramatic effect, matching how lightly the rest of the game already treats these deaths (a
// quiet Destroy + score add, no explosion). Built procedurally like Explosion.Spawn, so no
// external particle asset is needed.
public static class Blood
{
    public static void Spawn(Vector3 position, float scale = 1f)
    {
        GameObject fx = new GameObject("BloodFX");
        fx.transform.position = position;

        ParticleSystem ps = fx.AddComponent<ParticleSystem>();
        // AddComponent<ParticleSystem>() auto-plays immediately (Play On Awake defaults to true),
        // and Unity refuses to change main-module settings like duration while a system is
        // already playing. Stop it first so the configuration below actually takes effect.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = 0.5f;
        main.startSpeed = 3.5f * scale;
        main.startSize = 0.14f * scale;
        main.startColor = new Color(0.5f, 0.02f, 0.02f);
        main.gravityModifier = 3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(12 * scale))
        });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.5f, 0.02f, 0.02f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.35f, 0.01f, 0.01f) * 1.2f);
        renderer.material = mat;

        ps.Play();
        Object.Destroy(fx, main.duration + main.startLifetime.constantMax + 0.3f);
    }
}
