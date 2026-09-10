using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GiantController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float rotateSpeed = 10f;
    public float gravity = -20f;
    public float jumpHeight = 2f;

    [Header("Building Smash")]
    public float smashDamagePerSecond = 60f;

    [Header("Animation")]
    public Animator animator;
    public float animSpeedDamping = 8f;

    private CharacterController controller;
    private Vector3 velocity;
    private Transform cam;
    private float currentAnimSpeed;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main != null ? Camera.main.transform : null;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.magnitude > 1f) inputDir.Normalize();

        float targetAnimSpeed = 0f;

        if (inputDir.magnitude >= 0.1f)
        {
            float camYaw = cam != null ? cam.eulerAngles.y : 0f;
            Vector3 moveDir = Quaternion.Euler(0f, camYaw, 0f) * inputDir;

            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);

            controller.Move(moveDir * moveSpeed * Time.deltaTime);
            targetAnimSpeed = inputDir.magnitude;
        }

        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetAnimSpeed, animSpeedDamping * Time.deltaTime);
        if (animator != null)
        {
            animator.SetFloat("Speed", currentAnimSpeed);
        }

        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
            if (Input.GetButtonDown("Jump"))
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Building"))
        {
            BuildingHealth building = hit.gameObject.GetComponent<BuildingHealth>();
            if (building != null)
            {
                building.TakeDamage(smashDamagePerSecond * Time.deltaTime, hit.moveDirection);
            }
        }
    }
}
