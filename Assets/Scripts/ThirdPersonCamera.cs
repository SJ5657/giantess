using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public static ThirdPersonCamera Instance { get; private set; }

    public Transform target;
    public Vector3 offset = new Vector3(0f, 4f, -10f);
    public float mouseSensitivity = 3f;
    public float smoothSpeed = 10f;
    public float minPitch = -20f;
    public float maxPitch = 60f;
    [Tooltip("How far up the camera can look while aiming (right-click), overriding minPitch -- " +
        "more negative = further up. Separate from minPitch so normal free-look stays as before, " +
        "while aiming lets the player tilt up enough to target things overhead like helicopters.")]
    public float aimMinPitch = -60f;

    [Header("Aim / Shoulder View")]
    [Tooltip("Offset used instead of 'offset' above while the giant is aiming (right-click held " +
        "with something in hand) -- pulled in closer and shifted to one side for an over-the-shoulder " +
        "view. Same coordinate space as 'offset' (rotated by the current yaw/pitch), so a positive X " +
        "shifts the camera to the giant's right.")]
    public Vector3 aimOffset = new Vector3(3.8f, 9.5f, -6.5f);
    [Tooltip("Camera field of view while aiming (lower = more zoomed in). Only applied if this " +
        "object has a Camera component.")]
    public float aimFieldOfView = 42f;
    [Tooltip("How quickly the field of view blends between its normal value and aimFieldOfView. " +
        "The offset itself reuses the existing position smoothing (smoothSpeed) rather than a " +
        "separate blend, since desiredPos is already lerped toward every frame.")]
    public float aimFovBlendSpeed = 8f;

    [Header("Shake")]
    [Tooltip("How quickly accumulated shake settles back down. Proportional/exponential decay " +
        "(trauma *= e^-decay*dt), not a flat per-second subtraction: a flat subtraction eats a " +
        "small trauma value (e.g. a footstep's 0.07-0.1) in under one frame, so it never actually " +
        "renders, while a big pulse (a punch's 0.6) is unaffected and still reads fine. Proportional " +
        "decay makes small and large shakes last the same relative number of frames.")]
    public float shakeDecay = 20f;
    [Tooltip("Max rotational kick (degrees) applied to the camera at full shake strength. " +
        "Rotation, not a position offset, because LookAt re-centers the target regardless of " +
        "where the camera itself sits, which makes a pure position shake nearly invisible.")]
    public float shakeMagnitude = 6f;

    private float yaw;
    private float pitch = 15f;
    private float trauma;
    private bool isAiming;
    private Camera cachedCamera;
    private float normalFieldOfView;

    void Awake()
    {
        Instance = this;
        cachedCamera = GetComponent<Camera>();
    }

    void Start()
    {
        // Don't force-lock the cursor while the start screen (or, by the same logic,
        // any future pre-game state GameFlowManager owns) is still up — it needs the
        // cursor free so the player can click its buttons. GameFlowManager.Awake() runs
        // before this Start() (all Awakes run before any Start), so its HasGameStarted
        // value is already correct here. No GameFlowManager in the scene at all falls
        // back to the old always-lock behavior.
        if (GameFlowManager.Instance == null || GameFlowManager.Instance.HasGameStarted)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (cachedCamera != null)
        {
            normalFieldOfView = cachedCamera.fieldOfView;
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            SnapToTarget();
        }
    }

    void Update()
    {
        // Escape (opening/closing the object manager popup) is handled by EscMenu,
        // which also owns the cursor lock state while the popup is open.
        if (Cursor.lockState != CursorLockMode.Locked) return;

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        float effectiveMinPitch = isAiming ? aimMinPitch : minPitch;
        pitch = Mathf.Clamp(pitch, effectiveMinPitch, maxPitch);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 activeOffset = isAiming ? aimOffset : offset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = target.position + rot * activeOffset;

        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);

        if (isAiming)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, smoothSpeed * Time.deltaTime);
        }
        else
        {
            transform.LookAt(target.position + Vector3.up * (activeOffset.y * 0.6f));
        }

        if (cachedCamera != null)
        {
            float targetFov = isAiming ? aimFieldOfView : normalFieldOfView;
            cachedCamera.fieldOfView = Mathf.Lerp(cachedCamera.fieldOfView, targetFov, aimFovBlendSpeed * Time.deltaTime);
        }

        if (trauma > 0f)
        {
            // Rotational kick applied ON TOP of the LookAt aim, not a position offset: a position
            // offset alone is barely visible here because LookAt keeps re-centering on the target
            // no matter where the camera sits, so the whole view (including the giant) needs to
            // swing to actually read as a shake.
            float angle = shakeMagnitude * trauma;
            float rx = (Random.value * 2f - 1f) * angle;
            float ry = (Random.value * 2f - 1f) * angle;
            float rz = (Random.value * 2f - 1f) * angle * 0.5f;
            transform.rotation *= Quaternion.Euler(rx, ry, rz);

            trauma = DecayTrauma(trauma, shakeDecay, Time.deltaTime);
        }
    }

    // Extracted as a pure function so the decay curve itself is directly testable
    // (Time.deltaTime can't be controlled from outside Play mode).
    static float DecayTrauma(float currentTrauma, float decayRate, float deltaTime)
    {
        float next = currentTrauma * Mathf.Exp(-decayRate * deltaTime);
        return next < 0.001f ? 0f : next;
    }

    void SnapToTarget()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target.position + rot * offset;
        transform.LookAt(target.position + Vector3.up * (offset.y * 0.6f));
    }

    // Adds a small burst of screen shake (0-1 range, clamped/accumulated). Called e.g. by
    // StompZone whenever the giant stomps a tiny person.
    public void AddShake(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    // Called every frame by GiantController while right-click aim mode is active/inactive (see
    // GiantController.TickAttackInput and ThrowHeldTarget). Blends the camera toward a closer,
    // shoulder-offset view and a narrower FOV while true, and back to the normal follow view
    // while false.
    public void SetAiming(bool value)
    {
        isAiming = value;
    }

    // Current up/down look angle (degrees), read by GiantController to drive a matching
    // backward torso lean on the throw animation when aiming steeply upward.
    public float Pitch => pitch;
}
