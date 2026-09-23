using UnityEngine;
using System.Collections;

// Scatters HP-restoring pickups at random points across the city and keeps that count topped
// up over time: when one is collected, a fresh one appears somewhere else after a short
// delay, so there's always a handful worth hunting down during a long run.
public class HealthPickupSpawner : MonoBehaviour
{
    [Header("Pickups")]
    [Tooltip("How many health pickups should exist in the world at once.")]
    public int pickupCount = 8;
    [Tooltip("HP restored by each pickup.")]
    public float healAmount = 60f;
    [Tooltip("Seconds to wait before a replacement pickup appears after one is collected.")]
    public float respawnDelay = 8f;

    [Header("Placement")]
    [Tooltip("Random spawn points are chosen within +/- this distance of the map center on both axes.")]
    public float placementHalfExtent = 95f;
    [Tooltip("Height above the ground the pickup is placed at.")]
    public float placementHeight = 0.6f;
    [Tooltip("How many random points to try before giving up and placing the pickup anyway (avoids buildings).")]
    public int maxPlacementAttempts = 30;
    [Tooltip("Radius used when checking a candidate spot is clear of buildings.")]
    public float clearanceRadius = 2f;

    void Start()
    {
        for (int i = 0; i < pickupCount; i++)
        {
            SpawnOne();
        }
    }

    void SpawnOne()
    {
        Vector3 pos = FindSpawnPosition();
        HealthPickup.Spawn(pos, healAmount, this);
    }

    // Tries a handful of random points and keeps the first one that isn't inside/overlapping a
    // building; falls back to the last candidate tried if none turned out clear, so a pickup
    // still always spawns rather than silently doing nothing.
    Vector3 FindSpawnPosition()
    {
        Vector3 lastCandidate = new Vector3(0f, placementHeight, 0f);

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float x = Random.Range(-placementHalfExtent, placementHalfExtent);
            float z = Random.Range(-placementHalfExtent, placementHalfExtent);
            Vector3 candidate = new Vector3(x, placementHeight, z);
            lastCandidate = candidate;

            if (IsClearOfBuildings(candidate))
            {
                return candidate;
            }
        }

        return lastCandidate;
    }

    bool IsClearOfBuildings(Vector3 point)
    {
        Collider[] hits = Physics.OverlapSphere(point, clearanceRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(BuildingCollision.BuildingTag)) return false;
        }
        return true;
    }

    // Called by a HealthPickup right before it destroys itself.
    public void NotifyCollected()
    {
        StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnOne();
    }
}
