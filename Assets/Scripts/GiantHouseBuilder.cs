using UnityEngine;
using System.Collections;

// Builds the giant's own home just south of the city: a walled yard with the house inside it
// and a single gate in the front wall (the side facing the city). Sized to actually match the
// giant (roughly 12 units tall, ~2.2 unit radius), not a human-scale building. Every wall
// segment and the gate's invisible blocker are tagged "Building" so TinyNPC/TinySoldierAI/
// enemy vehicle AI treat them exactly like city buildings (BuildingCollision's avoidance
// blocks them same as always) and the giant's CharacterController physically collides with
// them too -- except the gate, which GiantOnlyGate.cs specifically lets the giant pass
// through, making it the giant's own private entrance. The gate's visual door leaves swing
// open as the giant approaches (GiantGateDoor.cs) purely for show; the actual blocking never
// moves.
public class GiantHouseBuilder : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("World-space center of the whole compound (yard + walls). South of the city by default so it doesn't overlap any generated city block.")]
    public Vector3 compoundCenter = new Vector3(0f, 0f, -205f);

    [Header("Yard / Wall (scaled to the giant's real size: ~12 tall, ~2.2 radius)")]
    public float yardHalfSize = 70f;
    public float wallHeight = 20f;
    public float wallThickness = 3f;
    [Tooltip("Width of the gap left in the front wall for the gate. Comfortably wider than the giant's ~4.4 unit diameter.")]
    public float gateWidth = 14f;

    [Header("House")]
    public Vector3 houseSize = new Vector3(55f, 26f, 40f);
    [Tooltip("Gap kept between the house's back wall and the compound's back wall.")]
    public float houseBackMargin = 6f;

    [Header("Ground")]
    [Tooltip("Extra margin added beyond the compound's own footprint when growing the shared Ground plane, so nothing stands over empty space.")]
    public float groundBuffer = 20f;

    static readonly Color WallColor = new Color(0.55f, 0.5f, 0.42f);
    static readonly Color HouseWallColor = new Color(0.78f, 0.68f, 0.5f);
    static readonly Color RoofColor = new Color(0.45f, 0.18f, 0.14f);
    static readonly Color DoorColor = new Color(0.32f, 0.2f, 0.12f);
    static readonly Color YardColor = new Color(0.4f, 0.47f, 0.27f); // muted/desaturated grass tone -- less of a saturated cartoon green

    void Start()
    {
        BuildYardGround();
        BuildWalls();
        BuildHouse();
        BuildSpawnPoint();
        BuildSavePoint();
        StartCoroutine(EnsureGroundCoversCompound());
    }

    // A marker GameObject just inside the gate that GameFlowManager teleports the giant to
    // when starting a fresh (non-loaded) game, facing further into the yard toward the house.
    void BuildSpawnPoint()
    {
        GameObject spawn = new GameObject("SpawnPoint");
        spawn.transform.SetParent(transform, false);
        spawn.transform.position = compoundCenter + new Vector3(0f, 0.1f, yardHalfSize - 20f);
        spawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face south, into the yard/toward the house
    }

    // A simple glowing pedestal the giant can interact with (SavePoint.cs, press F in range)
    // to open the save-slot picker in Save mode.
    void BuildSavePoint()
    {
        GameObject savePointGo = new GameObject("SavePoint");
        savePointGo.transform.SetParent(transform, false);
        savePointGo.transform.position = compoundCenter + new Vector3(20f, 0f, yardHalfSize - 35f);

        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Pedestal";
        pedestal.transform.SetParent(savePointGo.transform, false);
        pedestal.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
        pedestal.transform.localPosition = new Vector3(0f, 1f, 0f);
        SetColor(pedestal, new Color(0.4f, 0.4f, 0.42f));

        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = "Crystal";
        crystal.transform.SetParent(savePointGo.transform, false);
        crystal.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
        crystal.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        crystal.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
        SetEmissive(crystal, new Color(0.2f, 0.4f, 0.9f), new Color(0.3f, 0.55f, 1f));
        Destroy(crystal.GetComponent<Collider>());
        crystal.AddComponent<RotorSpin>().spinSpeed = 40f;

        Light glow = savePointGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.35f, 0.55f, 1f);
        glow.range = 12f;
        glow.intensity = 1.5f;

        savePointGo.AddComponent<SavePoint>();
    }

    static void SetEmissive(GameObject go, Color baseColor, Color emissive)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = baseColor;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissive);
        rend.material = mat;
    }

    // The city's own generator (re)sizes the shared "Ground" plane around itself every time it
    // runs. Waiting a frame guarantees that has already happened before we check whether it's
    // big enough to also reach the compound, and we only ever grow it, never shrink it.
    IEnumerator EnsureGroundCoversCompound()
    {
        yield return null;

        GameObject ground = GameObject.Find("Ground");
        if (ground == null) yield break;

        float neededHalfExtent = Mathf.Max(Mathf.Abs(compoundCenter.x), Mathf.Abs(compoundCenter.z)) + yardHalfSize + groundBuffer;
        float neededScale = neededHalfExtent / 5f; // Unity's default Plane is 10x10 units (half-size 5) at scale 1.

        Vector3 currentScale = ground.transform.localScale;
        float newScale = Mathf.Max(currentScale.x, currentScale.z, neededScale);
        ground.transform.localScale = new Vector3(newScale, 1f, newScale);
    }

    void BuildYardGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "YardGround";
        ground.transform.SetParent(transform, false);
        ground.transform.position = compoundCenter + Vector3.up * 0.02f;
        float scale = (yardHalfSize * 2f) / 10f;
        ground.transform.localScale = new Vector3(scale, 1f, scale);
        SetColor(ground, YardColor, 0.08f); // matte -- a glossy flat plane reads as shiny plastic astroturf rather than natural grass
        Destroy(ground.GetComponent<Collider>()); // purely cosmetic; the main Ground mesh already handles walking
    }

    void BuildWalls()
    {
        GameObject wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(transform, false);

        float half = yardHalfSize;
        float wallY = wallHeight * 0.5f;

        // Back wall (opposite the gate).
        AddWallSegment(wallsParent.transform, compoundCenter + new Vector3(0f, wallY, -half), new Vector3(half * 2f + wallThickness, wallHeight, wallThickness));

        // Left / right walls.
        AddWallSegment(wallsParent.transform, compoundCenter + new Vector3(-half, wallY, 0f), new Vector3(wallThickness, wallHeight, half * 2f + wallThickness));
        AddWallSegment(wallsParent.transform, compoundCenter + new Vector3(half, wallY, 0f), new Vector3(wallThickness, wallHeight, half * 2f + wallThickness));

        // Front wall (facing the city), split around a centered gate opening.
        float sideSpan = (half * 2f - gateWidth) * 0.5f;
        float sideCenterOffsetX = gateWidth * 0.5f + sideSpan * 0.5f;
        AddWallSegment(wallsParent.transform, compoundCenter + new Vector3(-sideCenterOffsetX, wallY, half), new Vector3(sideSpan, wallHeight, wallThickness));
        AddWallSegment(wallsParent.transform, compoundCenter + new Vector3(sideCenterOffsetX, wallY, half), new Vector3(sideSpan, wallHeight, wallThickness));

        BuildGate(wallsParent.transform, compoundCenter + new Vector3(0f, wallY, half));
    }

    void AddWallSegment(Transform parent, Vector3 position, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "WallSegment";
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = size;
        SetColor(wall, WallColor);
        wall.tag = "Building";
    }

    // The gate opening: an invisible blocker handles ALL the actual physics (solid to
    // everyone, ignored only by the giant via GiantOnlyGate) and never moves. Two visual door
    // leaves sit on top of it purely for show, swinging open/closed via GiantGateDoor.
    void BuildGate(Transform parent, Vector3 gateCenter)
    {
        GameObject gateRoot = new GameObject("Gate");
        gateRoot.transform.SetParent(parent, false);
        gateRoot.transform.position = gateCenter;

        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "GateBlocker";
        blocker.transform.SetParent(gateRoot.transform, false);
        blocker.transform.localPosition = Vector3.zero;
        blocker.transform.localScale = new Vector3(gateWidth, wallHeight, wallThickness);
        blocker.tag = "Building";
        Destroy(blocker.GetComponent<Renderer>());
        blocker.AddComponent<GiantOnlyGate>();

        float leafWidth = gateWidth * 0.5f;
        float leafThickness = wallThickness * 0.6f;

        Transform leftPivot = BuildDoorLeaf(gateRoot.transform, "Left", -gateWidth * 0.5f, leafWidth, leafThickness, true);
        Transform rightPivot = BuildDoorLeaf(gateRoot.transform, "Right", gateWidth * 0.5f, leafWidth, leafThickness, false);

        GiantGateDoor doorScript = gateRoot.AddComponent<GiantGateDoor>();
        doorScript.leftPivot = leftPivot;
        doorScript.rightPivot = rightPivot;
    }

    // One swinging door leaf hinged at localHingeX (the gate opening's outer edge). The leaf
    // mesh is offset from its own pivot so rotating the pivot swings the leaf around that
    // hinge edge instead of its own center, like a real door.
    Transform BuildDoorLeaf(Transform parent, string label, float localHingeX, float leafWidth, float leafThickness, bool isLeft)
    {
        GameObject pivot = new GameObject(label + "Pivot");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = new Vector3(localHingeX, 0f, 0f);

        GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = label + "Leaf";
        leaf.transform.SetParent(pivot.transform, false);
        float sign = isLeft ? 1f : -1f; // leaf extends inward from its hinge, toward the opening's center
        leaf.transform.localPosition = new Vector3(sign * leafWidth * 0.5f, 0f, 0f);
        leaf.transform.localScale = new Vector3(leafWidth, wallHeight, leafThickness);
        SetColor(leaf, DoorColor);
        Destroy(leaf.GetComponent<Collider>()); // purely visual; GateBlocker handles the real blocking

        return pivot.transform;
    }

    void BuildHouse()
    {
        GameObject house = new GameObject("House");
        house.transform.SetParent(transform, false);

        float houseZ = compoundCenter.z - (yardHalfSize - houseSize.z * 0.5f - houseBackMargin);
        house.transform.position = new Vector3(compoundCenter.x, 0f, houseZ);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(house.transform, false);
        body.transform.localPosition = new Vector3(0f, houseSize.y * 0.5f, 0f);
        body.transform.localScale = houseSize;
        SetColor(body, HouseWallColor);
        body.tag = "Building";

        // Simple gable roof made of two slanted panels.
        float roofOverhang = 2f;
        float roofWidth = houseSize.x + roofOverhang * 2f;
        float roofThickness = 0.6f;
        float roofRise = houseSize.y * 0.35f;
        float roofRun = houseSize.z * 0.5f + roofOverhang;
        float roofPanelDepth = Mathf.Sqrt(roofRun * roofRun + roofRise * roofRise);
        float roofAngle = Mathf.Atan2(roofRise, roofRun) * Mathf.Rad2Deg;
        float roofBaseY = houseSize.y + roofRise * 0.5f;

        GameObject roofFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofFront.name = "RoofFront";
        roofFront.transform.SetParent(house.transform, false);
        roofFront.transform.localScale = new Vector3(roofWidth, roofThickness, roofPanelDepth);
        roofFront.transform.localPosition = new Vector3(0f, roofBaseY, houseSize.z * 0.25f);
        roofFront.transform.localRotation = Quaternion.Euler(roofAngle, 0f, 0f);
        SetColor(roofFront, RoofColor);
        Destroy(roofFront.GetComponent<Collider>());

        GameObject roofBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofBack.name = "RoofBack";
        roofBack.transform.SetParent(house.transform, false);
        roofBack.transform.localScale = new Vector3(roofWidth, roofThickness, roofPanelDepth);
        roofBack.transform.localPosition = new Vector3(0f, roofBaseY, -houseSize.z * 0.25f);
        roofBack.transform.localRotation = Quaternion.Euler(-roofAngle, 0f, 0f);
        SetColor(roofBack, RoofColor);
        Destroy(roofBack.GetComponent<Collider>());

        // Purely cosmetic door on the side facing the gate.
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.SetParent(house.transform, false);
        door.transform.localScale = new Vector3(houseSize.x * 0.2f, houseSize.y * 0.55f, 0.15f);
        door.transform.localPosition = new Vector3(0f, houseSize.y * 0.275f, houseSize.z * 0.5f + 0.08f);
        SetColor(door, DoorColor);
        Destroy(door.GetComponent<Collider>());
    }

    static void SetColor(GameObject go, Color color, float smoothness = 0.5f)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Smoothness", smoothness);
        rend.material = mat;
    }
}
