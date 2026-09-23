using UnityEngine;

// Shared helper so tiny NPCs and enemy vehicles can't walk/drive/fly straight through
// buildings. Their movement is plain transform translation (no Rigidbody or
// CharacterController), which never collides with anything on its own — this is what
// actually stops them. Used by TinyNPC, TinySoldierAI, PoliceCarAI, TankAI, HelicopterAI.
public static class BuildingCollision
{
    // Buildings get this tag from CityGenerator.AddSolidCollider(). Only objects with this
    // tag block movement here — other NPCs/vehicles, the ground, and road strips (which have
    // no collider at all) are all passed through freely, so crowds don't gridlock on each other.
    public const string BuildingTag = "Building";

    // Reused across every call instead of allocating a new array each time (this runs once
    // per moving NPC/vehicle per logic tick, so avoiding GC pressure at scale matters).
    static readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    // Returns a movement vector clamped so it never crosses into a building: if the desired
    // move would hit one, the returned vector stops a small skin-width short of it instead of
    // covering the full distance. Otherwise returns desiredMove unchanged.
    // `origin` is the mover's current world position; `heightOffset` raises the cast origin to
    // roughly torso/body height (0 for something already flying at its own altitude, like a
    // helicopter). `radius` should roughly match the mover's own footprint so it doesn't clip
    // through a building corner.
    public static Vector3 ClampAgainstBuildings(Vector3 origin, Vector3 desiredMove, float radius, float heightOffset)
    {
        float distance = desiredMove.magnitude;
        if (distance < 0.0001f) return desiredMove;

        Vector3 dir = desiredMove / distance;
        Vector3 castOrigin = origin + Vector3.up * heightOffset;

        int count = Physics.SphereCastNonAlloc(castOrigin, radius, dir, hitBuffer, distance);
        float closest = distance;
        bool blocked = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null || !hit.collider.CompareTag(BuildingTag)) continue;
            if (hit.distance < closest)
            {
                closest = hit.distance;
                blocked = true;
            }
        }

        if (!blocked) return desiredMove;

        float safeDistance = Mathf.Max(0f, closest - 0.1f);
        return dir * safeDistance;
    }

    // Same building-blocking test as ClampAgainstBuildings, but instead of simply freezing the
    // mover the instant it grazes a wall, redirects whatever distance the wall ate into a slide
    // along the wall's surface (its tangent plane). Used for knockback specifically: a shove that
    // happens to point roughly at a building would otherwise register a near-zero blocked distance
    // and look like the hit did nothing at all, especially for vehicles (their larger radius makes
    // them far more likely to already be grazing a nearby building when hit). A push aimed dead-on
    // at a flat wall still correctly produces no sideways motion -- that matches real physics -- but
    // any push at an angle now visibly slides the target along the wall instead of freezing solid.
    public static Vector3 ClampAgainstBuildingsSliding(Vector3 origin, Vector3 desiredMove, float radius, float heightOffset)
    {
        float distance = desiredMove.magnitude;
        if (distance < 0.0001f) return desiredMove;

        Vector3 dir = desiredMove / distance;
        Vector3 castOrigin = origin + Vector3.up * heightOffset;

        int count = Physics.SphereCastNonAlloc(castOrigin, radius, dir, hitBuffer, distance);
        float closest = distance;
        bool blocked = false;
        Vector3 hitNormal = Vector3.up;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null || !hit.collider.CompareTag(BuildingTag)) continue;
            if (hit.distance < closest)
            {
                closest = hit.distance;
                blocked = true;
                hitNormal = hit.normal;
            }
        }

        if (!blocked) return desiredMove;

        float safeDistance = Mathf.Max(0f, closest - 0.1f);
        Vector3 safeMove = dir * safeDistance;

        float usedFraction = distance > 0.0001f ? Mathf.Clamp01(safeDistance / distance) : 1f;
        Vector3 remaining = desiredMove * (1f - usedFraction);
        Vector3 slideRemaining = Vector3.ProjectOnPlane(remaining, hitNormal);

        // One extra check so the sideways slide itself can't punch through a second wall
        // (e.g. a corner) -- reuses the plain clamp, which is a safe (if slightly conservative)
        // bound for this secondary, usually much shorter, movement.
        slideRemaining = ClampAgainstBuildings(origin + safeMove, slideRemaining, radius, heightOffset);

        return safeMove + slideRemaining;
    }
}
