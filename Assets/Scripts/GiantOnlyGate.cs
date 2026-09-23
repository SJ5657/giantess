using UnityEngine;

// Attached to the gate collider in the giant's home wall. The gate is otherwise a normal
// solid "Building"-tagged obstacle, so TinyNPC/TinySoldierAI/vehicle AI treat it exactly like
// any wall (BuildingCollision's SphereCast avoidance blocks them same as always). This script
// carves out the one exception: it tells physics to ignore collisions between the gate and
// every collider on the giant, so the giant alone can walk straight through while everyone
// else stays physically blocked.
public class GiantOnlyGate : MonoBehaviour
{
    void Start()
    {
        Collider gateCollider = GetComponent<Collider>();
        if (gateCollider == null) return;

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj == null) return;

        // CharacterController IS a Collider subclass, and this also covers any other collider
        // hanging off the giant (e.g. the StompZone hitbox child) so nothing on the giant's
        // hierarchy can snag on the gate.
        Collider[] giantColliders = giantObj.GetComponentsInChildren<Collider>();
        foreach (Collider c in giantColliders)
        {
            if (c == null) continue;
            Physics.IgnoreCollision(gateCollider, c, true);
        }
    }
}
