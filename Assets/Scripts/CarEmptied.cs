using System.Collections;
using UnityEngine;

// Marks a car whose driver the giant has already eaten (see GiantController.CarEatRoutine).
// Once the giant lets go of such a car it stays parked where it lands -- its driving AI stays
// off. If it landed on a road it disappears after a while; anywhere else (sidewalk, lot, etc.)
// it just stays put. It never produces another passenger.
public class CarEmptied : MonoBehaviour
{
    [Tooltip("Seconds an abandoned car stays on the ground before it disappears (only if it is on a road).")]
    public float disappearAfter = 20f;
    [Tooltip("Seconds its colliders stay off after being put down (so landing at the giant's feet can't instantly stomp it).")]
    public float colliderDelay = 1.5f;

    public void BeginAbandoned()
    {
        // Keep every driving/AI script off so the car just sits there.
        TrafficCarAI traffic = GetComponent<TrafficCarAI>();
        if (traffic != null) traffic.enabled = false;
        PoliceCarAI police = GetComponent<PoliceCarAI>();
        if (police != null) police.enabled = false;

        StopAllCoroutines();
        StartCoroutine(AbandonedRoutine());
    }

    IEnumerator AbandonedRoutine()
    {
        yield return new WaitForSeconds(colliderDelay);
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = true;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, disappearAfter - colliderDelay));

        // Only cars sitting on a road vanish; one that landed off the road stays where it is.
        if (IsOnRoad())
        {
            Destroy(gameObject);
        }
    }

    // True if this car's position is within road width of any road segment of the city's road
    // graph (the same graph traffic drives along). If there is no city/road data, counts as
    // on-road so the car still disappears.
    bool IsOnRoad()
    {
        CityGenerator city = FindObjectOfType<CityGenerator>();
        if (city == null || city.RoadGraph == null || city.CityRoot == null) return true;

        Vector3 lp = city.CityRoot.InverseTransformPoint(transform.position);
        Vector2 p = new Vector2(lp.x, lp.z);
        float maxDist = city.kitRoadWidth * 0.5f + 0.75f;

        foreach (var kv in city.RoadGraph)
        {
            Vector3 a3 = city.GridToLocal(kv.Key);
            Vector2 a = new Vector2(a3.x, a3.z);
            foreach (var n in kv.Value)
            {
                Vector3 b3 = city.GridToLocal(n);
                Vector2 b = new Vector2(b3.x, b3.z);
                if (DistanceToSegment(p, a, b) <= maxDist) return true;
            }
        }
        return false;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
