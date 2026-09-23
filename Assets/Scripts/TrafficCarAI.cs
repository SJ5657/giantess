using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Drives one traffic car along the city's actual road-intersection graph (see
// CityGenerator.RoadGraph), turning onto a random connected road at every intersection it
// reaches (avoiding an immediate U-turn back the way it came, unless that's the only option --
// a dead end). Pure ambient background traffic: no combat, no physics -- moved directly via
// transform each frame, and parented under the city's own root (see TrafficCarSpawner) so it
// automatically stays correctly placed/oriented even though the whole city gets a random Y
// rotation applied once at generation time.
//
// Two-lane driving: instead of following the road's exact centerline, each car is offset
// sideways from it by laneOffset, to whichever side is "right of travel" for its CURRENT
// direction (see ComputeLaneOffset). Because that offset flips sign for the opposite travel
// direction, two cars can share the very same road segment, heading opposite ways, without
// overlapping -- exactly like a real one-lane-each-way street. The offset is recomputed every
// time the car picks a new segment (Init/AdvanceToNextNode), since a turn can change its
// direction of travel.
public class TrafficCarAI : MonoBehaviour
{
    public CityGenerator cityGen;
    public float moveSpeed = 6f;
    public float turnDegreesPerSecond = 180f;
    [Tooltip("Sideways distance from the road's centerline this car drives at, so two cars can share one road going opposite directions without overlapping.")]
    public float laneOffset = 1.1f;
    [Tooltip("How close the giant has to get, while directly ahead of this car, before it notices and stops driving. It resumes on its own, from wherever it stopped, once the giant either moves back out past this range or is no longer in front.")]
    public float giantSightRange = 12f;
    [Tooltip("How narrow the forward-facing cone is: 1 = dead ahead only, 0 = a full 180-degree arc to either side. Compared against the dot product of this car's forward direction and the direction to the giant.")]
    [Range(-1f, 1f)]
    public float giantSightForwardDot = 0.75f;

    [Header("Drop (grabbed & released by the giant)")]
    [Tooltip("Downward speed (units/sec) this car falls the rest of the way to the road once the giant lets go of it, instead of snapping straight down.")]
    public float dropFallSpeed = 4f;
    [Tooltip("How long (seconds) this car sits stunned in place once it lands from a drop, before driving off again.")]
    public float dropStunDuration = 1.5f;

    Vector2Int currentNode;
    Vector2Int targetNode;
    Vector3 currentLaneOffsetVec;
    bool initialized;
    bool isDestroyed;
    bool isDropping;
    float squashInvulnerableUntil;
    Transform giant;

    public void Init(CityGenerator generator, Vector2Int startNode, Vector2Int firstTarget, float speed, float lane)
    {
        cityGen = generator;
        currentNode = startNode;
        targetNode = firstTarget;
        moveSpeed = speed;
        laneOffset = lane;

        Vector3 dir = (cityGen.GridToLocal(targetNode) - cityGen.GridToLocal(currentNode)).normalized;
        currentLaneOffsetVec = ComputeLaneOffset(dir);

        transform.localPosition = cityGen.GridToLocal(currentNode) + currentLaneOffsetVec;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        initialized = true;
    }

