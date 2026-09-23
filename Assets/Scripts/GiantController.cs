using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CharacterController))]
public class GiantController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;
    public float rotateSpeed = 10f;
    public float gravity = -20f;
    public float jumpHeight = 2f;

    [Header("Sprint")]
    public float sprintMultiplier = 2f;

    [Header("Building Smash")]
    public float smashDamagePerSecond = 60f;

    [Header("Animation")]
    public Animator animator;
    public float animSpeedDamping = 8f;

    [Header("Footstep Shake")]
    [Tooltip("Screen shake strength added per footstep while walking (0-1 range). Kept small/subtle.")]
    public float footstepShakeAmount = 0.1f;
    [Tooltip("Extra multiplier applied to footstep shake while sprinting.")]
    public float sprintShakeMultiplier = 1.1f;
    [Tooltip("Ground distance (units) the giant must actually cover, while grounded, before the next footstep shake fires. Distance-based (not a timer) so a shake never fires while airborne or standing still.")]
    public float footstepStride = 5f;

    [Header("Stamina")]
    [Tooltip("Maximum stamina.")]
    public float maxStamina = 100f;
    [Tooltip("Current stamina (read-only at runtime; starts at maxStamina).")]
    public float currentStamina;
    [Tooltip("Stamina drained per second while sprinting.")]
    public float staminaDrainPerSecondSprint = 20f;
    [Tooltip("Flat stamina cost paid once per punch, on top of any sprint drain.")]
    public float staminaCostPerAttack = 15f;
    [Tooltip("Stamina regenerated per second while walking normally or standing still (never while sprinting or mid-attack).")]
    public float staminaRegenPerSecond = 15f;
    [Tooltip("The gauge's fill Image, shown under the HP gauge. Its RectTransform width is driven " +
        "directly (left edge fixed, right edge moves) rather than relying on Image.fillAmount, " +
        "so the bar unambiguously shrinks toward the left and grows toward the right.")]
    public Image staminaFillImage;
    public TextMeshProUGUI staminaText;
    [Tooltip("Fill color while stamina is usable (the bar's normal yellow).")]
    public Color staminaNormalColor = new Color(0.95f, 0.85f, 0.15f, 1f);
    [Tooltip("Fill color while locked out from hitting 0 stamina, shown until it fully refills back to max.")]
    public Color staminaExhaustedColor = new Color(0.32f, 0.28f, 0.08f, 1f);

    private RectTransform staminaFillRect;
    private float staminaFillMaxWidth;

    [Header("Attack")]
    [Tooltip("Trigger parameter name on the Animator for the left-click punch (Giant@UnarmedAttack02).")]
    public string attackTrigger = "Attack";
    [Tooltip("Trigger parameter name on the Animator for the right-click punch (Giant@UnarmedAttack01).")]
    public string attackTriggerRight = "AttackRight";
    [Tooltip("Trigger parameter name on the Animator for the combo punch when left+right click are pressed together (Giant@UnarmedAttack03_A).")]
    public string attackTriggerCombo = "AttackCombo";
    [Tooltip("Forward distance from the giant's center to the middle of the attack sweep.")]
    public float attackRange = 8f;
    [Tooltip("Radius of the damage sweep around the attack point.")]
    public float attackRadius = 7f;
    [Tooltip("Forward distance from the giant's center to the middle of the combo attack's sweep. Bigger than the single-punch range, since the combo is meant to hit a wider area.")]
    public float attackRangeCombo = 10f;
    [Tooltip("Radius of the combo punch's damage sweep. Bigger than the single-punch radius, since the combo is meant to hit a wider area.")]
    public float attackRadiusCombo = 9f;
    [Tooltip("Extra visual-only size multiplier applied to the combo punch's ground-impact ring, on top of attackRadiusCombo. Purely cosmetic -- does not change the actual hit-detection radius.")]
    public float comboRingVisualScale = 1.6f;
    [Tooltip("Height above the ground of the attack point.")]
    public float attackHeight = 5f;
    [Tooltip("Damage applied to everything caught in the sweep.")]
    public float attackDamage = 40f;
    [Tooltip("Damage multiplier applied to a hit landing exactly at the center of the punch's impact point (attackPoint) -- the strongest possible hit from a right-click or combo punch.")]
    public float attackDamageCenterMultiplier = 3f;
    [Tooltip("Damage multiplier applied to a hit landing right at the outer edge of the punch's impact radius -- the weakest possible hit. Kept high enough that attackDamage * this is still clearly more than smashDamagePerSecond, so even a glancing right-click/combo punch always outdamages just walking into something.")]
    public float attackDamageEdgeMultiplier = 1.8f;
    [Tooltip("Seconds into the left/right punch's swing animation when the hit actually lands.")]
    public float attackImpactDelay = 0.85f;
    [Tooltip("Total length (seconds) of the left/right punch animation, used to know when the giant can move/attack again.")]
    public float attackAnimDuration = 1.97f;
    [Tooltip("Seconds into the combo punch's swing animation when the hit actually lands.")]
    public float attackImpactDelayCombo = 0.68f;
    [Tooltip("Total length (seconds) of the combo punch animation (Giant@UnarmedAttack03_A is shorter than the single punches).")]
    public float attackAnimDurationCombo = 1.57f;
    [Tooltip("Screen shake strength added per punch (0-1 range).")]
    public float attackShakeAmount = 0.6f;
    [Tooltip("Speed (units/sec) enemies would be shoved away from the giant by a knockback punch. Currently unused in practice: left click no longer deals damage (it grabs -- see the Grab section below) and the right-click/combo punches never pass knockback=true, so nothing currently triggers this. Kept in case a damaging knockback punch is wired back up later.")]
    public float attackKnockbackForce = 32f;
    [Tooltip("How long (seconds), after the first mouse button (left or right) is pressed, the other " +
        "button can still join it and turn the punch into the combo attack. Human clicks are almost " +
        "never on the exact same frame, so single punches are held back by this small window before " +
        "committing, to reliably detect a \"both at once\" press instead of firing the wrong single punch.")]
    public float comboInputWindow = 0.12f;
    [Tooltip("Forward distance the knockback punch's arm-swing fan is pushed out from the giant's own center before it's drawn, so it reads as happening in front of the giant rather than clipping through its body.")]
    public float armSwingForwardOffset = 3f;

    [Header("Grab")]
    [Tooltip("Left-click's punch (attackTrigger) grabs whichever tiny person/soldier is nearest the grab point, instead of dealing damage. Only one target can be held at a time; the grab attempt silently does nothing while a target is already held.")]
    public float grabRadius = 4.5f;
    [Tooltip("How far in front of the giant (along its forward direction) the grab point sits, so the grab range leans slightly ahead of the giant's feet rather than being centered exactly on them.")]
    public float grabForwardOffset = 3f;
    [Tooltip("Radius of the ground ring shown on whichever target would be grabbed right now (the nearest one in range). Purely visual.")]
    public float grabIndicatorRadius = 1.4f;
    [Tooltip("Where a grabbed target is held. If left unassigned, resolved automatically the first time something is grabbed: the giant's own right-hand bone (Humanoid rig), or a fixed point above/in front of the body as a fallback.")]
    public Transform handHoldPoint;
    [Tooltip("Small fine-tune offset, in the hand bone's own local axes, added on top of the grip position computed once at the moment of the grab (see ComputeHandLocalHoldOffset). Computed once (not every frame) for performance -- the held target then just rides along rigidly with the hand bone like any other parented object.")]
    public Vector3 grabLocalOffset = new Vector3(0.04f, 0.01f, 0f);
    [Tooltip("How tightly a held target is pulled in toward the giant's own torso, 0-1. 0 leaves it out at the hand's natural position (arm's length); 1 pulls it all the way to the torso/chest bone.")]
    public float grabBodyPullRatio = 0.45f;
    [Tooltip("Extra yaw (degrees), on top of facing the same direction as the giant, turning the held target further inward so its head/body leans toward the giant's torso instead of facing squarely forward.")]
    public float grabInwardYawDegrees = 30f;
    [Tooltip("Manual override for exactly where a held target sits. Assign a Transform here (e.g. a child object you've placed and dragged into position under the hand bone in the Hierarchy) and it is used directly -- the held target snaps to this exact position/rotation, and none of the automatic finger/body/yaw math above is used. Leave empty to keep using the automatic placement.")]
    public Transform grabAnchorOverride;
    [Tooltip("Extra fine-tune local position offset on top of grabAnchorOverride's own transform. Leave at zero if the anchor's own Position already has the exact values you want (the normal case) -- only use this for a small additional nudge without touching the anchor object itself.")]
    public Vector3 grabAnchorLocalPositionOffset = Vector3.zero;
    [Tooltip("Extra fine-tune local rotation offset (Euler degrees) on top of grabAnchorOverride's own rotation. Leave at zero if the anchor's own Rotation already has the exact values you want.")]
    public Vector3 grabAnchorLocalRotationOffset = Vector3.zero;

    [Header("Throw")]
    [Tooltip("Initial speed (units/sec) a held target launches at when thrown (see ThrowHeldTarget), along the camera's current look direction.")]
    public float throwSpeed = 120f;
    [Tooltip("Downward acceleration (units/sec^2) applied to a thrown target while it's in the air -- same idea as the giant's own gravity field, just given as a positive magnitude here.")]
    public float throwGravity = 10f;
    [Tooltip("Delay (seconds) between the Throw trigger firing and the held target actually detaching and launching -- tuned to line up with the animation's real release point (found by " +
        "sampling the throwing hand's speed across the clip; it spikes hardest around 52% through). The HumanF@ThrowBall01_R clip is 1.5s at 1x, but the Throw state's own Speed is set to 1.6x " +
        "in the AnimatorController for a snappier throwing motion, so this value is 0.78s / 1.6 rather than the raw 0.78s -- if the state's Speed changes again, rescale this by the same factor " +
        "(release normalized time * clip length / state speed) instead of re-guessing it.")]
    public float throwReleaseDelay = 0.49f;
    [Tooltip("Radius (world units) checked around a thrown target's current position -- every step of its flight arc, and once more at the landing spot -- for another vehicle or living " +
        "person/soldier in the way. The thrown target itself always dies/explodes on impact (see DestroyThrownOrHit) -- unlike a plain drop/release (ReleaseHeldTarget), which still lands " +
        "safely and survives -- but what happens to whatever it HIT depends on what was thrown: see ApplyThrowImpact and thrownPersonVehicleDamage.")]
    public float throwImpactRadius = 3.5f;
    [Tooltip("Damage dealt instead of an outright kill when a THROWN PERSON (not a vehicle) hits a tank/police car/helicopter -- a person is much lighter than another vehicle slamming " +
        "into it, so it shouldn't one-shot something with a real health pool the way a thrown car does. See ApplyThrowImpact.")]
    public float thrownPersonVehicleDamage = 30f;

    [Header("Eat")]
    [Tooltip("How long (seconds) the hand takes to rise from its normal held position up to the giant's mouth once eating triggers.")]
    public float eatRiseDuration = 0.35f;
    [Tooltip("How long (seconds) the hand pauses at the mouth -- this is the moment the held person is actually consumed (see EatRoutine) -- before the arm returns to rest.")]
    public float eatHoldAtMouthDuration = 0.15f;
    [Tooltip("How long (seconds) the now-empty hand takes to come back down to its normal resting position after eating.")]
    public float eatFallDuration = 0.35f;
    [Tooltip("How far (degrees) the upper arm swings to help bring the hand up toward the mouth, layered on top of whatever pose the Animator is already playing (see LateUpdate) -- same non-destructive approach as the throw-lean torso bend.")]
    public float eatUpperArmAngle = 100f;
    [Tooltip("How far (degrees) the forearm curls, on top of the upper arm swing, to bring the hand the rest of the way to the mouth.")]
    public float eatForearmAngle = 70f;
    [Tooltip("How far in front of the head bone (as a multiple of the chest-to-head bone distance, so it automatically scales with the giant's own size) the held person is pulled toward while eating -- on top of the arm-swing above, so the hand reliably ends up AT the mouth instead of just generally 'raised' near the face.")]
    public float eatMouthForwardFactor = 0.4f;
    [Tooltip("How far above (positive) or below (negative) the head bone, same scaling, the held person is pulled toward while eating.")]
    public float eatMouthUpFactor = -0.1f;
    [Tooltip("HP restored to the giant (GiantHealth.Heal) each time a person is eaten -- EatRoutine (person held directly) or CarPassengerFallRoutine (shaken out of a held car). Eating is now the giant's HP-recovery method in place of the roadside health pickups.")]
    public float eatHealAmount = 20f;

    [Header("Car Eat")]
    [Tooltip("How long (seconds) the arm takes to lift a held car up overhead once car-eating triggers (left click on a held car -- see CarEatRoutine).")]
    public float carLiftRiseDuration = 0.4f;
    [Tooltip("How long (seconds) the car stays lifted overhead while the shaken-out passenger falls into the giant's mouth.")]
    public float carLiftHoldDuration = 0.5f;
    [Tooltip("How long (seconds) the car falls back down to its normal held position afterward.")]
    public float carLiftFallDuration = 0.4f;
    [Tooltip("How far (degrees) the upper arm swings to lift a held car overhead -- bigger than the plain eat lift since the car needs to clear the giant's own head.")]
    public float carLiftUpperArmAngle = 150f;
    [Tooltip("How far (degrees) the forearm curls on top of the upper arm swing while lifting a car overhead.")]
    public float carLiftForearmAngle = 40f;
    [Tooltip("How far (degrees) the head tilts back to look up at the lifted car.")]
    public float carLiftHeadTiltAngle = 35f;
    [Tooltip("How long (seconds) the shaken-out passenger takes to fall from the car down to the giant's mouth.")]
    public float carEjectFallDuration = 0.3f;

    [Tooltip("UI element (e.g. a small crosshair) shown at the center of the screen while aiming, hidden the rest of the time. Leave unassigned to skip the marker.")]
    public GameObject aimMarker;
    [Tooltip("Max backward torso lean (degrees) applied while aiming/throwing, scaled by how far up the camera is currently pitched -- so throwing while looking up arches the body backward instead of always swinging the arm forward/level no matter where the camera's aimed.")]
    public float maxThrowLeanAngle = 35f;
    [Tooltip("How quickly the torso lean blends toward its target angle, both rising into a lean while aiming up and relaxing back out of one once the throw finishes.")]
    public float throwLeanBlendSpeed = 8f;

    [Header("Cage")]
    [Tooltip("The birdcage's transform. If left unassigned, resolved automatically at the moment it's needed by searching the scene for an object named 'YardTreeBirdCage'.")]
    public Transform birdCage;
    [Tooltip("Where inside the cage a caged target is placed. If left unassigned, a child object named 'CageInsidePoint' is created under the cage the first time it's needed (at the cage's own origin) -- drag it into position afterward, the same way GrabHoldAnchor works for the hand.")]
    public Transform cageInsidePoint;
    [Tooltip("How close (world units, measured from the giant's own position to the cage, horizontally) the giant must be for left-click to either cage a held target or open the Hostage Shop with an empty hand. Sized generously (bigger than the cage's own outward offset from the trunk) so it's reachable from any approach angle around the tree, including the side blocked furthest by the trunk's own solid collider.")]
    public float cageInteractRadius = 10f;
    [Tooltip("Fine-tune local position offset applied on top of cageInsidePoint's own transform (in the cage's local axes). The cage itself is built procedurally at runtime (see YardTreeBuilder), so cageInsidePoint can't be dragged into place in the Editor the way GrabHoldAnchor can -- use these numbers instead to nudge exactly where a caged target sits inside the cage.")]
    public Vector3 cageInsideLocalOffset = Vector3.zero;
    [Tooltip("Fine-tune local rotation offset (Euler degrees), same idea as cageInsideLocalOffset.")]
    public Vector3 cageInsideLocalRotationEuler = Vector3.zero;
    [Tooltip("HUD text (top of screen, next to the coin count) showing how many hostages are currently caged. Wire this to a Text/TMP element in the Inspector -- if left unassigned, the count simply isn't shown anywhere outside the Hostage Shop popup.")]
    public TMP_Text hostageCountText;

    private enum PendingAttackButton { None, Left, Right }

    private CharacterController controller;
    // Cached once in Start() -- Heal() is called on this when eating someone (see
    // EatRoutine/CarPassengerFallRoutine). Null-safe if the giant somehow lacks the
    // component.
    private GiantHealth giantHealth;
    private Vector3 velocity;
    // Last position the giant was confirmed actually grounded at (see IsActuallyGrounded()).
    // Pure safety net for the "fell through the ground" failure mode: whatever the root cause
    // of a given occurrence turns out to be (a runaway velocity.y from an unreliable grounded
    // check, an oversized single-frame deltaTime after the Editor/app stalls, some future
    // change nobody anticipated), if the giant ever ends up far enough below this that it can
    // only mean it tunneled through the ground plane, Update() below snaps it back here
    // instead of leaving it to fall forever.
    private Vector3 lastGroundedPosition;
    private bool hasLastGroundedPosition;
    private Transform cam;
    private float currentAnimSpeed;
    private bool isSprinting;
    private bool isAttacking;
    private bool isExhausted;
    // True while right click is held down AND something is currently held -- see TickAttackInput.
    // Drives the Animator's "Aiming" bool (the ThrowBall hold pose) and, while true, intercepts
    // both mouse buttons: right click no longer queues a punch, and left click throws instead of
    // grabbing/releasing/caging.
    private bool isAiming;
    private float footstepDistance;
    private ThirdPersonCamera cachedCam;
    private TrajectoryPreview trajectoryPreview;
    // Smoothed backward-lean angle currently applied to the torso (see LateUpdate), the target
    // angle captured from the aim camera's pitch while aiming, and a hold timer that keeps that
    // captured lean active through the throw's wind-up/release after isAiming itself flips back
    // to false (see ThrowHeldTarget) so the lean doesn't snap away right as the throw fires.
    private float currentThrowLean;
    private float throwLeanCapturedTarget;
    private float throwLeanHoldTimer;
    private PendingAttackButton pendingAttackButton = PendingAttackButton.None;
    private float pendingAttackTimer;
    // Set by SuppressAttackInputBriefly (real/unscaled time, so it works whether the game is
    // currently paused or not). While Time.unscaledTime is under this, TickAttackInput ignores
    // mouse input entirely.
    private float inputSuppressUntil;
    private Transform heldTarget;
    // True while the currently held target is a tiny person/soldier eligible to be caged as
    // a hostage (see CageHeldTarget) -- false for a held vehicle, which left click near the
    // cage should never lock away as a "hostage".
    private bool heldIsHostage;
    // Tiny people locked into the birdcage (see CageHeldTarget), spendable as currency at the
    // Hostage Shop (see SpendHostages / HostageShopMenu). Newest-caged is spent first (a
    // simple stack), which is an arbitrary but harmless choice since all hostages are worth
    // the same amount.
    private System.Collections.Generic.List<Transform> cagedHostages = new System.Collections.Generic.List<Transform>();
    private GrabTargetIndicator grabIndicator;
    private Transform gripHandBone;
    private Transform gripIndexBone;
    private Transform gripMiddleBone;
    private Transform gripRingBone;
    private Transform gripLittleBone;
    private Transform gripChestBone;
    private bool gripBonesResolved;
    private Transform gripUpperArmBone;
    private Transform gripLowerArmBone;
    // True for the duration of EatRoutine (rise -> consume -> fall). Gates LateUpdate's arm
    // rotation and, via isAttacking, blocks other attack input for the same span TickAttackInput
    // already blocks while a punch/throw animation is playing.
    private bool isEating;
    // 0..1 blend driving how far up the eat arm-raise is right now (0 = resting, 1 = at the
    // mouth) -- rises over eatRiseDuration, holds at 1 through eatHoldAtMouthDuration, then
    // falls back to 0 over eatFallDuration. See EatRoutine/LateUpdate.
    private float eatBlend;
    // The person currently mid-EatRoutine, if any -- LateUpdate pulls it the rest of the way
    // to the mouth (on top of the arm-swing rotation) while this is set. Cleared once it's
    // actually consumed.
    private Transform eatingTarget;
    private Transform gripHeadBone;
    // True for the duration of CarEatRoutine (lift car overhead -> eject+eat passenger ->
    // lower car back down). Gates LateUpdate's overhead arm/head rotation the same way
    // isEating/eatBlend gate the plain hand-to-mouth motion.
    private bool isCarLifting;
    private float carLiftBlend;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        giantHealth = GetComponent<GiantHealth>();
        cam = Camera.main != null ? Camera.main.transform : null;
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        currentStamina = maxStamina;

        if (staminaFillImage != null)
        {
            staminaFillRect = staminaFillImage.rectTransform;
            // Whatever width the bar was authored at (full stamina) is the max — read it once
            // rather than hardcoding it, so resizing the bar in the UI still works correctly.
            staminaFillMaxWidth = staminaFillRect.sizeDelta.x;
        }

        UpdateStaminaUI();
        UpdateHostageUI();
    }

    void Update()
    {
        // Ignore all input until the game has actually started -- otherwise the mouse click
        // that lands on the start screen's "Start Game" button also registers as a real
        // Input.GetMouseButtonDown(0) here (Unity's Input class doesn't know or care that the
        // click was consumed by UI), which used to queue up a left-click punch that then fired
        // the instant Time.timeScale resumed, making the game appear to start mid-attack.
        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.HasGameStarted)
        {
            return;
        }

        // Once stamina bottoms out, lock out stamina-consuming actions (sprint, attack) until
        // it has fully refilled back to max — not just above 0 — so the player can't immediately
        // re-sprint/re-punch a moment after hitting empty. Checked every frame from the current
        // value, so this self-clears exactly once currentStamina reaches maxStamina again.
        if (currentStamina <= 0f)
        {
            isExhausted = true;
        }
        else if (currentStamina >= maxStamina)
        {
            isExhausted = false;
        }

        TickAttackInput();
        UpdateGrabPreview();

        float h = isAttacking ? 0f : Input.GetAxis("Horizontal");
        float v = isAttacking ? 0f : Input.GetAxis("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.magnitude > 1f) inputDir.Normalize();

        bool hasInput = inputDir.magnitude >= 0.1f;
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // Sprint requires Shift + a direction key held together, this frame, AND stamina left
        // to spend, AND not currently locked out from hitting empty. The moment any of those
        // isn't true, sprinting stops immediately.
        isSprinting = ComputeSprint(hasInput, shiftHeld, currentStamina, isExhausted);

        // Drain while sprinting or mid-punch, regenerate while walking normally or standing
        // still. Uses this frame's isSprinting/isAttacking, so a sprint that just got cut off
        // by hitting 0 stamina above correctly stops draining and starts regenerating already
        // this same frame.
        UpdateStamina(Time.deltaTime);

        float targetAnimSpeed = 0f;
        float currentMoveSpeed = 0f;

        if (hasInput)
        {
            float camYaw = cam != null ? cam.eulerAngles.y : 0f;
            Vector3 moveDir = Quaternion.Euler(0f, camYaw, 0f) * inputDir;

            // While aiming, facing is driven by the camera instead (right below) so the throw
            // always goes where the camera's actually pointing, even while strafing.
            if (!isAiming)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
            }

            currentMoveSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
            controller.Move(moveDir * currentMoveSpeed * Time.deltaTime);
            targetAnimSpeed = inputDir.magnitude;
        }

        // Aiming always faces the camera's current yaw, whether or not the giant is also moving
        // -- lets the player strafe with WASD while keeping the throw lined up with the camera.
        if (isAiming && cam != null)
        {
            Quaternion aimRot = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, aimRot, rotateSpeed * Time.deltaTime);
        }

        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetAnimSpeed, animSpeedDamping * Time.deltaTime);
        if (animator != null)
        {
            animator.SetFloat("Speed", currentAnimSpeed);
            animator.SetBool("Sprint", isSprinting);
        }

        // Ground check used for BOTH footstep shake and the gravity clamp just below --
        // computed once per frame via the direct raycast rather than trusting
        // controller.isGrounded (confirmed via live testing to read false even while the
        // giant is standing still and settled on the ground on this rig -- it's collision-flag
        // based and only reflects the last Move() call, which on a frame with purely
        // horizontal input movement often doesn't register a below-collision at all). This
        // was previously only fixed for footstep shake; the gravity clamp below still used
        // the unreliable controller.isGrounded, which meant velocity.y almost never actually
        // got reset back down to its resting -2f baseline -- it just kept accumulating more
        // negative, frame after frame, for as long as controller.isGrounded happened to read
        // false (which live testing shows is most of the time even while grounded). Given
        // enough uninterrupted playtime that eventually produces a single-frame velocity.y
        // large enough to tunnel straight through the thin Ground plane collider, which is
        // the real root cause of the giant falling through the ground -- not specific to
        // Load, just most noticeable shortly after one since velocity.y resets to 0 there.
        bool grounded = IsActuallyGrounded();

        // Subtle footstep shake, gated strictly by the foot actually being on the ground:
        // distance-based rather than a timer, so it only accumulates while grounded AND
        // moving, and can never fire mid-air or while standing still.
        TickFootstepShake(hasInput, grounded, currentMoveSpeed, Time.deltaTime);

        if (grounded && velocity.y < 0f)
        {
            velocity.y = -2f;
            if (Input.GetButtonDown("Jump"))
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Track the last confirmed-grounded spot, and recover instantly if we ever end up far
        // enough below it that the only explanation is having tunneled through the ground --
        // see the field comment on lastGroundedPosition for why this exists alongside the
        // grounded-check fix above.
        if (grounded)
        {
            lastGroundedPosition = transform.position;
            hasLastGroundedPosition = true;
        }
        else if (hasLastGroundedPosition && transform.position.y < lastGroundedPosition.y - 5f)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = lastGroundedPosition + Vector3.up * 0.5f;
            controller.enabled = wasEnabled;
            velocity = Vector3.zero;
        }
    }

    // Left click alone -> attackTrigger (Attack02) animation, but the actual effect at impact
    // is now a grab (whichever tiny person/soldier is directly underneath the giant), not damage.
    // If already holding someone, left click instead either cages them (if close enough to the
    // birdcage -- see IsHeldTargetNearCage/CageHeldTarget) or releases them.
    // Right click alone -> attackTriggerRight (Attack01), a real damaging punch as before. Both
    // pressed within comboInputWindow of each other -> attackTriggerCombo (Attack03_A), also a
    // real damaging punch. A newly-pressed button is held pending for comboInputWindow seconds
    // before committing to its single action, so the other button still has a chance to join it
    // and upgrade it into the combo — otherwise a same-frame double click would almost never be
    // detected, since human input rarely lands both clicks on the exact same frame.
    void TickAttackInput()
    {
        if (isAttacking)
        {
            return;
        }

        // Skip entirely while any menu has the game paused (Z/X/the Hostage Shop/etc.), the
        // pointer is over a UI element, or we're in a brief post-menu-close cooldown (see
        // SuppressAttackInputBriefly).
        if (Time.timeScale == 0f || Time.unscaledTime < inputSuppressUntil || IsPointerOverUI())
        {
            return;
        }

        bool leftDown = Input.GetMouseButtonDown(0);
        bool rightDown = Input.GetMouseButtonDown(1);

        // Q always fires the bigger combo punch, whether or not a hand is already full (same
        // as a real punch could always land even while holding something). It's on its own key
        // specifically so it never needs to be disambiguated from a single left/right click --
        // which is why the mouse clicks below no longer need any wait-and-see delay either.
        if (Input.GetKeyDown(KeyCode.Q) && !isExhausted)
        {
            StartCoroutine(DoAttack(attackTriggerCombo, attackImpactDelayCombo, attackAnimDurationCombo, false, true));
            return;
        }

        if (heldTarget != null)
        {
            // Holding something -- mouse is entirely about what to do with it: aim+throw
            // (right held + left click), let go (both clicked together), cage (left click near
            // the birdcage), or eat (left click otherwise -- people get the hand-to-mouth
            // motion, cars get hoisted overhead and shaken first -- see
            // EatRoutine/CarEatRoutine).
            isAiming = Input.GetMouseButton(1);
            if (animator != null)
            {
                animator.SetBool("Aiming", isAiming);
            }

            ThirdPersonCamera aimCam = GetCam();
            if (aimCam != null)
            {
                aimCam.SetAiming(isAiming);
            }
            if (isAiming && aimCam != null)
            {
                // How far up the camera is currently pitched, as a 0-1 factor between level
                // (0) and its steepest allowed upward aim angle (1) -- drives how far the torso
                // leans back in LateUpdate, so looking straight up and throwing visibly arches
                // the body instead of throwing flat/forward regardless of where the camera's
                // pointed.
                float lookUpFactor = Mathf.Clamp01(Mathf.InverseLerp(0f, aimCam.aimMinPitch, aimCam.Pitch));
                throwLeanCapturedTarget = lookUpFactor * maxThrowLeanAngle;
            }
            if (aimMarker != null && aimMarker.activeSelf)
            {
                aimMarker.SetActive(false);
            }

            if (isAiming)
            {
                // Live trajectory preview: show the predicted flight path (and where it'll
                // land) using the exact same launch-direction logic the real throw uses.
                // Created lazily on first use.
                if (trajectoryPreview == null)
                {
                    trajectoryPreview = TrajectoryPreview.Create();
                }
                Vector3 previewOrigin = heldTarget.position;
                Vector3 previewAimPoint = ComputeAimPoint(heldTarget);
                Vector3 previewDir = (previewAimPoint - previewOrigin).normalized;
                trajectoryPreview.Show(previewOrigin, previewDir, throwSpeed, throwGravity, transform, heldTarget);

                if (leftDown)
                {
                    ThrowHeldTarget();
                }
                return;
            }
            else if (trajectoryPreview != null)
            {
                trajectoryPreview.Hide();
            }

            // Both buttons clicked on the same frame -- let go, whatever's being held.
            if (leftDown && rightDown)
            {
                ReleaseHeldTarget();
                return;
            }

            if (!leftDown)
            {
                return;
            }

            if (heldIsHostage && IsHeldTargetNearCage())
            {
                // Close enough to the birdcage -- left click locks the held hostage into the
                // cage instead of eating/releasing them.
                CageHeldTarget();
            }
            else if (IsPersonTransform(heldTarget))
            {
                // A tiny person/soldier, not near the cage -- left click eats them (see
                // EatRoutine).
                StartCoroutine(EatRoutine(heldTarget));
            }
            else if (IsCarTransform(heldTarget))
            {
                // A held car -- left click hoists it overhead and shakes the driver out into
                // the giant's mouth instead of just dropping it (see CarEatRoutine). The car
                // itself stays held afterward.
                StartCoroutine(CarEatRoutine(heldTarget));
            }
            else
            {
                // A held tank/helicopter/other vehicle (no car-eat spectacle for these) --
                // left click just lets go of it.
                ReleaseHeldTarget();
            }
            return;
        }

        // Empty hand: left click grabs, right click throws a real damaging punch -- both fire
        // immediately now, no combo to wait and disambiguate from (that moved to Q above).
        if (leftDown)
        {
            if (IsGiantNearCage() && TryOpenHostageShop())
            {
                // Empty hand, near the birdcage -- left click opens the Hostage Shop instead
                // of grabbing. No animation/stamina cost, since this is a UI interaction, not
                // an attack.
            }
            else if (!isExhausted)
            {
                // Left click: same punch animation/timing/stamina cost as a real punch, but
                // grabs whichever tiny person/soldier/vehicle is directly underneath the giant
                // instead of dealing damage.
                StartCoroutine(DoAttack(attackTrigger, attackImpactDelay, attackAnimDuration, false, false, true));
            }
        }
        else if (rightDown && !isExhausted)
        {
            // Right click always throws a real damaging punch.
            StartCoroutine(DoAttack(attackTriggerRight, attackImpactDelay, attackAnimDuration, false, false));
        }
    }

    IEnumerator DoAttack(string trigger, float impactDelay, float animDuration, bool knockback, bool isCombo, bool isGrab = false)
    {
        isAttacking = true;

        // Flat one-time cost per punch (on top of whatever passive stamina rules already apply
        // while isAttacking is true, which block regen for the swing's duration).
        currentStamina = Mathf.Max(0f, currentStamina - staminaCostPerAttack);
        UpdateStaminaUI();

        if (animator != null)
        {
            animator.SetTrigger(trigger);
        }

        yield return new WaitForSeconds(impactDelay);

        if (isGrab)
        {
            TryGrabBelow();
        }
        else
        {
            ApplyAttackDamage(knockback, isCombo);
        }

        float remaining = animDuration - impactDelay;
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        isAttacking = false;
    }

    // Extracted so it's directly testable and so Update() reads as a single clear rule:
    // sprinting requires Shift + a direction key held this frame, stamina left to spend, and
    // not being in the post-exhaustion lockout (which only clears once stamina hits max again).
    bool ComputeSprint(bool hasInput, bool shiftHeld, float stamina, bool exhausted)
    {
        return hasInput && shiftHeld && stamina > 0f && !exhausted;
    }

    // Drains stamina while sprinting (continuous, per second), holds it steady while mid-punch
    // (the punch's own flat cost already applies once in DoAttack — no passive regen during the
    // swing either, so button-mashing punches can't be used to dodge the stamina system), and
    // regenerates it the rest of the time: walking at normal speed, or standing still.
    void UpdateStamina(float deltaTime)
    {
        if (isSprinting)
        {
            currentStamina -= staminaDrainPerSecondSprint * deltaTime;
        }
        else if (!isAttacking)
        {
            currentStamina += staminaRegenPerSecond * deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        UpdateStaminaUI();
    }

    void UpdateStaminaUI()
    {
        if (staminaText != null)
        {
            staminaText.text = "Stamina: " + Mathf.CeilToInt(currentStamina) + " / " + Mathf.CeilToInt(maxStamina);
        }

        if (staminaFillRect != null)
        {
            float ratio = maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;
            Vector2 sd = staminaFillRect.sizeDelta;
            sd.x = staminaFillMaxWidth * ratio;
            staminaFillRect.sizeDelta = sd;
        }

        // Darken the gauge while locked out from exhaustion, as an extra visual cue on top of the
        // bar's own width, so it's obvious the player can't sprint/attack again yet even at a
        // glance. Reverts to the normal color automatically once isExhausted clears (stamina back
        // at max), since this runs every time the stamina UI updates.
        if (staminaFillImage != null)
        {
            staminaFillImage.color = isExhausted ? staminaExhaustedColor : staminaNormalColor;
        }
    }

    // Direct, immediate ground check via a short raycast from the bottom of the capsule's
    // actual world-space bounds, instead of trusting CharacterController.isGrounded (which
    // only reflects whether the last Move() call happened to register a below-collision this
    // exact frame, and was confirmed live to read false while the giant stood still on flat
    // ground). This never depends on collision-flag timing, only on what's physically beneath.
    bool IsActuallyGrounded()
    {
        float bottomY = controller.bounds.min.y;
        Vector3 origin = new Vector3(transform.position.x, bottomY + 0.15f, transform.position.z);
        return Physics.Raycast(origin, Vector3.down, 0.35f);
    }

    // Accumulates ground distance covered while grounded+moving and fires one shake pulse
    // per "stride". Reset to 0 whenever airborne or not moving, so a pulse can only ever
    // land at a moment the giant is actually moving with its foot planted on the ground.
    void TickFootstepShake(bool moving, bool grounded, float speed, float deltaTime)
    {
        if (moving && grounded)
        {
            footstepDistance += speed * deltaTime;
            if (footstepDistance >= footstepStride)
            {
                ThirdPersonCamera camRef = GetCam();
                if (camRef != null)
                {
                    camRef.AddShake(footstepShakeAmount * (isSprinting ? sprintShakeMultiplier : 1f));
                }
                footstepDistance = 0f;
            }
        }
        else
        {
            footstepDistance = 0f;
        }
    }

    // ThirdPersonCamera.Instance is set in Awake(), which does NOT re-run for objects that
    // already existed before a script recompile mid-Play-session — so after any script edit
    // while already playing, the static Instance (and any cached reference to it) can go
    // stale/null even though the camera is alive in the scene. Re-resolve through Instance,
    // then a direct scene lookup, whenever the cache is missing, so shake never silently stops.
    ThirdPersonCamera GetCam()
    {
        if (cachedCam == null)
        {
            cachedCam = ThirdPersonCamera.Instance;
            if (cachedCam == null)
            {
                cachedCam = FindObjectOfType<ThirdPersonCamera>();
            }
        }
        return cachedCam;
    }

    // Where the grab range is centered: a little ahead of the giant's own feet (along its
    // current facing) rather than exactly on top of them, so it leans forward slightly like
    // the punches do, instead of only ever catching someone standing precisely underneath.
    Vector3 GrabCenter()
    {
        return transform.position + transform.forward * grabForwardOffset;
    }

    // Shared by the live target-preview and the actual grab: finds whichever tiny
    // person/soldier is nearest the grab center, within grabRadius. Returns null (with both
    // out params null) if nothing is in range.
    Transform FindNearestGrabCandidate(out TinyNPC tinyOut, out TinySoldierAI soldierOut, out Behaviour vehicleOut)
    {
        tinyOut = null;
        soldierOut = null;
        vehicleOut = null;

        Physics.SyncTransforms();

        Vector3 center = GrabCenter();
        Collider[] hits = Physics.OverlapSphere(center, grabRadius);

        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (Collider col in hits)
        {
            TinyNPC tiny = col.GetComponentInParent<TinyNPC>();
            if (tiny != null)
            {
                // Immune NPCs (see TinyNPC.giantImmune -- e.g. BlackMan) can't be grabbed at
                // all, so they're skipped here rather than just refusing the grab later --
                // this also keeps the grab-preview ring (see UpdateGrabPreview) from ever
                // highlighting them in the first place.
                if (tiny.giantImmune) continue;

                float d = Vector3.Distance(tiny.transform.position, center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = tiny.transform;
                    tinyOut = tiny;
                    soldierOut = null;
                    vehicleOut = null;
                }
                continue;
            }

            TinySoldierAI soldier = col.GetComponentInParent<TinySoldierAI>();
            if (soldier != null)
            {
                float d = Vector3.Distance(soldier.transform.position, center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = soldier.transform;
                    soldierOut = soldier;
                    tinyOut = null;
                    vehicleOut = null;
                }
                continue;
            }

            // Vehicles -- police cars, tanks, helicopters, and ambient traffic cars -- are all
            // grabbable the same way as tiny people/soldiers, just checked as a group since they
            // don't share a common base AI type.
            Behaviour vehicle = FindVehicleAI(col);
            if (vehicle != null)
            {
                float d = Vector3.Distance(vehicle.transform.position, center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = vehicle.transform;
                    vehicleOut = vehicle;
                    tinyOut = null;
                    soldierOut = null;
                }
            }
        }

        return best;
    }

    // Checks a collider against every vehicle AI type in turn (via GetComponentInParent, since
    // the collider that actually triggers a hit sits on a child body/turret piece while the AI
    // script lives on the vehicle's root -- the same layout ApplyAttackDamage/StompZone already
    // rely on). Returns the first match, or null if the collider isn't part of any vehicle.
    Behaviour FindVehicleAI(Collider col)
    {
        PoliceCarAI police = col.GetComponentInParent<PoliceCarAI>();
        if (police != null) return police;

        TankAI tank = col.GetComponentInParent<TankAI>();
        if (tank != null) return tank;

        HelicopterAI heli = col.GetComponentInParent<HelicopterAI>();
        if (heli != null) return heli;

        TrafficCarAI traffic = col.GetComponentInParent<TrafficCarAI>();
        if (traffic != null) return traffic;

        return null;
    }

    // Keeps a ring indicator hovering over whichever target would be grabbed right now, so the
    // player can see who's about to get snatched before clicking. Hidden while the giant is
    // already holding someone (nothing new can be grabbed) or nothing is in range.
    void UpdateGrabPreview()
    {
        if (heldTarget != null)
        {
            if (grabIndicator != null)
            {
                Destroy(grabIndicator.gameObject);
                grabIndicator = null;
            }
            return;
        }

        TinyNPC tinyOut;
        TinySoldierAI soldierOut;
        Behaviour vehicleOut;
        Transform best = FindNearestGrabCandidate(out tinyOut, out soldierOut, out vehicleOut);

        if (best == null)
        {
            if (grabIndicator != null)
            {
                Destroy(grabIndicator.gameObject);
                grabIndicator = null;
            }
            return;
        }

        if (grabIndicator == null)
        {
            grabIndicator = GrabTargetIndicator.Create(best, grabIndicatorRadius);
        }
        else if (!grabIndicator.IsTarget(best))
        {
            grabIndicator.SetTarget(best);
        }
    }

    // Called from the left-click punch's DoAttack coroutine at the moment of impact (in place
    // of dealing damage): grabs whichever tiny person/soldier is nearest the grab center (see
    // GrabCenter -- a little ahead of the giant's feet, NOT the forward-reaching attack sweep
    // the punches use) and holds them in the giant's hand. Does nothing if the giant is already
    // holding someone -- only one target can be held at a time.
    void TryGrabBelow()
    {
        if (heldTarget != null)
        {
            return;
        }

        TinyNPC bestTiny;
        TinySoldierAI bestSoldier;
        Behaviour bestVehicle;
        Transform best = FindNearestGrabCandidate(out bestTiny, out bestSoldier, out bestVehicle);
        if (best == null)
        {
            return;
        }

        if (bestTiny != null)
        {
            bestTiny.enabled = false;
            if (bestTiny.animator != null)
            {
                bestTiny.animator.SetFloat("Speed", 0f);
                bestTiny.animator.SetBool("Flee", false);
            }
            heldIsHostage = true;
            AttachToHand(bestTiny.transform);
        }
        else if (bestSoldier != null)
        {
            bestSoldier.enabled = false;
            if (bestSoldier.animator != null)
            {
                bestSoldier.animator.SetFloat("Speed", 0f);
                bestSoldier.animator.SetBool("Aiming", false);
            }
            heldIsHostage = true;
            AttachToHand(bestSoldier.transform);
        }
        else if (bestVehicle != null)
        {
            // Vehicles aren't hostages -- just stop their own AI/movement while held, same as
            // TinyNPC/TinySoldierAI above, with no animator resets since they don't share that
            // convention.
            bestVehicle.enabled = false;
            heldIsHostage = false;
            AttachToHand(bestVehicle.transform);
        }
    }

    // Disables the grabbed target's own colliders (so it stops being hit-tested/physically
    // interacting while held) and parents it to the hand hold point, riding along with the
    // giant's hand from then on.
    void AttachToHand(Transform target)
    {
        heldTarget = target;

        if (grabIndicator != null)
        {
            Destroy(grabIndicator.gameObject);
            grabIndicator = null;
        }

        Collider[] cols = target.GetComponentsInChildren<Collider>();
        foreach (Collider c in cols)
        {
            c.enabled = false;
        }

        // The giant's visual rig scales its (normal-human-sized) model up internally to reach
        // giant size, so every bone -- including the hand -- carries that same inherited scale.
        // Record the target's current WORLD scale before reparenting, then re-derive the local
        // scale needed under the hand bone so its world size is unchanged by the grab -- otherwise
        // it would suddenly balloon by the giant's own internal scale factor once parented.
        Vector3 worldScaleBeforeGrab = target.lossyScale;

        Transform hold;
        Vector3 localOffset;
        Quaternion localRot;

        if (grabAnchorOverride != null)
        {
            // Manual placement: whatever Transform is assigned here is used exactly as-is --
            // the held target just snaps onto it (zero local offset/rotation of its own). Move
            // grabAnchorOverride around in the Scene view (it's usually a child object sitting
            // under the hand bone) to change exactly where/how a held target sits, with no code
            // changes needed.
            hold = grabAnchorOverride;
            localOffset = grabAnchorLocalPositionOffset;
            localRot = Quaternion.Euler(grabAnchorLocalRotationOffset);
        }
        else
        {
            hold = ResolveHandHoldPoint();

            // Computed once, right now, from the live finger bones -- this is the moment the
            // giant is actually grabbing (hand slightly curled), so this captures "inside the
            // curled fingers" without needing to keep recomputing it every frame afterward.
            // From here on the target just rides along rigidly with the hand bone like any
            // other parented object.
            localOffset = ComputeHandLocalHoldOffset(hold);

            // Face the held target the same direction the giant itself is currently facing
            // (rather than inheriting the hand bone's own rotation, which can point
            // sideways/down depending on the arm's current pose), then add a bit of extra
            // inward yaw so it visibly leans toward the giant's body instead of facing squarely
            // forward like the giant does.
            localRot = Quaternion.Inverse(hold.rotation) * transform.rotation * Quaternion.Euler(0f, grabInwardYawDegrees, 0f);
        }

        target.SetParent(hold, false);
        target.localPosition = localOffset;
        target.localRotation = localRot;

        Vector3 parentScale = hold.lossyScale;
        target.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? target.localScale.x : worldScaleBeforeGrab.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? target.localScale.y : worldScaleBeforeGrab.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? target.localScale.z : worldScaleBeforeGrab.z / parentScale.z
        );

        ThirdPersonCamera camRef = GetCam();
        if (camRef != null)
        {
            camRef.AddShake(0.25f);
        }
    }

    // Undoes AttachToHand: unparents the held target back into the world at the hand's current
    // position (dropped straight down to ground level), restores its collider(s) and its own
    // AI/movement script, and clears heldTarget so a new grab can happen again.
    void ReleaseHeldTarget()
    {
        if (heldTarget == null)
        {
            return;
        }

        Transform released = heldTarget;
        heldTarget = null;

        FinalizeRelease(released);

        ThirdPersonCamera camRef = GetCam();
        if (camRef != null)
        {
            camRef.AddShake(0.15f);
        }
    }

    // Right click (while holding something) enters aim mode -- the ThrowBall01_R hold pose
    // (see the Animator's Aiming bool) -- and left click while aiming fires this: plays the
    // ThrowBall01_R throw animation, detaches the held target, and sends it flying along the
    // camera's current look direction under a simple gravity arc (ThrowArcRoutine), landing it
    // with the exact same logic a plain drop uses (FinalizeRelease) once it comes back down.
    void ThrowHeldTarget()
    {
        if (heldTarget == null)
        {
            return;
        }

        Transform target = heldTarget;
        heldTarget = null;
        isAiming = false;
        // Keep the torso lean captured while aiming held through the throw's wind-up and a short
        // follow-through, so the release itself still reads as an upward throw even though
        // isAiming has already flipped back to false (see LateUpdate).
        throwLeanHoldTimer = throwReleaseDelay + 0.35f;

        // Captured NOW, at the moment the throw actually fires -- not at release time -- so the
        // object flies exactly where the player was aiming when they clicked. Capturing it later
        // (at release, after the wind-up) used to make throws land lower than intended whenever
        // the player's aim naturally drifted down even a little during the throwReleaseDelay wait
        // -- most noticeable aiming steeply upward at something like a helicopter, where even a
        // small drift is a big miss.
        //
        // What's actually under the crosshair is captured NOW, at trigger time -- see
        // ComputeAimPoint for the parallax-correcting raycast (shared with the live trajectory
        // preview, so the preview always matches). The DIRECTION to it is deliberately NOT
        // computed yet: the held target keeps riding the hand through the whole Throw wind-up
        // animation, and the hand's position at the actual release (throwReleaseDelay later,
        // after the arm has swung through the throw motion) is meaningfully different from where
        // it is right now. Locking in a direction from this frame's hand position would send the
        // object off at an angle that no longer points at the captured aim point by the time it
        // actually leaves the hand -- ReleaseThrownTargetAfterDelay re-derives the direction from
        // THAT position instead, toward this same captured point.
        Transform capturedAimActor;
        Vector3 capturedAimPoint = ComputeAimPoint(target, out capturedAimActor);

        if (trajectoryPreview != null)
        {
            trajectoryPreview.Hide();
        }

        if (animator != null)
        {
            animator.SetBool("Aiming", false);
            animator.SetTrigger("Throw");
        }

        StartCoroutine(ReleaseThrownTargetAfterDelay(target, GetCam(), capturedAimPoint, capturedAimActor));
    }

    // Shared by ThrowHeldTarget (the real throw, at trigger time), ReleaseThrownTargetAfterDelay
    // (deriving the actual launch direction toward this same point once the wind-up finishes),
    // and the live trajectory preview -- so all three always agree on what's actually under the
    // crosshair. Corrects for parallax: the camera sits well off to the giant's side (the
    // shoulder-view aim offset), while a thrown object launches from the hand, close to the
    // giant's own centerline. Raycasts along the camera's own sightline (skipping the giant's own
    // body and the currently-held object) and returns the first real hit point, or a far point
    // along the sightline if the ray hits nothing, so aiming at open sky still resolves to a
    // sensible point rather than nothing.
    Vector3 ComputeAimPoint(Transform heldObj)
    {
        Transform unusedActor;
        return ComputeAimPoint(heldObj, out unusedActor);
    }

    // hitActor is the live vehicle/person the aim raycast actually landed on (if any) -- see
    // ResolveTrackableActor. ReleaseThrownTargetAfterDelay uses it to keep tracking that
    // target's CURRENT position at release time instead of the point it happened to occupy
    // back when the throw was triggered, since a moving target (a helicopter repositioning to
    // hover over the giant, a car still driving, etc.) may no longer be there by then --
    // especially at close range, where the same amount of target movement is a much bigger
    // angular miss than it would be at distance.
    Vector3 ComputeAimPoint(Transform heldObj, out Transform hitActor)
    {
        hitActor = null;

        if (cam == null)
        {
            return transform.position + transform.forward * 500f;
        }

        const float maxAimDistance = 500f;
        Vector3 aimPoint = cam.position + cam.forward * maxAimDistance;
        RaycastHit[] aimHits = Physics.RaycastAll(cam.position, cam.forward, maxAimDistance);
        System.Array.Sort(aimHits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit aimHit in aimHits)
        {
            if (aimHit.transform == transform || aimHit.transform.IsChildOf(transform))
            {
                continue;
            }
            if (heldObj != null && (aimHit.transform == heldObj || aimHit.transform.IsChildOf(heldObj)))
            {
                continue;
            }
            aimPoint = aimHit.point;
            hitActor = ResolveTrackableActor(aimHit.transform);
            break;
        }
        return aimPoint;
    }

    // Same "what kind of live target is this" check FindThrowHitAt uses for mid-flight hits --
    // shared here so the aim raycast can recognize when it landed on one of them too.
    Transform ResolveTrackableActor(Transform hit)
    {
        TrafficCarAI traffic = hit.GetComponentInParent<TrafficCarAI>();
        if (traffic != null) return traffic.transform;
        PoliceCarAI police = hit.GetComponentInParent<PoliceCarAI>();
        if (police != null) return police.transform;
        TankAI tank = hit.GetComponentInParent<TankAI>();
        if (tank != null) return tank.transform;
        HelicopterAI heli = hit.GetComponentInParent<HelicopterAI>();
        if (heli != null) return heli.transform;
        TinyNPC npc = hit.GetComponentInParent<TinyNPC>();
        if (npc != null) return npc.transform;
        TinySoldierAI soldier = hit.GetComponentInParent<TinySoldierAI>();
        if (soldier != null) return soldier.transform;
        return null;
    }

    // Applies the torso lean captured above on top of whatever pose the Animator has already
    // evaluated for this frame -- LateUpdate runs after Mecanim's own update, so this sticks
    // visually instead of being overwritten. Non-destructive: it rotates around the CURRENT
    // (already-posed) chest rotation each frame rather than accumulating, so it behaves the
    // same whether the giant is idle, aiming, or mid-throw-animation.
    void LateUpdate()
    {
        if (throwLeanHoldTimer > 0f)
        {
            throwLeanHoldTimer -= Time.deltaTime;
        }

        float targetLean = (isAiming || throwLeanHoldTimer > 0f) ? throwLeanCapturedTarget : 0f;
        currentThrowLean = Mathf.Lerp(currentThrowLean, targetLean, throwLeanBlendSpeed * Time.deltaTime);

        if (currentThrowLean > 0.05f)
        {
            ResolveGripBones();
            if (gripChestBone != null)
            {
                gripChestBone.rotation = Quaternion.AngleAxis(-currentThrowLean, transform.right) * gripChestBone.rotation;
            }
        }

        // Eat hand-to-mouth motion -- same layered-on-top-of-Mecanim approach as the throw
        // lean above, just applied to the arm bones instead of the chest, so it works no
        // matter what the Animator itself is currently playing.
        if (eatBlend > 0.001f)
        {
            ResolveGripBones();
            if (gripUpperArmBone != null)
            {
                gripUpperArmBone.rotation = Quaternion.AngleAxis(-eatUpperArmAngle * eatBlend, transform.right) * gripUpperArmBone.rotation;
            }
            if (gripLowerArmBone != null)
            {
                gripLowerArmBone.rotation = Quaternion.AngleAxis(-eatForearmAngle * eatBlend, transform.right) * gripLowerArmBone.rotation;
            }

            // Pull the held person the rest of the way to the mouth, on top of whatever the
            // arm swing above landed it at -- guarantees it actually reaches the mouth by
            // eatBlend == 1 regardless of the rig's exact arm proportions/reach, instead of
            // just ending up generally 'raised' near the face.
            if (eatingTarget != null && gripHeadBone != null)
            {
                float headScale = gripChestBone != null ? Vector3.Distance(gripChestBone.position, gripHeadBone.position) : 1f;
                Vector3 mouthPos = gripHeadBone.position + transform.forward * headScale * eatMouthForwardFactor + Vector3.up * headScale * eatMouthUpFactor;
                eatingTarget.position = Vector3.Lerp(eatingTarget.position, mouthPos, eatBlend);
            }
        }

        // Car-eat overhead lift + head tilt -- same layered approach as the plain eat lift
        // above, just a bigger arm swing (the car needs to clear the giant's own head) plus a
        // head tilt to look up at what it's holding.
        if (carLiftBlend > 0.001f)
        {
            ResolveGripBones();
            if (gripUpperArmBone != null)
            {
                gripUpperArmBone.rotation = Quaternion.AngleAxis(-carLiftUpperArmAngle * carLiftBlend, transform.right) * gripUpperArmBone.rotation;
            }
            if (gripLowerArmBone != null)
            {
                gripLowerArmBone.rotation = Quaternion.AngleAxis(-carLiftForearmAngle * carLiftBlend, transform.right) * gripLowerArmBone.rotation;
            }
            if (gripHeadBone != null)
            {
                gripHeadBone.rotation = Quaternion.AngleAxis(-carLiftHeadTiltAngle * carLiftBlend, transform.right) * gripHeadBone.rotation;
            }
        }
    }

    // Keeps the target rigidly parented to the hand (still riding along with the Throw wind-up,
    // exactly like it does the rest of the time it's held) for throwReleaseDelay seconds after
    // the trigger fires, then detaches and launches it -- so the object leaves the hand in sync
    // with the animation's actual release frame instead of snapping away the instant the button
    // is pressed. capturedAimPoint was captured back in ThrowHeldTarget, at the moment the trigger
    // fired (so the TARGET doesn't drift if the camera moves during the wind-up); the DIRECTION
    // to it is computed fresh right here, from wherever the hand actually is after riding out the
    // whole throw animation -- see the comment in ThrowHeldTarget for why those need to be two
    // separate steps.
    IEnumerator ReleaseThrownTargetAfterDelay(Transform target, ThirdPersonCamera camRef, Vector3 capturedAimPoint, Transform capturedAimActor)
    {
        yield return new WaitForSeconds(throwReleaseDelay);

        if (camRef != null)
        {
            camRef.SetAiming(false);
        }
        if (aimMarker != null)
        {
            aimMarker.SetActive(false);
        }

        if (target == null)
        {
            yield break;
        }

        // capturedAimActor is null if the raycast hit static geometry (or nothing) rather than
        // a live target -- the fixed point still applies then, nothing to lead. It also just
        // naturally reads as null again if that actor was destroyed in the meantime (Unity's
        // == treats a destroyed object as null), which correctly falls back to the last point
        // it was at.
        Vector3 effectiveAimPoint = capturedAimActor != null ? capturedAimActor.position : capturedAimPoint;

        Vector3 launchDir = (effectiveAimPoint - target.position).normalized;
        target.SetParent(null, true);

        StartCoroutine(ThrowArcRoutine(target, launchDir * throwSpeed));

        if (camRef != null)
        {
            camRef.AddShake(0.2f);
        }
    }

    // Flies a just-thrown target through a simple gravity arc -- no Rigidbody/physics involved,
    // consistent with how everything else in this game moves directly via transform (see e.g.
    // TrafficCarAI) -- until it comes back down to ground level, then lands it with
    // FinalizeRelease exactly as a plain (non-thrown) release would.
    IEnumerator ThrowArcRoutine(Transform obj, Vector3 launchVelocity)
    {
        Vector3 velocity = launchVelocity;

        while (obj != null && obj.position.y > 0f)
        {
            velocity += Vector3.down * throwGravity * Time.deltaTime;
            obj.position += velocity * Time.deltaTime;

            // Mid-flight impact: a thrown person or vehicle that clips another vehicle or a
            // living person/soldier on the way down destroys whatever it hit on the spot instead
            // of sailing straight through it, and the thrown target itself is destroyed right
            // along with it.
            Transform hitTarget = FindThrowHitAt(obj.position, obj);
            if (hitTarget != null)
            {
                ApplyThrowImpact(hitTarget, obj);
                DestroyThrownOrHit(obj);
                yield break;
            }

            yield return null;
        }

        if (obj == null)
        {
            yield break;
        }

        Vector3 landPos = obj.position;
        landPos.y = 0f;
        obj.position = landPos;

        // One more check right at the landing spot -- it can land squarely on a vehicle or a
        // person even if the arc's own per-frame steps never quite overlapped it -- then the
        // thrown target itself is always destroyed on impact. Unlike a plain drop/release
        // (FinalizeRelease), a *thrown* person or vehicle never just lands safely.
        Transform hitOnLand = FindThrowHitAt(landPos, obj);
        if (hitOnLand != null)
        {
            ApplyThrowImpact(hitOnLand, obj);
        }

        DestroyThrownOrHit(obj);
    }

    // A small radius around a thrown target's current position, checked for any OTHER vehicle
    // or living person/soldier in the way (excluding the thrown target's own colliders, in
    // case a vehicle or person is being thrown at another one) -- used both every step of
    // ThrowArcRoutine's arc and once more at the landing spot.
    Transform FindThrowHitAt(Vector3 position, Transform thrown)
    {
        Collider[] hits = Physics.OverlapSphere(position, throwImpactRadius);
        foreach (Collider c in hits)
        {
            if (c.transform == thrown || c.transform.IsChildOf(thrown))
            {
                continue;
            }

            TrafficCarAI traffic = c.GetComponentInParent<TrafficCarAI>();
            if (traffic != null) return traffic.transform;
            PoliceCarAI police = c.GetComponentInParent<PoliceCarAI>();
            if (police != null) return police.transform;
            TankAI tank = c.GetComponentInParent<TankAI>();
            if (tank != null) return tank.transform;
            HelicopterAI heli = c.GetComponentInParent<HelicopterAI>();
            if (heli != null) return heli.transform;
            TinyNPC npc = c.GetComponentInParent<TinyNPC>();
            if (npc != null) return npc.transform;
            TinySoldierAI soldier = c.GetComponentInParent<TinySoldierAI>();
            if (soldier != null) return soldier.transform;
        }
        return null;
    }

    // Whether this is a thrown PERSON (TinyNPC/TinySoldierAI) rather than a thrown vehicle --
    // see ApplyThrowImpact.
    bool IsPersonTransform(Transform t)
    {
        if (t == null) { return false; }
        return t.GetComponent<TinyNPC>() != null || t.GetComponent<TinySoldierAI>() != null;
    }

    // "Car" specifically -- TrafficCarAI/PoliceCarAI -- not tanks/helicopters, which don't
    // get the car-eat overhead-shake spectacle (see TickAttackInput/CarEatRoutine).
    bool IsCarTransform(Transform t)
    {
        if (t == null) { return false; }
        return t.GetComponent<TrafficCarAI>() != null || t.GetComponent<PoliceCarAI>() != null;
    }

    // Applies a thrown object's impact to whatever it actually hit. A thrown VEHICLE destroys
    // anything it hits outright, same as always (DestroyThrownOrHit). A thrown PERSON hitting a
    // tank/police car/helicopter only damages it (thrownPersonVehicleDamage) instead of
    // destroying it outright -- a person is far lighter than another vehicle, so it shouldn't
    // one-shot something with a real health pool. Ambient traffic cars have no health pool of
    // their own to partially damage (see TrafficCarAI.Squash), so they -- and anything else a
    // thrown person hits, like another person -- still go through DestroyThrownOrHit as before.
    void ApplyThrowImpact(Transform hit, Transform thrown)
    {
        if (hit == null)
        {
            return;
        }

        if (IsPersonTransform(thrown))
        {
            TankAI tank = hit.GetComponent<TankAI>();
            if (tank != null) { tank.TakeDamage(thrownPersonVehicleDamage); return; }

            PoliceCarAI police = hit.GetComponent<PoliceCarAI>();
            if (police != null) { police.TakeDamage(thrownPersonVehicleDamage); return; }

            HelicopterAI heli = hit.GetComponent<HelicopterAI>();
            if (heli != null) { heli.TakeDamage(thrownPersonVehicleDamage); return; }
        }

        DestroyThrownOrHit(hit);
    }

    // Destroys whatever was thrown or whatever it hit, per its own type -- a vehicle explodes
    // (reusing each AI script's existing TakeDamage/Explode, or TrafficCarAI's own Squash, the
    // exact same ones stomping already uses), while a tiny person/soldier just dies (Squash)
    // rather than exploding. Safe to call on something already destroyed, since every one of
    // these methods already no-ops in that case. Falls back to a normal safe landing
    // (FinalizeRelease) for any target type none of these recognize, so nothing thrown is ever
    // left stuck mid-air with no collider or AI re-enabled.
    // Plays the hand-to-mouth motion (LateUpdate, driven by eatBlend) then consumes the held
    // person at the peak, exactly like any other kill (DestroyThrownOrHit -> Squash): same
    // Blood VFX-at-the-hand and same +1 XP via GiantProgression, so eating someone rewards the
    // giant the same amount a stomp or thrown hit would rather than needing its own separate
    // reward system.
    IEnumerator EatRoutine(Transform target)
    {
        isAttacking = true;
        isEating = true;
        eatingTarget = target;

        float t = 0f;
        while (t < eatRiseDuration)
        {
            t += Time.deltaTime;
            eatBlend = Mathf.Clamp01(t / eatRiseDuration);
            yield return null;
        }
        eatBlend = 1f;

        yield return new WaitForSeconds(eatHoldAtMouthDuration);

        if (target == heldTarget)
        {
            heldTarget = null;
            heldIsHostage = false;
        }
        DestroyThrownOrHit(target);
        eatingTarget = null;
        if (giantHealth != null)
        {
            giantHealth.Heal(eatHealAmount);
        }

        ThirdPersonCamera camRef = GetCam();
        if (camRef != null)
        {
            camRef.AddShake(0.15f);
        }

        t = 0f;
        while (t < eatFallDuration)
        {
            t += Time.deltaTime;
            eatBlend = 1f - Mathf.Clamp01(t / eatFallDuration);
            yield return null;
        }
        eatBlend = 0f;

        isEating = false;
        isAttacking = false;
    }

    // Hoists a held car up overhead (LateUpdate, driven by carLiftBlend), shakes a passenger
    // out of it partway through (SpawnAndEatCarPassenger) who falls into the giant's mouth and
    // gets eaten (same Squash()-based reward as EatRoutine), then lowers the car back down to
    // its normal held position. The car itself is untouched -- still held afterward, so it
    // can still be thrown/dropped/eaten-from-again like normal.
    IEnumerator CarEatRoutine(Transform car)
    {
        isAttacking = true;
        isCarLifting = true;

        float t = 0f;
        while (t < carLiftRiseDuration)
        {
            t += Time.deltaTime;
            carLiftBlend = Mathf.Clamp01(t / carLiftRiseDuration);
            yield return null;
        }
        carLiftBlend = 1f;

        if (car != null)
        {
            SpawnAndEatCarPassenger(car);
        }

        yield return new WaitForSeconds(carLiftHoldDuration);

        t = 0f;
        while (t < carLiftFallDuration)
        {
            t += Time.deltaTime;
            carLiftBlend = 1f - Mathf.Clamp01(t / carLiftFallDuration);
            yield return null;
        }
        carLiftBlend = 0f;

        isCarLifting = false;
        isAttacking = false;
    }

    // Spawns a throwaway visual "passenger" at the held car's current position (using the
    // same prefab/scale/animator-controller the city itself spawns ambient tiny people from --
    // see CityGenerator) and starts it falling toward the giant's mouth. This person was never
    // part of the simulation before now -- the car itself has no real driver entity -- it
    // exists purely for this one shake-and-eat beat and is destroyed at the end of it.
    void SpawnAndEatCarPassenger(Transform car)
    {
        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        if (cityGen == null || cityGen.tinyPersonPrefab == null)
        {
            return;
        }

        GameObject person = Instantiate(cityGen.tinyPersonPrefab);
        person.name = "CarEjectedPerson";
        person.transform.position = car.position;
        person.transform.rotation = Quaternion.identity;
        person.transform.localScale = Vector3.one * cityGen.tinyScale;

        Animator personAnim = person.GetComponent<Animator>();
        if (personAnim == null)
        {
            personAnim = person.GetComponentInChildren<Animator>();
        }
        if (personAnim != null && cityGen.tinyAnimatorController != null)
        {
            personAnim.runtimeAnimatorController = cityGen.tinyAnimatorController;
        }

        // Added dynamically (same as CityGenerator's own ambient spawn) purely so Squash() --
        // XP reward, cleanup -- is available; immediately disabled since its own wander/flee
        // Update() loop should never run for this short-lived visual-only instance (it's
        // driven manually by CarPassengerFallRoutine instead).
        TinyNPC npc = person.AddComponent<TinyNPC>();
        npc.enabled = false;

        StartCoroutine(CarPassengerFallRoutine(person.transform, npc));
    }

    // Manually falls the ejected passenger from wherever it spawned (the held car, up near the
    // overhead-lifted hand) down to the giant's head/mouth, then consumes it exactly like
    // EatRoutine does -- same Squash() call, so it's worth the same +1 XP as any other kill.
    IEnumerator CarPassengerFallRoutine(Transform person, TinyNPC npc)
    {
        Vector3 startPos = person != null ? person.position : transform.position;
        float t = 0f;
        while (t < carEjectFallDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / carEjectFallDuration);
            Vector3 mouthPos = gripHeadBone != null ? gripHeadBone.position : (transform.position + Vector3.up * 2f);
            if (person != null)
            {
                person.position = Vector3.Lerp(startPos, mouthPos, p);
            }
            yield return null;
        }

        if (npc != null)
        {
            npc.Squash();
            if (giantHealth != null)
            {
                giantHealth.Heal(eatHealAmount);
            }
        }

        ThirdPersonCamera camRef = GetCam();
        if (camRef != null)
        {
            camRef.AddShake(0.15f);
        }
    }

    void DestroyThrownOrHit(Transform t)
    {
        if (t == null)
        {
            return;
        }

        TrafficCarAI traffic = t.GetComponent<TrafficCarAI>();
        if (traffic != null) { traffic.Squash(); return; }

        TankAI tank = t.GetComponent<TankAI>();
        if (tank != null) { tank.TakeDamage(float.MaxValue); return; }

        PoliceCarAI police = t.GetComponent<PoliceCarAI>();
        if (police != null) { police.TakeDamage(float.MaxValue); return; }

        HelicopterAI heli = t.GetComponent<HelicopterAI>();
        if (heli != null) { heli.TakeDamage(float.MaxValue); return; }

        TinyNPC npc = t.GetComponent<TinyNPC>();
        if (npc != null) { npc.Squash(); return; }

        TinySoldierAI soldier = t.GetComponent<TinySoldierAI>();
        if (soldier != null) { soldier.Squash(); return; }

        FinalizeRelease(t);
    }

    // Shared "landing" logic for a target that's no longer being held: re-enables its
    // collider(s) and its own AI/movement script (or, for an ambient traffic car, hands it off
    // to TrafficCarAI.BeginDrop for its own softer landing sequence instead) right where it
    // currently sits. Called both by an in-place release (ReleaseHeldTarget -- current position
    // is wherever the hand was) and by a completed throw arc (ThrowArcRoutine -- current
    // position is wherever the throw landed).
    void FinalizeRelease(Transform released)
    {
        // Ambient traffic cars get a softer, distinct landing (see TrafficCarAI.BeginDrop): a
        // gentle fall instead of snapping straight to the road if there's still height left to
        // cover, colliders held off through a brief stunned pause (so landing right at the
        // giant's feet can't trigger an instant stomp-squash), then it drives off again on its
        // own. It also has to go back under the city's own root -- not the scene root -- since
        // its movement math (CityGenerator.GridToLocal) is expressed relative to that parent.
        TrafficCarAI traffic = released.GetComponent<TrafficCarAI>();
        if (traffic != null && traffic.cityGen != null && traffic.cityGen.CityRoot != null)
        {
            released.SetParent(traffic.cityGen.CityRoot, true);
            traffic.enabled = true;
            traffic.BeginDrop();
        }
        else
        {
            released.SetParent(null, true);

            Vector3 dropPos = released.position;
            dropPos.y = 0.1f;
            released.position = dropPos;
            released.rotation = Quaternion.identity;

            Collider[] cols = released.GetComponentsInChildren<Collider>();
            foreach (Collider c in cols)
            {
                c.enabled = true;
            }

            TinyNPC tiny = released.GetComponent<TinyNPC>();
            if (tiny != null)
            {
                tiny.enabled = true;
            }

            TinySoldierAI soldier = released.GetComponent<TinySoldierAI>();
            if (soldier != null)
            {
                soldier.enabled = true;
            }

            PoliceCarAI police = released.GetComponent<PoliceCarAI>();
            if (police != null)
            {
                police.enabled = true;
            }

            TankAI tank = released.GetComponent<TankAI>();
            if (tank != null)
            {
                tank.enabled = true;
            }

            HelicopterAI heli = released.GetComponent<HelicopterAI>();
            if (heli != null)
            {
                heli.enabled = true;
            }

            if (traffic != null)
            {
                traffic.enabled = true;
            }
        }
    }

    // Resolves and caches the birdcage by name if it wasn't assigned in the Inspector.
    Transform ResolveBirdCage()
    {
        if (birdCage != null)
        {
            return birdCage;
        }

        GameObject found = GameObject.Find("YardTreeBirdCage");
        if (found != null)
        {
            birdCage = found.transform;
        }
        return birdCage;
    }

    // Resolves (creating once if needed, at the cage's own origin) the point inside the cage
    // where a caged target is placed. Drag this object into position in the Scene view later
    // to fine-tune exactly where inside the cage things end up, the same way GrabHoldAnchor
    // works for the hand.
    Transform ResolveCageInsidePoint()
    {
        if (cageInsidePoint != null)
        {
            return cageInsidePoint;
        }

        Transform cage = ResolveBirdCage();
        if (cage == null)
        {
            return null;
        }

        GameObject go = new GameObject("CageInsidePoint");
        go.transform.SetParent(cage, false);
        go.transform.localPosition = Vector3.zero;
        cageInsidePoint = go.transform;
        return cageInsidePoint;
    }

    // True once the giant has actually carried a held target close enough to the birdcage to
    // cage it. Delegates to IsGiantNearCage (the giant's OWN position) rather than measuring
    // from the held target's position: a held target rides along at hand height only a small
    // arm's-length from the giant's body, so using the giant's own position is just as
    // accurate and -- importantly -- matches the exact same proximity test the empty-hand
    // Hostage Shop check uses (IsGiantNearCage), so "close enough to the cage" means the same
    // thing whether the giant's hand is full or empty, from whichever side it approaches.
    bool IsHeldTargetNearCage()
    {
        return heldTarget != null && IsGiantNearCage();
    }

    // Moves the currently-held target off the hand and into the cage (triggered by left-click
    // when near the birdcage -- see TickAttackInput): reparents it under
    // cageInsidePoint (with the same lossyScale-compensation trick used for the hand, so it
    // doesn't balloon or shrink), then immediately frees the hand (heldTarget = null) so the
    // giant can go grab someone else right away. The caged target's collider/AI script stay
    // disabled exactly as they were while held, so it can't be hit or re-grabbed once caged.
    void CageHeldTarget()
    {
        if (heldTarget == null)
        {
            return;
        }

        Transform insidePoint = ResolveCageInsidePoint();
        if (insidePoint == null)
        {
            return;
        }

        Transform caged = heldTarget;
        heldTarget = null;

        Vector3 worldScaleBeforeCage = caged.lossyScale;
        caged.SetParent(insidePoint, false);
        caged.localPosition = cageInsideLocalOffset;
        caged.localRotation = Quaternion.Euler(cageInsideLocalRotationEuler);

        Vector3 parentScale = insidePoint.lossyScale;
        caged.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? caged.localScale.x : worldScaleBeforeCage.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? caged.localScale.y : worldScaleBeforeCage.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? caged.localScale.z : worldScaleBeforeCage.z / parentScale.z
        );

        cagedHostages.Add(caged);
        UpdateHostageUI();

        CagedHostageWander wander = caged.gameObject.AddComponent<CagedHostageWander>();
        wander.Initialize(ComputeCageWanderRadius());

        ThirdPersonCamera camRef = GetCam();
        if (camRef != null)
        {
            camRef.AddShake(0.2f);
        }
    }

    // Computes how far (world units) a caged hostage can wander from the spot it's placed
    // at, based on the birdcage's own interior footprint (the BoxCollider
    // YardTreeBuilder.AddCageCollider adds), so hostages roam a sensible area for however
    // big or small the cage actually is instead of a hardcoded distance that might poke
    // them through the bars.
    float ComputeCageWanderRadius()
    {
        Transform cage = ResolveBirdCage();
        if (cage == null) return 1f;

        BoxCollider box = cage.GetComponent<BoxCollider>();
        if (box == null) return 1f;

        Vector3 worldSize = Vector3.Scale(box.size, cage.lossyScale);
        float smallerSide = Mathf.Min(worldSize.x, worldSize.z);
        return Mathf.Max(smallerSide * 0.3f, 0.15f);
    }

    // How many caged hostages are currently available to spend at the Hostage Shop.
    public int HostageCount => cagedHostages.Count;

    // Keeps the HUD's hostage-count text (see hostageCountText) in sync -- called once at
    // Start() and again any time cagedHostages actually changes (caging one, spending one).
    void UpdateHostageUI()
    {
        if (hostageCountText != null)
        {
            hostageCountText.text = cagedHostages.Count.ToString();
        }
    }

    // Spends (removes and destroys) `amount` caged hostages, newest first. Returns false and
    // changes nothing if there aren't enough -- an all-or-nothing purchase never partially
    // spends hostages it can't fully pay with.
    public bool SpendHostages(int amount)
    {
        if (amount <= 0) return true;
        if (cagedHostages.Count < amount) return false;

        for (int i = 0; i < amount; i++)
        {
            int lastIndex = cagedHostages.Count - 1;
            Transform hostage = cagedHostages[lastIndex];
            cagedHostages.RemoveAt(lastIndex);
            if (hostage != null)
            {
                Destroy(hostage.gameObject);
            }
        }
        UpdateHostageUI();
        return true;
    }

    // Read-only view of the currently caged hostages, exposed for the Hostage list menu
    // (see HostageShopMenu) to enumerate and let the player pick specific ones to sell.
    public System.Collections.Generic.IReadOnlyList<Transform> CagedHostages => cagedHostages;

    // Removes and destroys exactly the given caged hostages (used by the Hostage list menu's
    // Sell button for a specific multi-selection, unlike SpendHostages' newest-first
    // all-or-nothing spend). Hostages no longer actually caged (already sold/removed elsewhere)
    // are silently skipped. Returns how many were actually removed, so the caller can pay out
    // coins per hostage actually sold.
    public int SellHostages(System.Collections.Generic.IEnumerable<Transform> hostagesToSell)
    {
        if (hostagesToSell == null) return 0;

        int sold = 0;
        foreach (Transform hostage in hostagesToSell)
        {
            if (hostage == null) continue;
            if (!cagedHostages.Remove(hostage)) continue;

            Destroy(hostage.gameObject);
            sold++;
        }

        if (sold > 0)
        {
            UpdateHostageUI();
        }

        return sold;
    }

    // Re-cages `count` hostages without needing an actual grab-and-carry -- used by
    // SaveSystem.LoadFromSlot right after CityGenerator.RegenerateFromSeed rebuilds the map,
    // so a loaded save's birdcage isn't left empty just because the scene had to regenerate
    // its citizens from scratch. Pulls from the freshly-spawned "Tiny" population (skipping
    // giant-immune named NPCs like BlackMan) and puts each one through the same
    // disable-collider-and-AI + reparent-into-the-cage steps a real grab-then-cage would, via
    // TryGrabBelow/AttachToHand/CageHeldTarget above.
    public void RestoreCagedHostages(int count)
    {
        if (count <= 0) return;

        Transform insidePoint = ResolveCageInsidePoint();
        if (insidePoint == null) return;

        GameObject[] candidates = GameObject.FindGameObjectsWithTag("Tiny");
        int taken = 0;

        foreach (GameObject go in candidates)
        {
            if (taken >= count) break;
            if (go == null) continue;

            TinyNPC tinyComp = go.GetComponent<TinyNPC>();
            if (tinyComp != null && tinyComp.giantImmune) continue;
            if (tinyComp == null) continue; // only regular citizens are restored, not soldiers

            Transform t = go.transform;

            tinyComp.enabled = false;

            Collider[] cols = t.GetComponentsInChildren<Collider>();
            foreach (Collider c in cols)
            {
                c.enabled = false;
            }

            Vector3 worldScaleBefore = t.lossyScale;
            t.SetParent(insidePoint, false);
            t.localPosition = cageInsideLocalOffset;
            t.localRotation = Quaternion.Euler(cageInsideLocalRotationEuler);

            Vector3 parentScale = insidePoint.lossyScale;
            t.localScale = new Vector3(
                Mathf.Approximately(parentScale.x, 0f) ? t.localScale.x : worldScaleBefore.x / parentScale.x,
                Mathf.Approximately(parentScale.y, 0f) ? t.localScale.y : worldScaleBefore.y / parentScale.y,
                Mathf.Approximately(parentScale.z, 0f) ? t.localScale.z : worldScaleBefore.z / parentScale.z
            );

            cagedHostages.Add(t);

            CagedHostageWander wander = t.gameObject.AddComponent<CagedHostageWander>();
            wander.Initialize(ComputeCageWanderRadius());

            taken++;
        }

        if (taken > 0)
        {
            UpdateHostageUI();
        }
    }

    // True once the giant itself (not a held target -- there is none in this case) has
    // walked close enough to the birdcage to interact with it, e.g. to open the Hostage Shop
    // with an empty hand. Same horizontal-only distance check as IsHeldTargetNearCage, just
    // measured from the giant's own position instead of a held target's.
    bool IsGiantNearCage()
    {
        Transform cage = ResolveBirdCage();
        if (cage == null)
        {
            return false;
        }

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = cage.position; b.y = 0f;
        return Vector3.Distance(a, b) <= cageInteractRadius;
    }

    // Small wrapper so TickAttackInput reads cleanly: returns false (do nothing / fall
    // through to a normal grab) if the shop isn't in the scene or refuses to open (e.g. some
    // other menu already has the game paused).
    bool TryOpenHostageShop()
    {
        return HostageShopMenu.Instance != null && HostageShopMenu.Instance.TryOpen();
    }

    // True while the mouse pointer is over any active UI element (a button, a panel, ...).
    // Used to keep world-space mouse clicks (grab/cage/attack) from also firing underneath a
    // UI click on the same frame.
    static bool IsPointerOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    // Called by a mouse-opened/closed popup (currently just HostageShopMenu) right as it
    // closes: clears any click already queued up this frame and ignores mouse input for a
    // short moment afterward, so the exact click that closed the popup can never also be read
    // as a fresh world-space action (which would otherwise immediately reopen it -- see
    // TickAttackInput). Uses real/unscaled time so it still counts down correctly whether or
    // not something else has the game paused.
    public void SuppressAttackInputBriefly(float seconds = 0.2f)
    {
        pendingAttackButton = PendingAttackButton.None;
        inputSuppressUntil = Time.unscaledTime + seconds;
    }

    // Prefers the giant's own right-hand bone (this rig is Humanoid), so a held target moves
    // naturally with the actual hand as the giant walks/turns/attacks instead of floating at a
    // fixed offset from the body. Falls back to a fixed point above/in front of the body if the
    // rig isn't Humanoid or the bone can't be resolved. Cached in handHoldPoint once resolved.
    Transform ResolveHandHoldPoint()
    {
        if (handHoldPoint != null)
        {
            return handHoldPoint;
        }

        if (animator != null && animator.isHuman)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null)
            {
                handHoldPoint = hand;
                return handHoldPoint;
            }
        }

        GameObject go = new GameObject("HandHoldPoint");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(1.8f, 6.5f, 3f);
        handHoldPoint = go.transform;
        return handHoldPoint;
    }

    // Looked up once and cached (Humanoid finger bones never change identity at runtime).
    // Missing bones are simply left null and skipped by UpdateHeldTargetPose.
    void ResolveGripBones()
    {
        if (gripBonesResolved)
        {
            return;
        }
        gripBonesResolved = true;

        if (animator != null && animator.isHuman)
        {
            gripHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            gripIndexBone = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            gripMiddleBone = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            gripRingBone = animator.GetBoneTransform(HumanBodyBones.RightRingProximal);
            gripLittleBone = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            gripChestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (gripChestBone == null)
            {
                gripChestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            }
            gripUpperArmBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            gripLowerArmBone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            gripHeadBone = animator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    // Called once from AttachToHand, at the exact moment of the grab -- computes where
    // "inside the curled fingers" is right now, from the live finger-base bones, and expresses
    // it as a local offset under the hand bone (hold). No ongoing per-frame cost: after this,
    // the held target is just a normal child transform riding along with the hand bone.
    Vector3 ComputeHandLocalHoldOffset(Transform hold)
    {
        ResolveGripBones();

        if (gripHandBone == null)
        {
            return grabLocalOffset;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;
        if (gripIndexBone != null) { sum += gripIndexBone.position; count++; }
        if (gripMiddleBone != null) { sum += gripMiddleBone.position; count++; }
        if (gripRingBone != null) { sum += gripRingBone.position; count++; }
        if (gripLittleBone != null) { sum += gripLittleBone.position; count++; }

        Vector3 worldPos = gripHandBone.position;
        if (count > 0)
        {
            Vector3 fingerCentroid = sum / count;
            // Blend mostly toward the finger-base centroid rather than the raw hand-bone pivot
            // (which sits on the back of the hand), so the target lands inside the curled
            // fingers/palm instead of on the back of the hand.
            worldPos = Vector3.Lerp(gripHandBone.position, fingerCentroid, 0.65f);
        }

        // Pull the hold point in toward the giant's own torso, so the held target hugs the
        // body instead of sitting out at the hand's natural arm's-length position. Falls back
        // to a point straight above the giant's own root (same height as the hand) if no
        // chest/upper-chest bone could be resolved.
        Vector3 bodyAnchor = gripChestBone != null ? gripChestBone.position : (transform.position + Vector3.up * worldPos.y);
        worldPos = Vector3.Lerp(worldPos, bodyAnchor, grabBodyPullRatio);

        return hold.InverseTransformPoint(worldPos) + grabLocalOffset;
    }

    void ApplyAttackDamage(bool knockback, bool isCombo)
    {
        // Make sure the physics world reflects the latest transform positions (tiny people/vehicles
        // move themselves via transform each Update) before querying for what's in range.
        Physics.SyncTransforms();

        // The combo punch reaches farther and sweeps a wider radius than a single punch, so it
        // visibly and mechanically hits more of what's in front of the giant.
        float range = isCombo ? attackRangeCombo : attackRange;
        float radius = isCombo ? attackRadiusCombo : attackRadius;

        Vector3 attackPoint = transform.position + transform.forward * range + Vector3.up * attackHeight;

        // The knockback (left-click) punch only shows the new arm-swing fan -- the old
        // ground-impact ring is reserved for the right-click/combo punches, which don't get
        // the fan, so every punch still has some visible impact cue. The ring is sized off the
        // same `radius` used for the hit sweep, so the combo's bigger reach also reads as a
        // visibly bigger impact.
        ThirdPersonCamera camRef = GetCam();
        if (camRef != null)
        {
            camRef.AddShake(attackShakeAmount);
        }

        if (knockback)
        {
            Vector3 armSwingCenter = transform.position + transform.forward * armSwingForwardOffset;
            ArmSwingEffect.Spawn(armSwingCenter, transform.forward, range, attackHeight, 130f, 0.2f);
        }
        else
        {
            Vector3 ringGroundPos = new Vector3(attackPoint.x, transform.position.y + 0.05f, attackPoint.z);
            float ringVisualRadius = isCombo ? radius * comboRingVisualScale : radius;
            StompRing.Spawn(ringGroundPos, ringVisualRadius);
        }

        Collider[] hits = Physics.OverlapSphere(attackPoint, radius);

        var hitTargets = new System.Collections.Generic.HashSet<object>();

        foreach (Collider col in hits)
        {
            // Damage scales up toward the center of the impact sweep (attackPoint) and down
            // toward the outer edge of its radius -- a clean center hit lands noticeably
            // harder than a glancing one caught at the rim. attackDamageEdgeMultiplier is
            // tuned so even the weakest (edge) hit still clearly outdamages the passive
            // per-second building-smash damage from just walking into something
            // (smashDamagePerSecond), so a real right-click/combo punch always reads as
            // strictly more impactful than idly bumping into buildings.
            float distFromCenter = Vector3.Distance(col.bounds.center, attackPoint);
            float centerT = radius > 0f ? Mathf.Clamp01(distFromCenter / radius) : 0f;
            float damage = attackDamage * Mathf.Lerp(attackDamageCenterMultiplier, attackDamageEdgeMultiplier, centerT);

            TinyNPC tiny = col.GetComponentInParent<TinyNPC>();
            if (tiny != null)
            {
                if (hitTargets.Add(tiny))
                {
                    tiny.TakeDamage(damage);
                    if (knockback) tiny.ApplyKnockback(KnockbackDirTo(tiny.transform.position) * attackKnockbackForce);
                }
                continue;
            }

            TinySoldierAI soldier = col.GetComponentInParent<TinySoldierAI>();
            if (soldier != null)
            {
                if (hitTargets.Add(soldier))
                {
                    soldier.TakeDamage(damage);
                    if (knockback) soldier.ApplyKnockback(KnockbackDirTo(soldier.transform.position) * attackKnockbackForce);
                }
                continue;
            }

            PoliceCarAI police = col.GetComponentInParent<PoliceCarAI>();
            if (police != null)
            {
                if (hitTargets.Add(police))
                {
                    police.TakeDamage(damage);
                    if (knockback) police.ApplyKnockback(KnockbackDirTo(police.transform.position) * attackKnockbackForce);
                }
                continue;
            }

            TankAI tank = col.GetComponentInParent<TankAI>();
            if (tank != null)
            {
                if (hitTargets.Add(tank))
                {
                    tank.TakeDamage(damage);
                    if (knockback) tank.ApplyKnockback(KnockbackDirTo(tank.transform.position) * attackKnockbackForce);
                }
                continue;
            }

            HelicopterAI heli = col.GetComponentInParent<HelicopterAI>();
            if (heli != null)
            {
                if (hitTargets.Add(heli))
                {
                    heli.TakeDamage(damage);
                    if (knockback) heli.ApplyKnockback(KnockbackDirTo(heli.transform.position) * attackKnockbackForce);
                }
                continue;
            }
        }
    }

    // Horizontal direction from the giant to a hit target, used to shove that target directly
    // away regardless of where inside the attack sweep it stood. Falls back to the giant's
    // forward facing on the (essentially impossible) case the target sits exactly on the giant.
    Vector3 KnockbackDirTo(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
    }

    // Called by SaveSystem.LoadFromSlot right after repositioning the giant on a Load, so
    // any vertical fall speed left over from before the load (e.g. carried over from
    // whatever the giant was doing right before saving, or from the load's own city-rebuild
    // taking a moment) doesn't survive the teleport and immediately slam the giant down --
    // or, at high enough speed in a single inflated frame, straight through -- the ground the
    // instant gravity resumes next frame.
    public void ResetFallVelocity()
    {
        velocity = Vector3.zero;
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

    void OnDrawGizmosSelected()
    {
        Vector3 attackPoint = transform.position + transform.forward * attackRange + Vector3.up * attackHeight;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint, attackRadius);

        Vector3 comboPoint = transform.position + transform.forward * attackRangeCombo + Vector3.up * attackHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(comboPoint, attackRadiusCombo);
    }
}
