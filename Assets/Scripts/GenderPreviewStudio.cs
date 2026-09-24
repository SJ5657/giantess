using UnityEngine;
using UnityEngine.UI;

// Drives the two character previews on the gender-select screen. Each preview is a separate
// copy of the character standing in a tiny studio parked far above the city; a dedicated camera
// renders it into a RenderTexture that the card's RawImage shows. Everything is created on
// enable and released on disable, so nothing runs (or costs anything) once the game has started.
//
// The game is frozen (Time.timeScale = 0) while this screen is up, so the preview Animators are
// set to unscaled time and the gentle sway below uses unscaledTime as well.
public class GenderPreviewStudio : MonoBehaviour
{
    [Tooltip("Root of the far-away preview studio (models, cameras, lights). Kept inactive except while this panel is open.")]
    public GameObject studioRoot;

    [Header("Female")]
    public Camera femaleCamera;
    public RawImage femaleImage;
    public Transform femaleModel;

    [Header("Male")]
    public Camera maleCamera;
    public RawImage maleImage;
    public Transform maleModel;

    [Header("Look")]
    public int textureWidth = 448;
    public int textureHeight = 512;
    [Tooltip("How far (degrees) each model slowly turns left and right.")]
    public float swayAngle = 18f;
    [Tooltip("Sway speed in radians per second.")]
    public float swaySpeed = 0.7f;

    RenderTexture femaleTexture;
    RenderTexture maleTexture;
    float femaleBaseYaw;
    float maleBaseYaw;
    bool baseYawCaptured;

    void OnEnable()
    {
        femaleTexture = CreateTexture(femaleCamera, femaleImage);
        maleTexture = CreateTexture(maleCamera, maleImage);

        if (!baseYawCaptured)
        {
            if (femaleModel != null) femaleBaseYaw = femaleModel.localEulerAngles.y;
            if (maleModel != null) maleBaseYaw = maleModel.localEulerAngles.y;
            baseYawCaptured = true;
        }

        if (studioRoot != null)
        {
            studioRoot.SetActive(true);
        }
    }

    void OnDisable()
    {
        if (studioRoot != null)
        {
            studioRoot.SetActive(false);
        }

        ReleaseTexture(ref femaleTexture, femaleCamera, femaleImage);
        ReleaseTexture(ref maleTexture, maleCamera, maleImage);
    }

    void Update()
    {
        float sway = Mathf.Sin(Time.unscaledTime * swaySpeed) * swayAngle;
        if (femaleModel != null) SetYaw(femaleModel, femaleBaseYaw + sway);
        // Opposite phase so the two characters don't move in lockstep.
        if (maleModel != null) SetYaw(maleModel, maleBaseYaw - sway);
    }

    RenderTexture CreateTexture(Camera cam, RawImage image)
    {
        RenderTexture rt = new RenderTexture(textureWidth, textureHeight, 24);
        rt.name = "GenderPreviewRT";
        if (cam != null) cam.targetTexture = rt;
        if (image != null) image.texture = rt;
        return rt;
    }

    static void ReleaseTexture(ref RenderTexture rt, Camera cam, RawImage image)
    {
        if (cam != null) cam.targetTexture = null;
        if (image != null) image.texture = null;
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    static void SetYaw(Transform t, float yaw)
    {
        Vector3 e = t.localEulerAngles;
        e.y = yaw;
        t.localEulerAngles = e;
    }
}
