using UnityEngine;
using System.Collections.Generic;

// Populates the city's actual road network (CityGenerator.RoadGraph) with a handful of
// low-poly cars that continuously drive around the grid, turning at intersections -- pure
// ambient background traffic. CityGenerator itself calls RespawnAll() right after it finishes
// generating (see CityGenerator.GenerateWorld), both for the very first city and every later
// RegenerateFromSeed/Load, so this always matches whatever road layout currently exists.
public class TrafficCarSpawner : MonoBehaviour
{
    [Header("References")]
    public CityGenerator cityGenerator;

    [Header("Cars")]
    [Tooltip("Car models driven around the road network -- one is picked at random for each spawned car.")]
    public GameObject[] carPrefabs;
    [Tooltip("How many cars to keep driving around the city at once.")]
    public int carCount = 12;
    [Tooltip("Uniform scale applied to every spawned car -- the pack's models come in at roughly real-world car size, which reads too big for these narrow toy-grid streets.")]
    public float carScale = 0.4f;
    [Tooltip("Minimum driving speed (units/sec).")]
    public float minSpeed = 5f;
    [Tooltip("Maximum driving speed (units/sec) -- picked per car (between min and max) so traffic doesn't all move in lockstep.")]
    public float maxSpeed = 9f;

    [Header("Lanes")]
    [Tooltip("Sideways distance each car drives from the road's centerline -- lets two cars share one road going opposite directions (each on its own side) instead of driving down the middle and overlapping. Half of kitRoadWidth is the road's edge, so keep this comfortably less than that once the car's own half-width is added.")]
    public float laneOffset = 1.1f;

    readonly List<GameObject> spawned = new List<GameObject>();

    void Start()
    {
        if (cityGenerator == null) cityGenerator = FindObjectOfType<CityGenerator>();
    }

    // Destroys whatever traffic currently exists (old cars parented under a previous,
    // already-destroyed city root are simply skipped -- see the null check below) and spawns
    // a fresh fleet distributed across the current road graph.
    public void RespawnAll()
    {
        foreach (var go in spawned)
        {
            if (go != null) DestroyImmediate(go);
        }
        spawned.Clear();

        if (cityGenerator == null) cityGenerator = FindObjectOfType<CityGenerator>();
        if (cityGenerator == null || carPrefabs == null || carPrefabs.Length == 0) return;
        if (cityGenerator.RoadGraph == null || cityGenerator.RoadGraph.Count == 0) return;
        if (cityGenerator.CityRoot == null) return;

        var nodes = new List<Vector2Int>(cityGenerator.RoadGraph.Keys);

        for (int i = 0; i < carCount; i++)
        {
            SpawnOne(nodes);
        }
    }

    void SpawnOne(List<Vector2Int> nodes)
    {
        // A handful of tries to land on an intersection that actually has a connected road
        // (true for essentially every node in this grid, but kept defensive).
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2Int start = nodes[Random.Range(0, nodes.Count)];
            List<Vector2Int> neighbors = cityGenerator.RoadGraph[start];
            if (neighbors == null || neighbors.Count == 0) continue;

            Vector2Int target = neighbors[Random.Range(0, neighbors.Count)];

            GameObject prefab = carPrefabs[Random.Range(0, carPrefabs.Length)];
            if (prefab == null) continue;

            GameObject car = Instantiate(prefab, cityGenerator.CityRoot);
            car.name = "TrafficCar";
            car.transform.localScale = Vector3.one * carScale;

            // Never physically block the giant's CharacterController -- same reasoning as
            // TinyNPC/police officers: solid collision here would make cars act like invisible
            // walls the giant keeps bumping into while walking down a street.
            Collider col = car.GetComponentInChildren<Collider>();
            if (col != null) col.isTrigger = true;

            TrafficCarAI ai = car.AddComponent<TrafficCarAI>();
            ai.Init(cityGenerator, start, target, Random.Range(minSpeed, maxSpeed), laneOffset);

            spawned.Add(car);
            return;
        }
    }
}
