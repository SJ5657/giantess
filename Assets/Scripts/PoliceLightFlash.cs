using UnityEngine;

// Alternates a light bar cube between red and blue emissive colors, police-siren style.
public class PoliceLightFlash : MonoBehaviour
{
    public float flashRate = 6f;
    public Color colorA = Color.red;
    public Color colorB = Color.blue;

    private Renderer rend;
    private Material mat;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.EnableKeyword("_EMISSION");
            rend.material = mat;
        }
    }

    void Update()
    {
        if (mat == null) return;
        bool useA = Mathf.FloorToInt(Time.time * flashRate) % 2 == 0;
        Color c = useA ? colorA : colorB;
        mat.color = c;
        mat.SetColor("_EmissionColor", c * 3f);
    }
}