    void Update()
    {
        if (!initialized || isDestroyed || isDropping || cityGen == null || cityGen.RoadGraph == null) return;

        if (giant == null)
        {
            GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
            if (giantObj != null) giant = giantObj.transform;
        }

        // Spotted the giant -- but only when it's actually ahead of the car (within the
        // forward-facing cone below), not just nearby in any direction -- stop dead where it
        // is (rather than driving obliviously toward danger) for as long as that stays true.
        // It just picks its route back up on its own once the giant moves out of range or out
        // of view, no separate "resume" timer needed.
        if (giant != null)
        {
            Vector3 toGiant = giant.position - transform.position;
            float distToGiant = toGiant.magnitude;
            if (distToGiant <= giantSightRange && distToGiant > 0.0001f)
            {
                float facing = Vector3.Dot(transform.forward, toGiant / distToGiant);
                if (facing >= giantSightForwardDot)
                {
                    return;
                }
            }
        }

        Vector3 targetLocalPos = cityGen.GridToLocal(targetNode) + currentLaneOffsetVec;
        Vector3 toTarget = targetLocalPos - transform.localPosition;
        float dist = toTarget.magnitude;
        float step = moveSpeed * Time.deltaTime;

        if (dist <= step)
        {
            transform.localPosition = targetLocalPos;
            AdvanceToNextNode();
            return;
        }

        Vector3 dir = toTarget / dist;
        transform.localPosition += dir * step;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRot, turnDegreesPerSecond * Time.deltaTime);
    }

    // Picks the next intersection to head to now that targetNode has been reached, preferring
    // any connected road other than the one just arrived from (so the car keeps driving
    // through instead of doing a U-turn at every single intersection) and falling back to a
    // U-turn only when that intersection truly is a dead end. Also recomputes the lane offset
    // for the new direction of travel.
    void AdvanceToNextNode()
    {
        Vector2Int previous = currentNode;
        currentNode = targetNode;

        if (!cityGen.RoadGraph.TryGetValue(currentNode, out List<Vector2Int> neighbors) || neighbors.Count == 0)
        {
            return; // stranded -- shouldn't normally happen, every drawn segment is a two-way edge
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        foreach (var n in neighbors)
        {
            if (n != previous) candidates.Add(n);
        }
        if (candidates.Count == 0) candidates = neighbors;

        targetNode = candidates[Random.Range(0, candidates.Count)];

        Vector3 dir = (cityGen.GridToLocal(targetNode) - cityGen.GridToLocal(currentNode)).normalized;
        currentLaneOffsetVec = ComputeLaneOffset(dir);
    }

    // The sideways offset -- perpendicular to travel direction -- that puts this car on the
    // "right-hand" side of the road's centerline for whichever way it's currently heading.
    // Cross(up, dir) flips sign when dir reverses, so opposite-direction traffic on the same
    // physical road strip always lands on opposite sides of the centerline.
    Vector3 ComputeLaneOffset(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return Vector3.zero;
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        return right * laneOffset;
    }

    // Called by GiantController.ReleaseHeldTarget right after letting go of this car: instead
    // of snapping straight back onto the road and immediately resuming, it gently falls the
    // rest of the way down, sits stunned for a moment, then drives off again. Colliders stay
    // disabled (GiantController left them off) for the whole sequence, so landing right at the
    // giant's feet can't trigger an immediate stomp-squash.
    public void BeginDrop()
    {
        isDropping = true;

        // Covers the fall + stun sequence below, plus a little extra buffer after colliders
        // come back on -- a stomp trigger overlapping the instant this car's collider
        // re-enables (e.g. the giant is still standing right where it dropped it) would
        // otherwise squash it the moment it lands, which defeats the whole point of a gentle,
        // survivable drop.
        float estimatedFallTime = transform.localPosition.y / Mathf.Max(dropFallSpeed, 0.01f);
        squashInvulnerableUntil = Time.time + estimatedFallTime + dropStunDuration + 0.75f;

        StartCoroutine(DropRoutine());
    }

    IEnumerator DropRoutine()
    {
        while (transform.localPosition.y > 0f)
        {
            float step = dropFallSpeed * Time.deltaTime;
            Vector3 pos = transform.localPosition;
            pos.y = Mathf.Max(0f, pos.y - step);
            transform.localPosition = pos;
            yield return null;
        }

        yield return new WaitForSeconds(dropStunDuration);

        Collider[] cols = GetComponentsInChildren<Collider>();
        foreach (Collider c in cols)
        {
            c.enabled = true;
        }

        isDropping = false;
    }

    // Instant-crush triggered by the giant's stomp (see StompZone) -- ambient traffic has no
    // health pool of its own, one stomp is always enough.
    public void Squash()
    {
        if (isDestroyed || Time.time < squashInvulnerableUntil) return;
        isDestroyed = true;

        Explosion.Spawn(transform.position + Vector3.up * 0.4f, 1f);
        Destroy(gameObject);
    }
}
