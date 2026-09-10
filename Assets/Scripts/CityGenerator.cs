using UnityEngine;

public class CityGenerator : MonoBehaviour
{
    [Header("Grid")]
    public int gridSize = 8;
    public float spacing = 14f;
    public float jitter = 3f;
    public float clearRadius = 12f;

    [Header("Buildings")]
    public float minHeight = 3f;
    public float maxHeight = 16f;
    public float minWidth = 3f;
    public float maxWidth = 6f;

    [Header("Tiny People")]
    public int tinyCount = 60;
    public float tinyScale = 0.35f;

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

    void Start()
    {
        GenerateBuildings();
        GenerateTinyPeople();
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

            body.AddComponent<TinyNPC>();
        }
    }
}
