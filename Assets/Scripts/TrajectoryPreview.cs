using UnityEngine;

// Draws a live preview of a thrown object's predicted flight path while the giant is aiming --
// a plain line curving down under gravity exactly like ThrowArcRoutine's real physics, down to
// where it lands on the ground, with a single arrowhead at the tip showing the direction of
// travel -- plus a small flat ring marking the landing spot. Owned by GiantController, which
// creates one instance via Create() and calls Show()/Hide() every frame while aiming.
public class TrajectoryPreview : MonoBehaviour
{
    LineRenderer line;
    LineRenderer arrowHead;
    GameObject landingRing;

    static Material lineMaterial;

    // How close the arc actually has to pass to a vehicle/person's own collider to count as
    // "the line touches it" for highlight purposes -- deliberately small (roughly the drawn
    // line's own visual thickness) and unrelated to throwImpactRadius, which is the much more
    // forgiving radius the REAL throw's hit detection uses. Using that same forgiving radius
    // here used to light objects up just for being near the arc, not on it.
    const float touchRadius = 0.3f;

    public static TrajectoryPreview Create()
    {
        GameObject go = new GameObject("TrajectoryPreview");
        TrajectoryPreview preview = go.AddComponent<TrajectoryPreview>();
        preview.Init();
        return preview;
    }

    void Init()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.material = GetLineMaterial();
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 4;
        line.widthMultiplier = 0.35f;
        line.startColor = new Color(1f, 0.95f, 0.35f, 0.95f);
        line.endColor = new Color(1f, 0.55f, 0.12f, 0.95f);
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // A single chevron drawn only at the tip of the line, instead of the old texture tiled
        // with little arrows along the whole length -- see UpdateArrowHead.
        GameObject arrowObj = new GameObject("TrajectoryArrowHead");
        arrowObj.transform.SetParent(transform, false);
        arrowHead = arrowObj.AddComponent<LineRenderer>();
        arrowHead.material = GetLineMaterial();
        arrowHead.textureMode = LineTextureMode.Stretch;
        arrowHead.numCapVertices = 2;
        arrowHead.widthMultiplier = 0.4f;
        arrowHead.startColor = new Color(1f, 0.55f, 0.12f, 0.95f);
        arrowHead.endColor = new Color(1f, 0.55f, 0.12f, 0.95f);
        arrowHead.useWorldSpace = true;
        arrowHead.positionCount = 0;
        arrowHead.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arrowHead.receiveShadows = false;

        landingRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        landingRing.name = "TrajectoryLandingMarker";
        Destroy(landingRing.GetComponent<Collider>());
        landingRing.transform.SetParent(transform, false);
        landingRing.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
        Renderer ringRend = landingRing.GetComponent<Renderer>();
        Material ringMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ringMat.color = new Color(1f, 0.85f, 0.25f);
        ringMat.EnableKeyword("_EMISSION");
        ringMat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.15f) * 1.5f);
        ringRend.material = ringMat;
        ringRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        gameObject.SetActive(false);
    }

    static Material GetLineMaterial()
    {
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        return lineMaterial;
    }

    // Recomputes and shows the predicted arc from launchOrigin along launchDir at throwSpeed,
    // under throwGravity -- the exact same step-by-step gravity integration GiantController's
    // real ThrowArcRoutine uses, so the preview always matches where a throw released right now
    // would actually go -- sampled until it reaches the ground (y <= 0) or maxSteps is hit (a
    // safety cap well beyond any throw this game's speed/gravity values produce). Along the way,
    // it sweeps each segment of the arc against vehicle/person colliders (not just sample
    // points, so a fast segment can't tunnel past a target between checks) and hands the first
    // one the line actually touches to ThrowHighlight so it glows orange -- excludeRoot/heldObj
    // keep the giant itself and the held object out of that scan, same exclusions ComputeAimPoint
    // applies for the aim raycast.
    public void Show(Vector3 launchOrigin, Vector3 launchDir, float throwSpeed, float throwGravity, Transform excludeRoot, Transform heldObj)
    {
        gameObject.SetActive(true);

        const float dt = 0.05f;
        const int maxSteps = 250;

        Vector3 velocity = launchDir * throwSpeed;
        Vector3 pos = launchOrigin;

        var points = new System.Collections.Generic.List<Vector3>(maxSteps + 1);
        points.Add(pos);

        Transform hitTarget = null;

        for (int i = 0; i < maxSteps; i++)
        {
            Vector3 prevPos = pos;
            velocity += Vector3.down * throwGravity * dt;
            pos += velocity * dt;

            if (hitTarget == null)
            {
                hitTarget = ResolveHitAlongSegment(prevPos, pos, excludeRoot, heldObj);
            }

            if (pos.y <= 0f)
            {
                // Interpolate the exact ground-crossing point so the line (and marker) end
                // cleanly at y = 0 instead of overshooting into the ground by one step.
                float segT = prevPos.y / (prevPos.y - pos.y);
                pos = Vector3.Lerp(prevPos, pos, segT);
                points.Add(pos);
                break;
            }

            points.Add(pos);
        }

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());

        Vector3 landingPoint = points[points.Count - 1];
        landingRing.transform.position = landingPoint + Vector3.up * 0.05f;

        UpdateArrowHead(points);

        ThrowHighlight.SetTarget(hitTarget);
    }

    // Draws a single chevron ("v") at the very tip of the line, in the vertical plane containing
    // the final segment's direction of travel, so it reads as one arrowhead pointing the way the
    // thrown object is actually heading -- instead of the old tiled-arrow-texture look running
    // the whole length of the line.
    void UpdateArrowHead(System.Collections.Generic.List<Vector3> points)
    {
        if (points.Count < 2)
        {
            arrowHead.positionCount = 0;
            return;
        }

        Vector3 tip = points[points.Count - 1];
        Vector3 forwardAxis = (tip - points[points.Count - 2]).normalized;

        Vector3 upAxis = Vector3.ProjectOnPlane(Vector3.up, forwardAxis);
        if (upAxis.sqrMagnitude < 0.0001f)
        {
            upAxis = Vector3.ProjectOnPlane(Vector3.forward, forwardAxis);
        }
        upAxis.Normalize();

        const float arrowLength = 2.2f;
        const float arrowWidth = 1.1f;
        Vector3 backTop = tip - forwardAxis * arrowLength + upAxis * arrowWidth;
        Vector3 backBottom = tip - forwardAxis * arrowLength - upAxis * arrowWidth;

        arrowHead.positionCount = 3;
        arrowHead.SetPositions(new Vector3[] { backTop, tip, backBottom });
    }

    // Sweeps a thin capsule (radius touchRadius) along one arc segment and returns the first
    // vehicle/person collider it actually touches -- same target-type check GiantController's
    // own FindThrowHitAt uses. Checking the whole segment (not just its endpoints) matters here:
    // at the speeds this game throws things, consecutive sample points can be several units
    // apart, far enough for a thin sweep to slip past a target's collider between them if only
    // the points themselves were tested.
    static Transform ResolveHitAlongSegment(Vector3 from, Vector3 to, Transform excludeRoot, Transform heldObj)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.0001f)
        {
            return null;
        }
        Vector3 dir = delta / dist;

        RaycastHit[] hits = Physics.SphereCastAll(from, touchRadius, dir, dist);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.transform;
            if (excludeRoot != null && (t == excludeRoot || t.IsChildOf(excludeRoot)))
            {
                continue;
            }
            if (heldObj != null && (t == heldObj || t.IsChildOf(heldObj)))
            {
                continue;
            }

            TrafficCarAI traffic = t.GetComponentInParent<TrafficCarAI>();
            if (traffic != null) return traffic.transform;
            PoliceCarAI police = t.GetComponentInParent<PoliceCarAI>();
            if (police != null) return police.transform;
            TankAI tank = t.GetComponentInParent<TankAI>();
            if (tank != null) return tank.transform;
            HelicopterAI heli = t.GetComponentInParent<HelicopterAI>();
            if (heli != null) return heli.transform;
            TinyNPC npc = t.GetComponentInParent<TinyNPC>();
            if (npc != null) return npc.transform;
            TinySoldierAI soldier = t.GetComponentInParent<TinySoldierAI>();
            if (soldier != null) return soldier.transform;
        }
        return null;
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
        ThrowHighlight.Clear();
    }
}
