using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CityGenerator : MonoBehaviour
{
    [Header("Grid")]
    public int gridSize = 8;
    public float spacing = 14f;
    public float jitter = 3f;
    public float clearRadius = 12f;

    [Header("Buildings (fallback primitives)")]
    public bool generateBuildings = false;
    public float minHeight = 3f;
    public float maxHeight = 16f;
    public float minWidth = 3f;
    public float maxWidth = 6f;

    [Header("Real City Model (Maps asset)")]
    [Tooltip("The full city model (e.g. Assets/Maps/.../PQ_Remake_AKIHABARA.fbx). When set, a randomized layout of it is spawned every time the game starts.")]
    public GameObject cityModelPrefab;
    [Tooltip("Uniform scale applied to the whole city model.")]
    public float cityScale = 2f;
    [Tooltip("Top-level district chunks inside the city model that can be randomly included/omitted each run.")]
    public string[] cityBlockNames = new string[] { "Block_A", "Block_B", "Block_C", "Block_D", "Block_E", "Block_F", "Block_G", "Block_H", "Block_I" };
    [Tooltip("Each run, a random number of blocks between 0 and this many are left out, for a different skyline every time.")]
    public int maxBlocksToOmit = 3;
    [Tooltip("Skybox materials to pick from at random each run (e.g. Assets/Maps/Skyboxes/*.mat). Only ones whose name contains \"Sunny\" or \"Overcast\" are used, so the sky always reads as daytime.")]
    public Material[] skyboxMaterials;

    [Header("Toon City Pack (modular kit, new asset)")]
    [Tooltip("Regular building prefabs.")]
    public GameObject[] kitBuildingPrefabs;
    [Tooltip("Big set-piece prefabs (large buildings, stadium/hospital/park/etc.) - picked less often; each one takes up a 2x2 super-block by itself.")]
    public GameObject[] kitLandmarkPrefabs;
    [Tooltip("Trees/bushes/rocks scattered between buildings. Currently unused - see GenerateCityFromKit.")]
    public GameObject[] kitVegetationPrefabs;
    [Tooltip("Small street props: lamps, signs, benches, hydrants, etc. Currently unused - see GenerateCityFromKit.")]
    public GameObject[] kitPropPrefabs;
    [Tooltip("Parked/decorative vehicles. Currently unused - see GenerateCityFromKit.")]
    public GameObject[] kitVehiclePrefabs;
    [Tooltip("Material for the base ground/sidewalk plane (light concrete) under the whole city.")]
    public Material kitSidewalkMaterial;
    [Tooltip("Material for the dark asphalt road strips that run along the block grid lines.")]
    public Material kitGroundMaterial;
    [Tooltip("Optional material for a center-line stripe painted on top of the road strips. Leave unassigned for plain roads with no lane markings.")]
    public Material kitRoadLineMaterial;
    [Tooltip("Size of one grid cell/block (a regular building gets one cell; landmarks and multi-building lots merge several).")]
    public float kitCellSize = 32f;
    [Tooltip("Width of each road strip.")]
    public float kitRoadWidth = 4.5f;

    [Header("Tiny People")]
    public int tinyCount = 60;
    [Tooltip("Seconds between automatic citizen top-ups -- keeps the city from staying empty after the giant squashes/kills people during a long run.")]
    public float tinyRespawnInterval = 6f;
    [Tooltip("Minimum citizens spawned per top-up cycle even if the population is already close to tinyCount, so numbers keep trickling in during a long fight rather than only refilling losses one-for-one.")]
    public int tinyRespawnMinPerCycle = 3;
    public float tinyScale = 0.65f;
    public GameObject tinyPersonPrefab;
    public RuntimeAnimatorController tinyAnimatorController;

    // The Random seed used for the most recent full city + tiny-people generation (either the
    // initial Start() call or a later RegenerateFromSeed call). SaveSystem reads this at save
    // time so a loaded save can reproduce the exact same map/citizens instead of whatever
    // random layout happened to be showing when the scene last loaded (see RegenerateFromSeed).
    public static int LastUsedSeed { get; private set; }

    static readonly Color[] BuildingPalette = new Color[]
    {
        new Color(1f, 0.55f, 0.3f),
        new Color(0.4f, 0.75f, 1f),
        new Color(1f, 0.85f, 0.3f),
        new Color(0.6f, 0.9f, 0.5f),
        new Color(0.9f, 0.5f, 0.85f),
        new Color(0.7f, 0.6f, 1f)
    };

    static readonly Color[] TinyPalette = new Color[]
    {
        Color.red, Color.blue, Color.yellow, Color.green, Color.magenta, Color.cyan
    };

    // A planned block on the grid: an arbitrary set of cells (a single cell, a straight
    // 2x1/1x2 pair, an L-tromino, or a full 2x2), all treated as one open lot.
    enum BlockKind { Landmark, MultiLot, Single, Prop }
    struct PlannedBlock
    {
        public List<Vector2Int> cells;
        public BlockKind kind;
    }

    // Local-space (relative to CityRoot, which itself gets a random Y rotation applied at the
    // end of GenerateCityFromKit) adjacency graph of the road network actually drawn by
    // DrawRoadGrid: keys/values are grid-intersection coordinates (0..gridSize on both axes),
    // and an edge between two adjacent intersections exists exactly where a road segment was
    // actually drawn there (not suppressed by a merged lot). TrafficCarSpawner/TrafficCarAI use
    // this plus GridToLocal to drive cars along the real street layout and turn correctly at
    // intersections, and stay correct after the city's own random rotation because everything
    // here is expressed in CityRoot's own local space (a child's local position/rotation is
    // unaffected by how its parent is itself rotated).
    public Dictionary<Vector2Int, List<Vector2Int>> RoadGraph { get; private set; }
    public Transform CityRoot { get; private set; }
    public float RoadGridCellSize { get; private set; }
    public float RoadGridHalfExtent { get; private set; }

    public Vector3 GridToLocal(Vector2Int node)
    {
        return new Vector3(node.x * RoadGridCellSize - RoadGridHalfExtent, 0f, node.y * RoadGridCellSize - RoadGridHalfExtent);
    }

    void Start()
    {
        // Picked fresh from whatever ambient Random state exists at scene load (same as
        // before this field existed -- still a different city every time the scene loads
        // normally), but now captured so a save made during this run can store it, and
        // RegenerateFromSeed can reproduce this exact layout later for a Load.
        LastUsedSeed = Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(LastUsedSeed);

        GenerateWorld();

        // Started exactly once here (not inside GenerateWorld(), which also runs on every
        // RegenerateFromSeed/Load -- starting it there would stack up duplicate loops).
        StartCoroutine(TinyRespawnLoop());
    }

    // The actual generation dispatch, shared by Start() (fresh random layout) and
    // RegenerateFromSeed (deterministic replay of a saved layout). Callers are responsible
    // for seeding Random.InitState beforehand.
    void GenerateWorld()
    {
        if (kitBuildingPrefabs != null && kitBuildingPrefabs.Length > 0)
        {
            GenerateCityFromKit();
        }
        else if (cityModelPrefab != null)
        {
            GenerateCity();
        }
        else if (generateBuildings)
        {
            GenerateBuildings();
        }
        GenerateTinyPeople();

        // Re-populate ambient road traffic to match whatever road network (if any) this
        // generation pass just built -- handles both the very first Start() and every later
        // RegenerateFromSeed/Load uniformly. No-ops harmlessly if no spawner exists in the
        // scene, or the active generation path isn't the kit/grid one (RoadGraph stays empty).
        TrafficCarSpawner trafficSpawner = FindObjectOfType<TrafficCarSpawner>();
        if (trafficSpawner != null)
        {
            trafficSpawner.RespawnAll();
        }
    }

    // Tears down whatever city/tiny-people this instance currently has and rebuilds them
    // deterministically from the given seed, so the result is byte-for-byte the same layout
    // every time this exact seed is used. Used by SaveSystem.LoadFromSlot: the scene already
    // generated a random city when it loaded (for the start screen / a fresh New Game), but a
    // Load needs to show the map as it was when that slot was saved, not that random one.
    public void RegenerateFromSeed(int seed)
    {
        var toRemove = new List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.name == "CityModel" || child.name == "CityModel_Kit" || child.CompareTag("Tiny") || child.CompareTag("Building"))
            {
                toRemove.Add(child.gameObject);
            }
        }
        // Tiny people are instantiated as children of this transform, but sweep the whole
        // scene too in case any stray ones ended up elsewhere (defensive, cheap at this size).
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Tiny"))
        {
            if (!toRemove.Contains(go)) toRemove.Add(go);
        }
        foreach (var go in toRemove)
        {
            // DestroyImmediate (not Destroy) is required here specifically: RegenerateFromSeed
            // is called synchronously mid-script by SaveSystem.LoadFromSlot, which immediately
            // follows this call with GiantController.RestoreCagedHostages -- itself an
            // immediate FindGameObjectsWithTag("Tiny") query. Destroy() only queues removal
            // for end-of-frame, so without DestroyImmediate, RestoreCagedHostages would still
            // see (and could cage) these stale about-to-be-destroyed citizens, which vanish out
            // from under it once the frame ends.
            if (go != null) DestroyImmediate(go);
        }

        LastUsedSeed = seed;
        Random.InitState(seed);
        GenerateWorld();
    }

    // Spawns the real city model (from Assets/Maps) with a randomized layout: a random
    // subset of its district blocks, a random overall rotation, and a random skybox -
    // so the city looks different every time the game starts (roguelite-style).
    void GenerateCity()
    {
        GameObject cityRoot = Instantiate(cityModelPrefab);
        cityRoot.name = "CityModel";
        cityRoot.transform.localScale = Vector3.one * cityScale;

        // Randomly omit a handful of district blocks each run, so the skyline/layout varies.
        int omitCount = Random.Range(0, maxBlocksToOmit + 1);
        var indices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < cityBlockNames.Length; i++) indices.Add(i);
        for (int i = 0; i < omitCount && indices.Count > 0; i++)
        {
            int pick = Random.Range(0, indices.Count);
            string blockName = cityBlockNames[indices[pick]];
            indices.RemoveAt(pick);

            Transform block = cityRoot.transform.Find(blockName);
            if (block != null) block.gameObject.SetActive(false);
        }

        // Random overall rotation for a fresh orientation each run. This must happen
        // BEFORE recentering below, so the recenter step accounts for the rotated footprint.
        float randomYaw = Random.Range(0f, 360f);
        cityRoot.transform.Rotate(Vector3.up, randomYaw, Space.World);

        // Recenter the (now scaled and rotated) model so its footprint is centered near
        // the world origin (where the giant spawns), regardless of the coordinates baked
        // into its meshes.
        Renderer[] renderers = cityRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 offset = new Vector3(-bounds.center.x, 0f, -bounds.center.z);
            cityRoot.transform.position += offset;
        }

        cityRoot.transform.SetParent(transform, true);

        ApplyRandomDaySkybox();
    }

    // Procedurally builds a city out of the modular "Toon City Pack" kit on a uniform
    // grid. Most blocks are a single kitCellSize x kitCellSize cell, but some blocks are
    // MERGED with their neighbors - a straight 2x1/1x2 pair, an L-tromino (3 cells), or a
    // full 2x2 - into one open lot with no road running through its interior. A landmark
    // gets a 2x2 lot to itself; a "multi lot" gets one regular building per cell, so 2 or
    // more buildings end up sharing the same open, road-free lot. On top of that, a
    // single ungrouped cell can ALSO end up with 2-3 small buildings clustered together
    // in it, when the building picked for it happens to be small.
    //
    // The road grid is planned and drawn first (with every merged lot's interior
    // segments left out), THEN buildings are placed into the interior of their cell(s),
    // so a building footprint can never land on top of a road.
    //
    // NOTE: vegetation, vehicles and small street props (lamps/signs) are temporarily
    // disabled per request - only buildings and landmarks are placed for now.
    void GenerateCityFromKit()
    {
        GameObject cityRoot = new GameObject("CityModel_Kit");
        cityRoot.transform.SetParent(transform, false);

        int n = Mathf.Max(4, gridSize);
        float half = n * kitCellSize * 0.5f;

        // PASS 1: plan every block on the grid (deciding size/shape/kind, not content
        // yet) without instantiating anything, so the road layout can be derived from it.
        bool[,] occupied = new bool[n, n];
        var blocks = new List<PlannedBlock>();

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (occupied[i, j]) continue;

                float roll = Random.value;

                if (roll < 0.05f && CanFit(occupied, n, RectCells(i, j, 2, 2)))
                {
                    AddBlock(blocks, occupied, RectCells(i, j, 2, 2), BlockKind.Landmark);
                }
                else if (roll < 0.20f && TryPlaceMultiLot(occupied, n, i, j, blocks))
                {
                    // handled inside TryPlaceMultiLot
                }
                else if (roll < 0.68f)
                {
                    AddBlock(blocks, occupied, new List<Vector2Int> { new Vector2Int(i, j) }, BlockKind.Single);
                }
                // else: leave the cell empty (vegetation/vehicles/props disabled for now)
            }
        }

        // PASS 2: turn the merged blocks into suppressed road-segment lookups, so the
        // interior boundary of every merged lot (whatever its shape) is simply never
        // drawn - any two cells belonging to the SAME block that share an edge get that
        // edge's road segment suppressed.
        bool[,] suppressV = new bool[n + 1, n]; // suppressV[line, rowSegment]
        bool[,] suppressH = new bool[n + 1, n]; // suppressH[line, colSegment]
        foreach (var b in blocks)
        {
            var cellSet = new HashSet<Vector2Int>(b.cells);
            foreach (var c in b.cells)
            {
                if (cellSet.Contains(new Vector2Int(c.x + 1, c.y))) suppressV[c.x + 1, c.y] = true;
                if (cellSet.Contains(new Vector2Int(c.x, c.y + 1))) suppressH[c.y + 1, c.x] = true;
            }
        }

        // Reuse the scene's existing ground plane instead of spawning a second one -
        // two coplanar ground planes at y=0 z-fight (flicker). The existing ground stays
        // un-rotated and gets a generous size margin so it still fully covers the city
        // regardless of the random rotation applied to the buildings below. This plane is
        // the light sidewalk/pavement base that sits under the road strips (and shows
        // through wherever a merged lot's interior road segment was left out).
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }
        ground.transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        ground.transform.rotation = Quaternion.identity;
        float groundSize = n * kitCellSize * 1.5f;
        float neededGroundScale = groundSize / 10f;
        // Never SHRINK the shared ground plane, only grow it. This same plane also gets grown
        // by GiantHouseBuilder.EnsureGroundCoversCompound (once, at the very first scene Start)
        // to additionally cover the giant's walled yard south of the city, which sits well
        // outside the city's own footprint. GenerateWorld() runs again on every single Load
        // (see CityGenerator.RegenerateFromSeed), and used to unconditionally overwrite the
        // scale to exactly fit just the city -- silently shrinking the plane back down and
        // wiping out the yard's floor coverage every time, even though nothing here ever
        // regenerates the yard itself. Anyone loaded back into the yard (where the SavePoint
        // is, so nearly every real save) then had literally no ground collider under them and
        // fell straight through. Taking the max of the current and newly-needed scale keeps
        // the city always covered while never taking coverage away from anything else sharing
        // this same plane.
        float currentGroundScale = Mathf.Max(ground.transform.localScale.x, ground.transform.localScale.z);
        float finalGroundScale = Mathf.Max(neededGroundScale, currentGroundScale);
        ground.transform.localScale = new Vector3(finalGroundScale, 1f, finalGroundScale);
        if (kitSidewalkMaterial != null)
        {
            MeshRenderer groundRend = ground.GetComponent<MeshRenderer>();
            if (groundRend != null) groundRend.sharedMaterial = kitSidewalkMaterial;
        }

        CityRoot = cityRoot.transform;
        RoadGridCellSize = kitCellSize;
        RoadGridHalfExtent = half;
        RoadGraph = BuildRoadGraph(n, suppressV, suppressH);

        DrawRoadGrid(cityRoot.transform, half, kitCellSize, n, suppressV, suppressH);

        // PASS 3: instantiate the actual content for every planned block.
        foreach (var b in blocks)
        {
            switch (b.kind)
            {
                case BlockKind.Landmark:
                    {
                        int minC = int.MaxValue, maxC = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
                        foreach (var c in b.cells)
                        {
                            minC = Mathf.Min(minC, c.x); maxC = Mathf.Max(maxC, c.x);
                            minR = Mathf.Min(minR, c.y); maxR = Mathf.Max(maxR, c.y);
                        }
                        PlaceOne(cityRoot.transform, PickRandom(kitLandmarkPrefabs), half, kitCellSize,
                            minC, minR, maxC - minC + 1, maxR - minR + 1, true);
                    }
                    break;

                case BlockKind.MultiLot:
                    // One regular building per cell inside the merged lot - guarantees
                    // 2 or more buildings sharing the same open, road-free lot.
                    foreach (var c in b.cells)
                    {
                        PlaceOne(cityRoot.transform, PickRandom(kitBuildingPrefabs), half, kitCellSize,
                            c.x, c.y, 1, 1, true);
                    }
                    break;

                case BlockKind.Single:
                    PlaceSingleBuildingCluster(cityRoot.transform, half, kitCellSize, b.cells[0].x, b.cells[0].y);
                    break;

                case BlockKind.Prop:
                    PlaceOne(cityRoot.transform, PickRandom(kitPropPrefabs), half, kitCellSize,
                        b.cells[0].x, b.cells[0].y, 1, 1, false);
                    break;
            }
        }

        // Random overall rotation around the giant's spawn point, for a fresh layout each run.
        float randomYaw = Random.Range(0f, 360f);
        cityRoot.transform.RotateAround(transform.position, Vector3.up, randomYaw);

        ApplyRandomDaySkybox();
    }

    // Picks a random skybox from skyboxMaterials, restricted to ones whose name reads as
    // daytime ("Sunny"/"Overcast") - the pack also ships night/dusk skyboxes (Eerie,
    // MoonShine, StarryNight, DawnDusk) which we don't want to randomly land on.
    void ApplyRandomDaySkybox()
    {
        if (skyboxMaterials == null || skyboxMaterials.Length == 0) return;

        var dayOnes = new List<Material>();
        foreach (var m in skyboxMaterials)
        {
            if (m == null) continue;
            string nm = m.name.ToLowerInvariant();
            if (nm.Contains("sunny") || nm.Contains("overcast")) dayOnes.Add(m);
        }

        Material[] pool = dayOnes.Count > 0 ? dayOnes.ToArray() : skyboxMaterials;
        RenderSettings.skybox = pool[Random.Range(0, pool.Length)];
        DynamicGI.UpdateEnvironment();
    }

    // Returns the colSpan x rowSpan rectangle of cells starting at (i, j) as a flat list.
    List<Vector2Int> RectCells(int i, int j, int colSpan, int rowSpan)
    {
        var cells = new List<Vector2Int>();
        for (int dc = 0; dc < colSpan; dc++)
        {
            for (int dr = 0; dr < rowSpan; dr++)
            {
                cells.Add(new Vector2Int(i + dc, j + dr));
            }
        }
        return cells;
    }

    // True if every one of the given cells is inside the grid and not already claimed
    // by another block.
    bool CanFit(bool[,] occupied, int n, List<Vector2Int> cells)
    {
        foreach (var c in cells)
        {
            if (c.x < 0 || c.x >= n || c.y < 0 || c.y >= n) return false;
            if (occupied[c.x, c.y]) return false;
        }
        return true;
    }

    void AddBlock(List<PlannedBlock> blocks, bool[,] occupied, List<Vector2Int> cells, BlockKind kind)
    {
        blocks.Add(new PlannedBlock { cells = cells, kind = kind });
        foreach (var c in cells) occupied[c.x, c.y] = true;
    }

    // Tries a handful of shapes anchored at (i, j) - a horizontal pair, a vertical pair,
    // one of four L-trominoes (ㄱ/ㄴ shaped, 3 cells out of a 2x2 box), or a full 2x2 quad
    // - in random order, and commits the first one that fits as a "multi lot": an open
    // block that gets 2 or more regular buildings clustered together with no road
    // between them. Straight pairs are weighted to come up more often than the 2x2 quad;
    // L-shapes sit in between.
    bool TryPlaceMultiLot(bool[,] occupied, int n, int i, int j, List<PlannedBlock> blocks)
    {
        var horiz = new List<Vector2Int> { new Vector2Int(i, j), new Vector2Int(i + 1, j) };
        var vert = new List<Vector2Int> { new Vector2Int(i, j), new Vector2Int(i, j + 1) };
        var quad = new List<Vector2Int> { new Vector2Int(i, j), new Vector2Int(i + 1, j), new Vector2Int(i, j + 1), new Vector2Int(i + 1, j + 1) };
        var lNoTR = new List<Vector2Int> { new Vector2Int(i, j), new Vector2Int(i + 1, j), new Vector2Int(i, j + 1) };
        var lNoBR = new List<Vector2Int> { new Vector2Int(i, j), new Vector2Int(i + 1, j), new Vector2Int(i + 1, j + 1) };
        var lNoTL = new List<Vector2Int> { new Vector2Int(i, j), new Vector2Int(i, j + 1), new Vector2Int(i + 1, j + 1) };
        var lNoBL = new List<Vector2Int> { new Vector2Int(i + 1, j), new Vector2Int(i, j + 1), new Vector2Int(i + 1, j + 1) };

        var candidates = new List<List<Vector2Int>> { horiz, horiz, horiz, vert, vert, vert, lNoTR, lNoBR, lNoTL, lNoBL, quad };

        for (int k = candidates.Count - 1; k > 0; k--)
        {
            int r = Random.Range(0, k + 1);
            (candidates[k], candidates[r]) = (candidates[r], candidates[k]);
        }

        foreach (var cells in candidates)
        {
            if (CanFit(occupied, n, cells))
            {
                AddBlock(blocks, occupied, cells, BlockKind.MultiLot);
                return true;
            }
        }
        return false;
    }

    // Places one regular building into cell (c0, r0). If the prefab picked for it turns
    // out to be small (its own footprint well under the cell size), 1-2 more small
    // buildings are clustered alongside it in the same cell's other corners, instead of
    // leaving a mostly-empty single-building lot. Skipped if the cell is inside the
    // giant's clear zone.
    void PlaceSingleBuildingCluster(Transform parent, float half, float cell, int c0, int r0)
    {
        float centerX = (c0 + 0.5f) * cell - half;
        float centerZ = (r0 + 0.5f) * cell - half;
        if (new Vector2(centerX, centerZ).magnitude < clearRadius) return;

        float smallThresh = cell * 0.4f;
        GameObject prefab = PickBuildingBySize(smallThresh, out bool isSmall, 4);
        if (prefab == null) return;

        if (!isSmall)
        {
            float slack = Mathf.Max(0f, cell * 0.5f - 3f);
            float jitterX = Mathf.Clamp(Random.Range(-jitter, jitter), -slack, slack);
            float jitterZ = Mathf.Clamp(Random.Range(-jitter, jitter), -slack, slack);
            Quaternion rot = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
            GameObject inst = Instantiate(prefab, new Vector3(centerX + jitterX, 0f, centerZ + jitterZ), rot);
            inst.transform.SetParent(parent, true);
            AddSolidCollider(inst);
            return;
        }

        // Small building: cluster 2 or 3 of them into this one cell's corners so the lot
        // doesn't sit mostly empty.
        int count = Random.Range(2, 4);
        var corners = new List<Vector2> { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
        for (int k = corners.Count - 1; k > 0; k--)
        {
            int r = Random.Range(0, k + 1);
            (corners[k], corners[r]) = (corners[r], corners[k]);
        }

        float q = cell * 0.22f;
        for (int k = 0; k < count; k++)
        {
            GameObject p = (k == 0) ? prefab : PickBuildingBySize(smallThresh, out _, 4);
            if (p == null) continue;

            Vector2 dir = corners[k % corners.Count];
            float px = centerX + dir.x * q + Random.Range(-1.5f, 1.5f);
            float pz = centerZ + dir.y * q + Random.Range(-1.5f, 1.5f);

            Quaternion rot = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
            GameObject inst = Instantiate(p, new Vector3(px, 0f, pz), rot);
            inst.transform.SetParent(parent, true);
            AddSolidCollider(inst);
        }
    }

    // Picks a random prefab from kitBuildingPrefabs, measuring its real footprint to
    // report whether it's "small" (under sizeThresh on both axes). Tries a few times to
    // find a small one; if it can't, returns the last prefab tried anyway with isSmall=false.
    GameObject PickBuildingBySize(float sizeThresh, out bool isSmall, int maxTries)
    {
        GameObject last = null;
        Vector3 stagingPos = new Vector3(0f, -5000f, 0f);

        for (int t = 0; t < maxTries; t++)
        {
            GameObject cand = PickRandom(kitBuildingPrefabs);
            if (cand == null) { isSmall = false; return null; }
            last = cand;

            GameObject probe = Instantiate(cand, stagingPos, Quaternion.identity);
            Vector2 fp = MeasureFootprintXZ(probe);
            Destroy(probe);

            if (Mathf.Max(fp.x, fp.y) <= sizeThresh)
            {
                isSmall = true;
                return cand;
            }
        }

        isSmall = false;
        return last;
    }

    // Measures a temporarily-instantiated object's world-space X/Z footprint from its
    // renderers' combined bounds.
    Vector2 MeasureFootprintXZ(GameObject inst)
    {
        Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Vector2(kitCellSize, kitCellSize);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return new Vector2(b.size.x, b.size.z);
    }

    // Instantiates one prefab into the interior of the given colSpan x rowSpan cell
    // range, jittered but clamped so it never crosses out into the surrounding road.
    void PlaceOne(Transform parent, GameObject prefab, float half, float cell, int c0, int r0, int colSpan, int rowSpan, bool isBuilding)
    {
        if (prefab == null) return;

        float centerX = (c0 + colSpan * 0.5f) * cell - half;
        float centerZ = (r0 + rowSpan * 0.5f) * cell - half;

        if (new Vector2(centerX, centerZ).magnitude < clearRadius) return;

        float slack = Mathf.Max(0f, cell * 0.5f - 3f);
        float jitterX = Mathf.Clamp(Random.Range(-jitter, jitter), -slack, slack);
        float jitterZ = Mathf.Clamp(Random.Range(-jitter, jitter), -slack, slack);

        Quaternion rot = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
        GameObject inst = Instantiate(prefab, new Vector3(centerX + jitterX, 0f, centerZ + jitterZ), rot);
        inst.transform.SetParent(parent, true);

        if (isBuilding) AddSolidCollider(inst);
    }

    // Draws one road strip per grid-line segment (one segment per cell along each of
    // the n+1 lines in both directions), skipping any segment marked in suppressV /
    // suppressH - that's how a merged lot ends up with an open interior and no road
    // running through it, while the rest of the grid stays fully connected. A tiny Y
    // offset between the X-direction and Z-direction strips avoids z-fighting flicker
    // where they cross.
    // Turns the same suppressV/suppressH lookups DrawRoadGrid uses into a bidirectional
    // adjacency graph of grid intersections, so traffic AI can walk the real road network
    // (see RoadGraph's own doc comment above).
    Dictionary<Vector2Int, List<Vector2Int>> BuildRoadGraph(int n, bool[,] suppressV, bool[,] suppressH)
    {
        var graph = new Dictionary<Vector2Int, List<Vector2Int>>();
        System.Action<Vector2Int, Vector2Int> addEdge = (a, b) =>
        {
            if (!graph.TryGetValue(a, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>();
                graph[a] = list;
            }
            list.Add(b);
        };

        // Vertical segments (run along Z): column i, row j connects intersections (i,j) and (i,j+1).
        for (int i = 0; i <= n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (suppressV[i, j]) continue;
                var a = new Vector2Int(i, j);
                var b = new Vector2Int(i, j + 1);
                addEdge(a, b);
                addEdge(b, a);
            }
        }

        // Horizontal segments (run along X): row j, column i connects intersections (i,j) and (i+1,j).
        for (int j = 0; j <= n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                if (suppressH[j, i]) continue;
                var a = new Vector2Int(i, j);
                var b = new Vector2Int(i + 1, j);
                addEdge(a, b);
                addEdge(b, a);
            }
        }

        return graph;
    }

    void DrawRoadGrid(Transform parent, float half, float cell, int n, bool[,] suppressV, bool[,] suppressH)
    {
        if (kitGroundMaterial == null && kitRoadLineMaterial == null) return;

        float roadY = 0.02f;
        float roadYAlt = 0.021f;
        float lineWidth = 1.4f;
        float lineY = 0.045f;

        // Vertical lines (run along Z), one per column boundary i = 0..n.
        for (int i = 0; i <= n; i++)
        {
            float x = i * cell - half;

            for (int j = 0; j < n; j++)
            {
                if (suppressV[i, j]) continue;

                float z0 = j * cell - half;
                float zc = z0 + cell * 0.5f;

                if (kitGroundMaterial != null)
                {
                    GameObject roadX = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    roadX.name = "RoadStrip";
                    Collider rxCol = roadX.GetComponent<Collider>();
                    if (rxCol != null) Destroy(rxCol);
                    roadX.transform.SetParent(parent, false);
                    roadX.transform.localPosition = new Vector3(x, roadY, zc);
                    roadX.transform.localScale = new Vector3(kitRoadWidth, 0.04f, cell);
                    roadX.GetComponent<Renderer>().sharedMaterial = kitGroundMaterial;
                }
                if (kitRoadLineMaterial != null)
                {
                    GameObject lineX = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lineX.name = "RoadLine";
                    Collider lxCol = lineX.GetComponent<Collider>();
                    if (lxCol != null) Destroy(lxCol);
                    lineX.transform.SetParent(parent, false);
                    lineX.transform.localPosition = new Vector3(x, lineY, zc);
                    lineX.transform.localScale = new Vector3(lineWidth, 0.02f, cell);
                    lineX.GetComponent<Renderer>().sharedMaterial = kitRoadLineMaterial;
                }
            }
        }

        // Horizontal lines (run along X), one per row boundary j = 0..n.
        for (int j = 0; j <= n; j++)
        {
            float z = j * cell - half;

            for (int i = 0; i < n; i++)
            {
                if (suppressH[j, i]) continue;

                float x0 = i * cell - half;
                float xc = x0 + cell * 0.5f;

                if (kitGroundMaterial != null)
                {
                    GameObject roadZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    roadZ.name = "RoadStrip";
                    Collider rzCol = roadZ.GetComponent<Collider>();
                    if (rzCol != null) Destroy(rzCol);
                    roadZ.transform.SetParent(parent, false);
                    roadZ.transform.localPosition = new Vector3(xc, roadYAlt, z);
                    roadZ.transform.localScale = new Vector3(cell, 0.04f, kitRoadWidth);
                    roadZ.GetComponent<Renderer>().sharedMaterial = kitGroundMaterial;
                }
                if (kitRoadLineMaterial != null)
                {
                    GameObject lineZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lineZ.name = "RoadLine";
                    Collider lzCol = lineZ.GetComponent<Collider>();
                    if (lzCol != null) Destroy(lzCol);
                    lineZ.transform.SetParent(parent, false);
                    lineZ.transform.localPosition = new Vector3(xc, lineY + 0.001f, z);
                    lineZ.transform.localScale = new Vector3(cell, 0.02f, lineWidth);
                    lineZ.GetComponent<Renderer>().sharedMaterial = kitRoadLineMaterial;
                }
            }
        }
    }

    // The Toon City Pack's building/landmark prefabs ship with no colliders at all, so
    // the giant and tiny people would otherwise walk straight through them. This adds a
    // BoxCollider sized to the instance's combined renderer bounds. Because every
    // instance is only ever rotated in 90-degree steps (see the Quaternion.Euler calls
    // above), the world-space axis-aligned bounds are still an accurate, unskewed box,
    // so converting them into local space here gives a properly fitted collider.
    void AddSolidCollider(GameObject inst)
    {
        Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 lossyScale = inst.transform.lossyScale;
        Vector3 localSize = new Vector3(
            lossyScale.x != 0f ? worldBounds.size.x / lossyScale.x : worldBounds.size.x,
            lossyScale.y != 0f ? worldBounds.size.y / lossyScale.y : worldBounds.size.y,
            lossyScale.z != 0f ? worldBounds.size.z / lossyScale.z : worldBounds.size.z
        );

        BoxCollider box = inst.AddComponent<BoxCollider>();
        box.center = inst.transform.InverseTransformPoint(worldBounds.center);
        box.size = localSize;

        // Tagged so tiny NPCs and enemy vehicles (BuildingCollision.cs) - and the giant's
        // own smash-damage hit check (GiantController.OnControllerColliderHit) - can tell
        // this apart from everything else (roads have no collider at all; other NPCs/
        // vehicles are untagged) and treat it as solid, unlike untagged geometry.
        inst.tag = "Building";
    }

    GameObject PickRandom(GameObject[] arr)
    {
        if (arr == null || arr.Length == 0) return null;
        return arr[Random.Range(0, arr.Length)];
    }

    void GenerateBuildings()
    {
        float half = gridSize * spacing * 0.5f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                float px = x * spacing - half + Random.Range(-jitter, jitter);
                float pz = z * spacing - half + Random.Range(-jitter, jitter);

                if (new Vector2(px, pz).magnitude < clearRadius) continue;

                float height = Random.Range(minHeight, maxHeight);
                float width = Random.Range(minWidth, maxWidth);

                GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
                building.name = "Building";
                building.tag = "Building";
                building.transform.position = new Vector3(px, height * 0.5f, pz);
                building.transform.localScale = new Vector3(width, height, width);
                building.transform.SetParent(transform);

                Renderer rend = building.GetComponent<Renderer>();
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = BuildingPalette[Random.Range(0, BuildingPalette.Length)];
                rend.material = mat;

                building.AddComponent<Rigidbody>();
                building.AddComponent<BuildingHealth>();
            }
        }
    }

    void GenerateTinyPeople()
    {
        float half = gridSize * spacing * 0.5f;

        for (int i = 0; i < tinyCount; i++)
        {
            float px = Random.Range(-half, half);
            float pz = Random.Range(-half, half);
            SpawnOneTinyPerson(px, pz);
        }
    }

    // One citizen at the given XZ position -- extracted out of GenerateTinyPeople so the
    // auto-respawn top-up loop below (TinyRespawnLoop) can spawn individual replacements
    // without destroying/rebuilding the whole population like RespawnTinyPeople does.
    void SpawnOneTinyPerson(float px, float pz)
    {
        if (tinyPersonPrefab != null)
        {
            GameObject body = Instantiate(tinyPersonPrefab);
            body.name = "TinyPerson";
            body.tag = "Tiny";
            body.transform.SetParent(transform);
            body.transform.localScale = Vector3.one * tinyScale;
            body.transform.position = new Vector3(px, 0f, pz);

            Animator anim = body.GetComponent<Animator>();
            if (anim == null) anim = body.GetComponentInChildren<Animator>();
            if (anim != null && tinyAnimatorController != null)
            {
                anim.runtimeAnimatorController = tinyAnimatorController;
            }

            CapsuleCollider col = body.GetComponent<CapsuleCollider>();
            if (col == null) col = body.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.3f;
            // Trigger, not solid: citizens should never physically block the giant's
            // movement like a building would, and the giant must never be able to stand
            // on top of one. Grab/attack detection (Physics.OverlapSphere) and the
            // birdcage squash trigger (StompZone) both still work fine against a trigger
            // collider -- only CharacterController's own solid-collision resolution cares
            // about the trigger flag, and that's exactly what we want to disable here.
            col.isTrigger = true;

            TinyNPC npc = body.AddComponent<TinyNPC>();
            npc.animator = anim;
        }
        else
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "TinyPerson";
            body.tag = "Tiny";
            body.transform.position = new Vector3(px, tinyScale, pz);
            body.transform.localScale = Vector3.one * tinyScale;
            body.transform.SetParent(transform);

            Renderer rend = body.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = TinyPalette[Random.Range(0, TinyPalette.Length)];
            rend.material = mat;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(body.transform);
            head.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            head.transform.localScale = Vector3.one * 0.8f;
            Destroy(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().material = mat;

            // Same reasoning as the tinyPersonPrefab branch above: this fallback capsule
            // ships with its own CapsuleCollider by default, which must also be a trigger
            // so it doesn't block/support the giant's movement like a building.
            CapsuleCollider fallbackCol = body.GetComponent<CapsuleCollider>();
            if (fallbackCol != null) fallbackCol.isTrigger = true;

            body.AddComponent<TinyNPC>();
        }
    }

    // Periodically tops the citizen population back up toward tinyCount, so the city doesn't
    // stay empty for the rest of a long run after the giant has squashed/killed people --
    // started once in Start() and just keeps running for the lifetime of the scene (works
    // fine across RegenerateFromSeed too, since it only looks at live "Tiny"-tagged children
    // under this transform each cycle rather than caching anything).
    IEnumerator TinyRespawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(tinyRespawnInterval);
            TopUpTinyPeople();
        }
    }

    void TopUpTinyPeople()
    {
        int alive = 0;
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Tiny")) alive++;
        }

        int deficit = Mathf.Max(0, tinyCount - alive);
        int toSpawn = Mathf.Max(deficit, tinyRespawnMinPerCycle);

        float half = gridSize * spacing * 0.5f;
        for (int i = 0; i < toSpawn; i++)
        {
            float px = Random.Range(-half, half);
            float pz = Random.Range(-half, half);
            SpawnOneTinyPerson(px, pz);
        }
    }

    // Destroys every currently-spawned tiny person and spawns a fresh batch.
    public void RespawnTinyPeople()
    {
        var toRemove = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Tiny"))
            {
                toRemove.Add(child.gameObject);
            }
        }
        foreach (var go in toRemove)
        {
            Destroy(go);
        }

        GenerateTinyPeople();
    }
}
