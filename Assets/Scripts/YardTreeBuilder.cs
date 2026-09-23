using UnityEngine;

// Places a large decorative tree in the giant's yard and hangs a giant-scaled birdcage
// prop on one of its branches, connected by a visible rope, at runtime. Both the tree and
// the birdcage are existing asset prefabs (Toon City Pack vegetation + Assets/Models/bird_cage.glb)
// -- this script only instantiates, rescales and positions them, so the references must be
// wired in the Inspector (AssetDatabase lookups don't work outside the Editor / in a build).
public class YardTreeBuilder : MonoBehaviour
{
    [Header("Source Prefabs (wire in Inspector)")]
    public GameObject treePrefab;
    public GameObject birdCagePrefab;

    [Header("Placement")]
    public Vector3 treePosition = new Vector3(-40f, 0f, -195f);
    public float treeTargetHeight = 48f; // world units -- tall enough to read as a "big tree" next to a ~12-unit-tall giant
    public float treeYRotation = 35f;

    [Header("Birdcage")]
    [Range(0.05f, 0.4f)]
    public float cageHeightRatioOfTree = 0.18f; // cage height as a fraction of the tree's actual (scaled) height
    [Range(0.1f, 0.9f)]
    public float cageBranchHeightRatio = 0.55f; // how far up the trunk (0=base, 1=top) the branch/rope attach point sits, up inside the canopy
    [Range(0.05f, 0.8f)]
    public float cageOutwardOffsetRatio = 0.35f; // how far out from the trunk, as a fraction of the tree's canopy radius, the cage hangs -- large enough that the cage clears the trunk instead of overlapping/poking through it
    public float cageHangRotationY = 25f;
    [Tooltip("How far below the branch attach point (world units) the cage hangs at the end of its rope, so the cage sits clear of the foliage instead of touching/poking through it. Bigger = the cage hangs lower.")]
    public float cageRopeLength = 10f;
    public float ropeThickness = 0.15f;
    public Color ropeColor = new Color(0.32f, 0.22f, 0.12f);

    [Header("Collision")]
    [Tooltip("World-unit radius of the invisible capsule collider that makes the trunk solid, so the giant and tiny people can't walk straight through it. Kept trunk-only (not the whole canopy) so they can still walk near/under the overhanging leaves and reach the birdcage.")]
    public float trunkColliderRadius = 1.8f;
    [Tooltip("How far up the tree (0-1 of treeTargetHeight) the trunk's solid collider extends. Kept below the canopy so the leaves overhead don't also block movement -- only the actual trunk does.")]
    [Range(0.1f, 1f)]
    public float trunkColliderHeightRatio = 0.65f;

    private GameObject builtTree;
    private GameObject builtCage;
    private GameObject builtRope;

    void Start()
    {
        BuildTree();
    }

    void BuildTree()
    {
        if (treePrefab == null)
        {
            Debug.LogWarning("YardTreeBuilder: treePrefab not assigned.");
            return;
        }

        GameObject tree = Instantiate(treePrefab, transform);
        tree.name = "BigYardTree";
        tree.transform.position = treePosition;
        tree.transform.rotation = Quaternion.Euler(0f, treeYRotation, 0f);

        // Measure the tree's raw (unscaled) height from its actual mesh bounds, then scale
        // it uniformly so its real-world height matches treeTargetHeight. The prefab's pivot
        // is at the base of the trunk, so scaling in place keeps it planted on the ground.
        Bounds rawBounds = GetRendererBounds(tree);
        float rawHeight = Mathf.Max(rawBounds.size.y, 0.01f);
        float scale = treeTargetHeight / rawHeight;
        tree.transform.localScale = Vector3.one * scale;

        AddTrunkCollider(tree, rawBounds, scale);

        builtTree = tree;
        BuildBirdCage(tree);
    }

