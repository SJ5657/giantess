using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    [Header("Spawn")]
    public float spawnDistance = 60f;

    [Header("Tank")]
    public float tankMoveSpeed = 4f;
    public float tankAttackRange = 35f;
    public float tankFireInterval = 1.5f;
    public float tankShellDamage = 22f;
    public float tankShellSpeed = 30f;
    public float tankShellSpread = 1.8f;
    [Tooltip("Imported tank body models (from the Lowpoly Military Armored Army Vehicles Strategy Assets Pack) -- one is picked at random each time a tank spawns. Left empty, SpawnTank() falls back to the original procedurally-built box-and-cylinder tank.")]
    public GameObject[] tankModelPrefabs;
    [Tooltip("Uniform scale applied to a picked tankModelPrefabs entry so the imported model reads at roughly the same size as the original procedural tank.")]
    public float tankModelScale = 1.6f;

    [Header("Helicopter")]
    public float heliMoveSpeed = 8f;
    public float heliAttackRange = 30f;
    public float heliHoverHeight = 7f;
    public float heliFireRate = 10f;
    public float heliBulletDamage = 1f;
    [Tooltip("Imported helicopter body models (from the Lowpoly Military Armored Army Vehicles Strategy Assets Pack) -- one is picked at random each time a helicopter spawns. Left empty, SpawnHelicopter() falls back to the original procedurally-built box-and-cylinder helicopter.")]
    public GameObject[] helicopterModelPrefabs;
    [Tooltip("Uniform scale applied to a picked helicopterModelPrefabs entry so the imported model reads at roughly the same size as the original procedural helicopter.")]
    public float helicopterModelScale = 1.15f;

    [Header("Police")]
    public GameObject officerBodyPrefab;
    public RuntimeAnimatorController officerAnimatorController;
    public GameObject officerWeaponPrefab;
    public float officerScale = 0.45f;
    public float officerAttackRange = 14f;
    public float officerFireInterval = 1.4f;
    public float officerShotDamage = 4f;
    public float officerShotSpread = 0.9f;
    public float policeCarMoveSpeed = 10f;
    public float policeDisembarkRange = 22f;
    public float policeReboardRange = 34f;

    static Material tracerMat;

    public static Material GetTracerMaterial()
    {
        if (tracerMat == null)
        {
            tracerMat = new Material(Shader.Find("Sprites/Default"));
        }
        return tracerMat;
    }

    Vector3 GetSpawnPosition()
    {
        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        Vector3 center = giantObj != null ? giantObj.transform.position : Vector3.zero;

        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 offset = new Vector3(dir.x, 0f, dir.y) * spawnDistance;
        return center + offset;
    }

    public void SpawnTank()
    {
        Vector3 pos = GetSpawnPosition();

        if (tankModelPrefabs != null && tankModelPrefabs.Length > 0)
        {
            SpawnImportedTank(pos);
            return;
        }

        GameObject tank = new GameObject("Tank");
        tank.transform.position = new Vector3(pos.x, 0.2f, pos.z);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(tank.transform, false);
        body.transform.localScale = new Vector3(1.32f, 0.39f, 1.98f);
        body.transform.localPosition = Vector3.zero;
        SetColor(body, new Color(0.22f, 0.28f, 0.18f));

        GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cube);
        turret.name = "Turret";
        turret.transform.SetParent(tank.transform, false);
        turret.transform.localScale = new Vector3(0.66f, 0.3f, 0.72f);
        turret.transform.localPosition = new Vector3(0f, 0.35f, -0.1f);
        SetColor(turret, new Color(0.18f, 0.24f, 0.15f));

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Barrel";
        barrel.transform.SetParent(turret.transform, false);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localScale = new Vector3(0.1f, 0.54f, 0.1f);
        barrel.transform.localPosition = new Vector3(0f, 0.03f, 0.54f);
        SetColor(barrel, new Color(0.12f, 0.12f, 0.12f));
        Destroy(barrel.GetComponent<Collider>());

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(barrel.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 1f, 0f);

        TankAI ai = tank.AddComponent<TankAI>();
        ai.moveSpeed = tankMoveSpeed;
        ai.attackRange = tankAttackRange;
        ai.fireInterval = tankFireInterval;
        ai.shellDamage = tankShellDamage;
        ai.shellSpeed = tankShellSpeed;
        ai.shellSpread = tankShellSpread;
        ai.muzzle = muzzle.transform;
        ai.barrel = barrel.transform;
    }

    public void SpawnHelicopter()
    {
        Vector3 pos = GetSpawnPosition();

        if (helicopterModelPrefabs != null && helicopterModelPrefabs.Length > 0)
        {
            SpawnImportedHelicopter(pos);
            return;
        }

        GameObject heli = new GameObject("Helicopter");
        heli.transform.position = new Vector3(pos.x, heliHoverHeight, pos.z);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(heli.transform, false);
        body.transform.localScale = new Vector3(0.6f, 0.54f, 1.32f);
        body.transform.localPosition = Vector3.zero;
        SetColor(body, new Color(0.25f, 0.27f, 0.24f));

        GameObject tailBoom = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tailBoom.name = "TailBoom";
        tailBoom.transform.SetParent(heli.transform, false);
        tailBoom.transform.localScale = new Vector3(0.132f, 0.132f, 0.99f);
        tailBoom.transform.localPosition = new Vector3(0f, 0.066f, -0.99f);
        SetColor(tailBoom, new Color(0.22f, 0.24f, 0.21f));

        GameObject rotorPivot = new GameObject("MainRotorPivot");
        rotorPivot.transform.SetParent(heli.transform, false);
        rotorPivot.transform.localPosition = new Vector3(0f, 0.36f, 0f);
        rotorPivot.AddComponent<RotorSpin>().spinSpeed = 900f;

        GameObject mainRotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainRotor.name = "MainRotorBlade";
        mainRotor.transform.SetParent(rotorPivot.transform, false);
        mainRotor.transform.localScale = new Vector3(2.28f, 0.03f, 0.114f);
        SetColor(mainRotor, new Color(0.08f, 0.08f, 0.08f));
        Destroy(mainRotor.GetComponent<Collider>());

        GameObject tailRotorPivot = new GameObject("TailRotorPivot");
        tailRotorPivot.transform.SetParent(tailBoom.transform, false);
        tailRotorPivot.transform.localPosition = new Vector3(0f, 0f, -0.528f);
        RotorSpin tailSpin = tailRotorPivot.AddComponent<RotorSpin>();
        tailSpin.spinSpeed = 1400f;

        GameObject tailRotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tailRotor.name = "TailRotorBlade";
        tailRotor.transform.SetParent(tailRotorPivot.transform, false);
        tailRotor.transform.localScale = new Vector3(0.024f, 0.54f, 0.048f);
        SetColor(tailRotor, new Color(0.08f, 0.08f, 0.08f));
        Destroy(tailRotor.GetComponent<Collider>());

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(body.transform, false);
        muzzle.transform.localPosition = new Vector3(0.21f, -0.12f, 0.33f);

        HelicopterAI ai = heli.AddComponent<HelicopterAI>();
        ai.moveSpeed = heliMoveSpeed;
        ai.attackRange = heliAttackRange;
        ai.hoverHeight = heliHoverHeight;
        ai.fireRate = heliFireRate;
        ai.bulletDamage = heliBulletDamage;
        ai.muzzle = muzzle.transform;
    }

    // Instantiates a random imported tank model (tankModelPrefabs), fits it with a generic
    // bounding-box collider and a Muzzle point, and wires up TankAI exactly like the
    // procedural path above. Recoil animation is skipped for these (TankAI.barrel is left
    // null, which it already handles gracefully as "no recoil animation" rather than an
    // error) since these detailed imported models have no single named "barrel" child to
    // animate the way the procedural tank's does. The pack's models all use local +Z as
    // forward (confirmed by their gun barrels/tail booms sitting toward positive/negative Z
    // respectively), matching Unity's own forward convention, so no rotation correction is
    // needed for TankAI's own transform.forward-based aiming/movement to work correctly.
    void SpawnImportedTank(Vector3 pos)
    {
        GameObject prefab = tankModelPrefabs[Random.Range(0, tankModelPrefabs.Length)];
        if (prefab == null) return;

        GameObject tank = Instantiate(prefab);
        tank.name = "Tank";

        Bounds bounds = ComputeCombinedLocalBounds(tank);

        BoxCollider col = tank.AddComponent<BoxCollider>();
        col.center = bounds.center;
        col.size = bounds.size;

        tank.transform.localScale = Vector3.one * tankModelScale;
        // Ground the model regardless of where each individual prefab's own pivot happens to
        // sit (some FBX exports put it at the base, some at the geometric center) by measuring
        // its own lowest point and lifting exactly that far off the ground.
        float groundY = -bounds.min.y * tankModelScale + 0.05f;
        tank.transform.position = new Vector3(pos.x, groundY, pos.z);

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(tank.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, bounds.center.y + bounds.extents.y * 0.2f, bounds.max.z);

        TankAI ai = tank.AddComponent<TankAI>();
        ai.moveSpeed = tankMoveSpeed;
        ai.attackRange = tankAttackRange;
        ai.fireInterval = tankFireInterval;
        ai.shellDamage = tankShellDamage;
        ai.shellSpeed = tankShellSpeed;
        ai.shellSpread = tankShellSpread;
        ai.muzzle = muzzle.transform;
    }

    // Same idea as SpawnImportedTank -- picks a random imported helicopter model, fits a
    // bounding-box collider, adds a Muzzle point, and spins whichever child reads as the main
    // rotor (the widest part of the model along local X, matching a rotor disc's span --
    // confirmed on the pack's own helicopters, where the true main rotor was by far the
    // widest single child) via the same RotorSpin component the procedural helicopter uses.
    void SpawnImportedHelicopter(Vector3 pos)
    {
        GameObject prefab = helicopterModelPrefabs[Random.Range(0, helicopterModelPrefabs.Length)];
        if (prefab == null) return;

        GameObject heli = Instantiate(prefab);
        heli.name = "Helicopter";

        Bounds bounds = ComputeCombinedLocalBounds(heli);

        BoxCollider col = heli.AddComponent<BoxCollider>();
        col.center = bounds.center;
        col.size = bounds.size;

        Transform rotor = FindWidestXChild(heli);
        if (rotor != null)
        {
            rotor.gameObject.AddComponent<RotorSpin>().spinSpeed = 900f;
        }

        heli.transform.localScale = Vector3.one * helicopterModelScale;
        heli.transform.position = new Vector3(pos.x, heliHoverHeight, pos.z);

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(heli.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, bounds.center.y, bounds.max.z);

        HelicopterAI ai = heli.AddComponent<HelicopterAI>();
        ai.moveSpeed = heliMoveSpeed;
        ai.attackRange = heliAttackRange;
        ai.hoverHeight = heliHoverHeight;
        ai.fireRate = heliFireRate;
        ai.bulletDamage = heliBulletDamage;
        ai.muzzle = muzzle.transform;
    }

    // World-space renderer bounds, combined across every mesh in the model and expressed back
    // in the root's own local space -- valid because this is always called right after
    // Instantiate(), before the root has been moved, rotated or scaled away from the identity
    // transform it starts at, so world space and the root's local space are still identical.
    Bounds ComputeCombinedLocalBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }
        return combined;
    }

    // Finds the direct child whose renderer bounds are widest along local X -- for these
    // vehicle models that's reliably the main rotor disc (it spans far wider than any other
    // single part), so this doubles as "find the main rotor" without depending on any
    // particular child name, which differs per imported model/variant.
    Transform FindWidestXChild(GameObject root)
    {
        Transform best = null;
        float bestWidth = -1f;
        foreach (Transform child in root.transform)
        {
            Renderer r = child.GetComponent<Renderer>();
            if (r == null) continue;
            if (r.bounds.size.x > bestWidth)
            {
                bestWidth = r.bounds.size.x;
                best = child;
            }
        }
        return best;
    }

    public void SpawnPolice()
    {
        if (officerBodyPrefab == null) return;

        Vector3 pos = GetSpawnPosition();

        GameObject car = new GameObject("PoliceCar");
        car.transform.position = new Vector3(pos.x, 0.21f, pos.z);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(car.transform, false);
        body.transform.localScale = new Vector3(0.66f, 0.33f, 1.44f);
        body.transform.localPosition = Vector3.zero;
        SetColor(body, Color.white);

        GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        cabin.transform.SetParent(car.transform, false);
        cabin.transform.localScale = new Vector3(0.6f, 0.24f, 0.66f);
        cabin.transform.localPosition = new Vector3(0f, 0.27f, -0.06f);
        SetColor(cabin, new Color(0.08f, 0.1f, 0.15f));

        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "Stripe";
        stripe.transform.SetParent(car.transform, false);
        stripe.transform.localScale = new Vector3(0.672f, 0.108f, 1.452f);
        stripe.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        SetColor(stripe, new Color(0.05f, 0.15f, 0.55f));
        Destroy(stripe.GetComponent<Collider>());

        GameObject lightBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightBar.name = "LightBar";
        lightBar.transform.SetParent(car.transform, false);
        lightBar.transform.localScale = new Vector3(0.42f, 0.072f, 0.18f);
        lightBar.transform.localPosition = new Vector3(0f, 0.408f, -0.06f);
        Destroy(lightBar.GetComponent<Collider>());
        lightBar.AddComponent<PoliceLightFlash>();

        Vector3[] wheelOffsets = new Vector3[]
        {
            new Vector3(0.348f, -0.18f, 0.48f),
            new Vector3(-0.348f, -0.18f, 0.48f),
            new Vector3(0.348f, -0.18f, -0.48f),
            new Vector3(-0.348f, -0.18f, -0.48f)
        };
        foreach (Vector3 offset in wheelOffsets)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            wheel.transform.SetParent(car.transform, false);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.168f, 0.072f, 0.168f);
            wheel.transform.localPosition = offset;
            SetColor(wheel, new Color(0.05f, 0.05f, 0.05f));
            Destroy(wheel.GetComponent<Collider>());
        }

        GameObject seatL = new GameObject("SeatL");
        seatL.transform.SetParent(car.transform, false);
        seatL.transform.localPosition = new Vector3(-0.96f, -0.21f, 0.18f);

        GameObject seatR = new GameObject("SeatR");
        seatR.transform.SetParent(car.transform, false);
        seatR.transform.localPosition = new Vector3(0.96f, -0.21f, 0.18f);

        TinySoldierAI officer1 = CreateOfficer(car.transform);
        TinySoldierAI officer2 = CreateOfficer(car.transform);

        PoliceCarAI carAi = car.AddComponent<PoliceCarAI>();
        carAi.moveSpeed = policeCarMoveSpeed;
        carAi.disembarkRange = policeDisembarkRange;
        carAi.reboardRange = policeReboardRange;
        carAi.officers = new TinySoldierAI[] { officer1, officer2 };
        carAi.officerSeats = new Transform[] { seatL.transform, seatR.transform };
    }

    TinySoldierAI CreateOfficer(Transform carTransform)
    {
        GameObject body = Instantiate(officerBodyPrefab, carTransform);
        body.name = "PoliceOfficer";
        body.tag = "Tiny";
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one * officerScale;

        Animator anim = body.GetComponent<Animator>();
        if (anim == null) anim = body.GetComponentInChildren<Animator>();
        if (anim != null && officerAnimatorController != null)
        {
            anim.runtimeAnimatorController = officerAnimatorController;
        }

        CapsuleCollider col = body.GetComponent<CapsuleCollider>();
        if (col == null) col = body.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 1f, 0f);
        col.height = 2f;
        col.radius = 0.3f;
        // Same reasoning as CityGenerator.GenerateTinyPeople: an officer must not physically
        // block the giant's movement or act as standable ground, so its collider is a trigger.
        // Grab/attack OverlapSphere checks and the StompZone squash trigger are unaffected.
        col.isTrigger = true;

        Transform muzzleT = null;
        if (anim != null && officerWeaponPrefab != null)
        {
            Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null)
            {
                GameObject weapon = Instantiate(officerWeaponPrefab, hand);
                weapon.name = "Pistol";
                weapon.transform.localPosition = new Vector3(0f, -0.03f, 0.05f);
                weapon.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                weapon.transform.localScale = Vector3.one;

                GameObject muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(weapon.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 0f, 0.22f);
                muzzleT = muzzle.transform;
            }
        }

        TinySoldierAI ai = body.AddComponent<TinySoldierAI>();
        ai.animator = anim;
        ai.muzzle = muzzleT;
        ai.attackRange = officerAttackRange;
        ai.fireInterval = officerFireInterval;
        ai.shotDamage = officerShotDamage;
        ai.shotSpread = officerShotSpread;

        body.SetActive(false);
        return ai;
    }

    static void SetColor(GameObject go, Color color)
    {
        Renderer rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }
}
