using UnityEngine;

// Shows a thin white outline around the birdcage model whenever the giant is close enough to
// actually do something with it (cage/release a held target with left-click, or open the
// Hostage Shop with an empty hand -- see GiantController.IsGiantNearCage). Reads its distance
// threshold directly from GiantController.cageInteractRadius at Start so it always agrees
// with whatever click would actually trigger, instead of duplicating that number and risking
// the two drifting apart.
//
// Added onto the birdcage GameObject itself by YardTreeBuilder right after it's built, since
// the cage (and its visual mesh) only exists once YardTreeBuilder instantiates it at runtime.
public class CageInteractHighlight : MonoBehaviour
{
    public Transform giant;
    public float interactRadius = 10f;
    public Color outlineColor = Color.white;
    [Tooltip("How much bigger than the real mesh the outline copy is scaled, so only a thin rim pokes out past the actual surface.")]
    public float outlineScale = 1.03f;

    private GameObject outline;
    private bool isNear;

    void Start()
    {
        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null)
        {
            giant = giantObj.transform;

            GiantController controller = giantObj.GetComponent<GiantController>();
            if (controller != null)
            {
                interactRadius = controller.cageInteractRadius;
            }
        }

        outline = BuildOutline(gameObject, outlineColor, outlineScale);
        if (outline != null)
        {
            outline.SetActive(false);
        }
    }

    void Update()
    {
        if (giant == null || outline == null)
        {
            return;
        }

        Vector3 a = giant.position; a.y = 0f;
        Vector3 b = transform.position; b.y = 0f;
        bool near = Vector3.Distance(a, b) <= interactRadius;

        if (near != isNear)
        {
            isNear = near;
            outline.SetActive(isNear);
        }
    }

    // Classic "inverted hull" outline: a slightly-larger, backface-only, unlit copy of every
    // mesh under `source`, so only a thin silhouette rim pokes out past the real surface from
    // every viewing angle -- no custom shader asset needed, just Cull Front on URP's Unlit
    // shader (confirmed to expose _Cull as a real material property in this project).
    static GameObject BuildOutline(GameObject source, Color color, float scale)
    {
        MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length == 0)
        {
            return null;
        }

        GameObject outlineRoot = new GameObject("InteractOutline");
        outlineRoot.transform.SetParent(source.transform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);

        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh == null)
            {
                continue;
            }

            // Built and sized in WORLD space first (no parent yet, so localScale IS world
            // scale) so the outline part's final size is correct regardless of how small the
            // cage's own transform scale is -- then reparented with worldPositionStays=true,
            // which lets Unity work out the right local values to keep that same world
            // transform under the (tiny-scaled) cage hierarchy.
            GameObject copy = new GameObject("OutlinePart");
            copy.transform.position = mf.transform.position;
            copy.transform.rotation = mf.transform.rotation;
            copy.transform.localScale = mf.transform.lossyScale * scale;
            copy.transform.SetParent(outlineRoot.transform, true);

            MeshFilter copyFilter = copy.AddComponent<MeshFilter>();
            copyFilter.sharedMesh = mf.sharedMesh;

            MeshRenderer copyRenderer = copy.AddComponent<MeshRenderer>();
            copyRenderer.sharedMaterial = mat;
            copyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            copyRenderer.receiveShadows = false;
        }

        return outlineRoot;
    }
}