    void BuildBirdCage(GameObject tree)
    {
        if (birdCagePrefab == null)
        {
            Debug.LogWarning("YardTreeBuilder: birdCagePrefab not assigned.");
            return;
        }

        Bounds treeBounds = GetRendererBounds(tree);

        GameObject cage = Instantiate(birdCagePrefab, transform);
        cage.name = "YardTreeBirdCage";

        Bounds rawCageBounds = GetRendererBounds(cage);
        float rawCageHeight = Mathf.Max(rawCageBounds.size.y, 0.01f);
        float targetCageHeight = treeBounds.size.y * cageHeightRatioOfTree;
        float cageScale = targetCageHeight / rawCageHeight;
        cage.transform.localScale = Vector3.one * cageScale;

        // Where the rope's top end attaches, up inside the canopy -- tucked closer to the
        // trunk than the cage itself (see ropeBranchInwardRatio) so it reads as coming from
        // an actual branch rather than starting out in open air.
        float attachY = treeBounds.min.y + treeBounds.size.y * cageBranchHeightRatio;
        float fullOutward = Mathf.Max(treeBounds.size.x, treeBounds.size.z) * 0.5f * cageOutwardOffsetRatio;
        Vector3 dir = new Vector3(Mathf.Cos(Mathf.Deg2Rad * cageHangRotationY), 0f, Mathf.Sin(Mathf.Deg2Rad * cageHangRotationY)).normalized;

        // Same horizontal position as the cage itself (only Y differs) -- an actual
        // vertical rope hanging straight down out of the leaves, rather than a diagonal line
        // running from somewhere near the trunk out to the cage.
        Vector3 branchAttachPos = new Vector3(
            tree.transform.position.x + dir.x * fullOutward,
            attachY,
            tree.transform.position.z + dir.z * fullOutward
        );

        // The cage prefab's pivot is at its own base. To make the cage hang BELOW the branch
        // at the end of a cageRopeLength-long rope (instead of overlapping/poking up through
        // the foliage the way a plain branch-height placement did), first find where the
        // cage's TOP should sit (cageRopeLength below the branch attach point), then step down
        // by the cage's own height to get the base position actually assigned below.
        float cageTopY = attachY - cageRopeLength;
        float cageBaseY = cageTopY - targetCageHeight;

        Vector3 cagePos = new Vector3(
            tree.transform.position.x + dir.x * fullOutward,
            cageBaseY,
            tree.transform.position.z + dir.z * fullOutward
        );

        cage.transform.position = cagePos;
        cage.transform.rotation = Quaternion.Euler(0f, cageHangRotationY, 0f);

        AddCageCollider(cage);
        cage.AddComponent<CageInteractHighlight>();

        builtCage = cage;

        Vector3 ropeTopWorld = new Vector3(cagePos.x, cageTopY, cagePos.z);
        BuildRope(branchAttachPos, ropeTopWorld);
    }

    // A thin stretched cylinder connecting the branch attach point down to the top of the
    // cage, so the cage visibly hangs from the tree by a rope instead of floating with a gap
    // below the foliage and nothing visually connecting it.
    void BuildRope(Vector3 from, Vector3 to)
    {
        GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rope.name = "YardTreeBirdCageRope";
        rope.transform.SetParent(transform, false);

        Collider ropeCollider = rope.GetComponent<Collider>();
        if (ropeCollider != null)
        {
            Destroy(ropeCollider);
        }

        Vector3 mid = (from + to) * 0.5f;
        float length = Mathf.Max(Vector3.Distance(from, to), 0.01f);
        rope.transform.position = mid;
        rope.transform.up = (to - from).normalized;
        // Unity's built-in cylinder primitive is 2 world units tall at scale 1 (local Y runs
        // -1..1), so a Y scale of length/2 stretches it to exactly span from -> to.
        rope.transform.localScale = new Vector3(ropeThickness, length * 0.5f, ropeThickness);

        Renderer rend = rope.GetComponent<Renderer>();
        if (rend != null)
        {
            // Match whatever render pipeline the project is actually using (URP here) --
            // "Standard" is the Built-in RP shader and renders as broken/magenta under URP.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ropeColor);
            else mat.color = ropeColor;
            rend.material = mat;
        }

        builtRope = rope;
    }

    // Solid, trunk-only capsule collider (not the whole canopy) so the giant's
    // CharacterController (which collides with any collider automatically) and tiny NPCs/
    // soldiers (which only block against colliders tagged "Building" -- see
    // BuildingCollision.cs) both treat the trunk as solid, while still leaving the area
    // under/around the overhanging leaves walkable so the birdcage stays reachable.
    void AddTrunkCollider(GameObject tree, Bounds rawBounds, float scale)
    {
        CapsuleCollider capsule = tree.AddComponent<CapsuleCollider>();
        capsule.direction = 1; // Y axis

        // rawBounds/scale are both captured BEFORE tree.transform.localScale was applied, so
        // these are the tree's own local-space (pre-scale) measurements -- exactly what a
        // collider on this same transform needs, since Unity scales collider size by the
        // transform's own scale automatically.
        float localHeight = rawBounds.size.y * trunkColliderHeightRatio;
        float localRadius = trunkColliderRadius / Mathf.Max(scale, 0.0001f);

        capsule.height = localHeight;
        capsule.radius = localRadius;
        // Pivot is at the base of the trunk (see BuildTree's own comment), so the capsule's
        // center sits half its own height above that same local origin.
        capsule.center = new Vector3(0f, localHeight * 0.5f, 0f);

        tree.tag = "Building";
    }

    // Same box-collider-from-renderer-bounds approach CityGenerator.AddSolidCollider() uses
    // for real buildings, applied to the birdcage so it's just as solid -- tagged "Building"
    // for the same reason (see AddTrunkCollider above).
    void AddCageCollider(GameObject cage)
    {
        Renderer[] renderers = cage.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 lossyScale = cage.transform.lossyScale;
        Vector3 localSize = new Vector3(
            lossyScale.x != 0f ? worldBounds.size.x / lossyScale.x : worldBounds.size.x,
            lossyScale.y != 0f ? worldBounds.size.y / lossyScale.y : worldBounds.size.y,
            lossyScale.z != 0f ? worldBounds.size.z / lossyScale.z : worldBounds.size.z
        );

        BoxCollider box = cage.AddComponent<BoxCollider>();
        box.center = cage.transform.InverseTransformPoint(worldBounds.center);
        box.size = localSize;

        cage.tag = "Building";
    }

    static Bounds GetRendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
