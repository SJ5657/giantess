using UnityEngine;

// Animates the giant's home gate swinging open as the giant approaches, and closing again
// once it walks away. Purely visual -- the actual blocking collider (GateBlocker, with
// GiantOnlyGate) never moves, so whatever still stops tiny people/vehicles is completely
// unaffected by this animation; it's just showmanship for the giant's own entrance.
public class GiantGateDoor : MonoBehaviour
{
    [HideInInspector] public Transform leftPivot;
    [HideInInspector] public Transform rightPivot;

    [Header("Door Animation")]
    [Tooltip("How far each leaf swings open, in degrees.")]
    public float openAngle = 100f;
    [Tooltip("Giant needs to be within this distance of the gate for it to start opening.")]
    public float openDistance = 26f;
    [Tooltip("Giant needs to move at least this far away before the gate starts closing again (kept larger than openDistance so it doesn't flicker right at the threshold).")]
    public float closeDistance = 34f;
    [Tooltip("How fast the doors swing, in degrees per second.")]
    public float swingSpeed = 90f;

    private Transform giant;
    private bool isOpen;

    void Start()
    {
        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null) giant = giantObj.transform;
    }

    void Update()
    {
        if (giant == null || leftPivot == null || rightPivot == null) return;

        float dist = Vector3.Distance(
            new Vector3(giant.position.x, 0f, giant.position.z),
            new Vector3(transform.position.x, 0f, transform.position.z));

        if (!isOpen && dist <= openDistance) isOpen = true;
        else if (isOpen && dist >= closeDistance) isOpen = false;

        float targetAngle = isOpen ? openAngle : 0f;
        Quaternion leftTarget = Quaternion.Euler(0f, -targetAngle, 0f);
        Quaternion rightTarget = Quaternion.Euler(0f, targetAngle, 0f);

        leftPivot.localRotation = Quaternion.RotateTowards(leftPivot.localRotation, leftTarget, swingSpeed * Time.deltaTime);
        rightPivot.localRotation = Quaternion.RotateTowards(rightPivot.localRotation, rightTarget, swingSpeed * Time.deltaTime);
    }
}
