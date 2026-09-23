using UnityEngine;

// A flat, filled pie-slice ("fan"/부채꼴) that sweeps out from the giant on the left-click
// knockback punch — distinct from StompRing (the flat ground-impact disc every punch spawns):
// this is a solid wedge drawn at attack height, only used for the punch that actually shoves
// enemies back. Tilted downward (a diagonal downward slash) rather than lying flat.
public class ArmSwingEffect : MonoBehaviour
{
    private const int SegmentCount = 24;

    public float duration = 0.2f;
    [Tooltip("How many degrees the wedge is pitched downward from flat/horizontal, so it reads as a diagonal downward slash instead of a flat horizontal sweep.")]
    public float downTiltDeg = 30f;

    private Mesh mesh;
    private Renderer rend;
    private float radius;
    private float startAngleDeg;
    private float endAngleDeg;
    private Color baseColor;
    private float timer;

    private Vector3[] vertices;
    private int[] triangles;

    public static void Spawn(Vector3 center, Vector3 forward, float radius, float height, float arcAngleDeg, float durationSeconds, float downTiltDeg = 30f)
    {
        GameObject go = new GameObject("ArmSwingEffect");

        MeshFilter mfComp = go.AddComponent<MeshFilter>();
        MeshRenderer mrComp = go.AddComponent<MeshRenderer>();

        Material mat = new Material(Shader.Find("Sprites/Default"));
        Color c = new Color(0.85f, 0.95f, 1f, 0.55f);
        mat.color = c;
        mrComp.material = mat;
        mrComp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrComp.receiveShadows = false;

        Mesh mesh = new Mesh();
        mesh.name = "ArmSwingFan";
        mesh.MarkDynamic();
        mfComp.mesh = mesh;

        float forwardAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        ArmSwingEffect fx = go.AddComponent<ArmSwingEffect>();
        fx.mesh = mesh;
        fx.rend = mrComp;
        fx.radius = radius;
        fx.duration = durationSeconds;
        fx.downTiltDeg = downTiltDeg;
        fx.baseColor = c;
        // The mesh itself is built with angles relative to local forward (0deg = straight
        // ahead) -- facing and downward tilt are both handled by the transform's rotation
        // below, so this stays simple regardless of which way the giant is facing.
        fx.startAngleDeg = -arcAngleDeg * 0.5f;
        fx.endAngleDeg = arcAngleDeg * 0.5f;

        // The mesh is built in local space (center vertex at the origin, outer vertices at
        // `radius` from it), so the pivot goes at the giant's position raised to attack height —
        // the same point the swing sweeps around. Rotation faces the swing forward (yaw), then
        // pitches the whole wedge down (local X axis) so it reads as a downward diagonal slash
        // instead of a flat horizontal sweep.
        go.transform.position = center + Vector3.up * height;
        go.transform.rotation = Quaternion.AngleAxis(forwardAngle, Vector3.up) * Quaternion.AngleAxis(downTiltDeg, Vector3.right);

        fx.UpdateFan(0f);
    }

    void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        UpdateFan(t);

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    // First half of the lifetime sweeps the wedge open from the start edge to the full arc
    // (selling the arm actually swinging through), then the second half fades the whole
    // wedge out. Rebuilding the mesh every frame at this segment count/lifetime is cheap.
    void UpdateFan(float t)
    {
        float sweepT = Mathf.Clamp01(t / 0.5f);
        float fadeT = Mathf.Clamp01((t - 0.5f) / 0.5f);

        float visibleEndAngle = Mathf.Lerp(startAngleDeg, endAngleDeg, sweepT);

        int vertCount = SegmentCount + 2; // center + (SegmentCount + 1) outer rim points
        if (vertices == null || vertices.Length != vertCount)
        {
            vertices = new Vector3[vertCount];
            triangles = new int[SegmentCount * 6]; // 2 triangles (front+back) per segment
        }

        vertices[0] = Vector3.zero;

        for (int i = 0; i <= SegmentCount; i++)
        {
            float segT = (float)i / SegmentCount;
            float angle = Mathf.Lerp(startAngleDeg, visibleEndAngle, segT) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
        }

        int triIdx = 0;
        for (int i = 0; i < SegmentCount; i++)
        {
            int a = 0;
            int b = i + 1;
            int c = i + 2;

            // Both winding orders, so the wedge is visible from above and below regardless
            // of shader culling settings.
            triangles[triIdx++] = a;
            triangles[triIdx++] = b;
            triangles[triIdx++] = c;

            triangles[triIdx++] = a;
            triangles[triIdx++] = c;
            triangles[triIdx++] = b;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        if (rend != null)
        {
            Color col = baseColor;
            col.a = Mathf.Lerp(baseColor.a, 0f, fadeT);
            rend.material.color = col;
        }
    }
}
