using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform CharacterBody;
    [SerializeField] private Transform CameraArm;
    [SerializeField] private Transform cameraTransform; // Main Camera
    [SerializeField] private PlayerCombat combat;

    [Header("Lock-On")]
    [SerializeField] private KeyCode lockOnKey = KeyCode.Q;
    [SerializeField] private float lockOnRadius = 15f;
    [SerializeField] private LayerMask lockOnLayer;
    [SerializeField] private float lockOnDirectionDeadzone = 0.2f;

    [Header("Camera")]
    [SerializeField] private float cameraTargetDistance = 4.5f;
    [SerializeField] private float cameraMinDistance = 1.0f;
    [SerializeField] private float cameraMaxDistance = 6.0f;
    [SerializeField] private float cameraHeight = 1.6f;
    [SerializeField] private float cameraCollisionRadius = 0.25f;
    [SerializeField] private LayerMask cameraCollisionMask;
    [SerializeField] private float yawSmoothTime = 0.03f;
    [SerializeField] private float pitchSmoothTime = 0.03f;
    [SerializeField] private float distSmoothTime = 0.05f;

    [Header("Movement")]
    [SerializeField] private float turnSpeed = 12f;
    [SerializeField] public float walkSpeed = 5f;
    [SerializeField] public float runSpeed = 10f;
    [SerializeField] public float jumpPower = 10f;
    [SerializeField] public float sensitivity = 2f;

    [Header("Pitch Clamp")]
    public float minPitch = -40f;
    public float maxPitch = 60f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeRadius = 0.28f;
    [SerializeField] private float groundProbeOffsetY = 0.2f;
    [SerializeField] private float groundProbeDown = 0.05f;

    private float yaw = 0f, pitch = 10f;
    private float yawVel = 0f, pitchVel = 0f;
    private float currentDistance = 0f, distVel = 0f;

    private Transform lockOnTarget;
    private bool isLockedOn = false;

    private Animator animator;
    private Rigidbody rigid;
    private bool isGrounded = true;
    private bool isMoving = false;

    void Start()
    {
        animator = CharacterBody != null ? CharacterBody.GetComponent<Animator>() : null;
        rigid = GetComponent<Rigidbody>();
        rigid.interpolation = RigidbodyInterpolation.Interpolate;
        rigid.constraints = RigidbodyConstraints.FreezeRotation;

        if (cameraTransform == null) cameraTransform = Camera.main ? Camera.main.transform : null;

        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (combat != null)
        {
            if (combat.animator == null) combat.animator = animator;
            if (combat.rigid == null) combat.rigid = rigid;
            if (combat.characterBody == null) combat.characterBody = CharacterBody;
            if (combat.cameraArm == null) combat.cameraArm = CameraArm;
            if (combat.weapon != null && combat.weapon.owner == null)
                combat.weapon.owner = transform;
        }

        yaw = CharacterBody != null ? CharacterBody.eulerAngles.y : transform.eulerAngles.y;
        currentDistance = cameraTargetDistance;
        if (CameraArm != null)
        {
            CameraArm.position = CharacterBody.position + Vector3.up * cameraHeight;
            CameraArm.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        if (cameraTransform != null)
        {
            cameraTransform.position = CameraArm.position - CameraArm.forward * currentDistance;
            cameraTransform.rotation = CameraArm.rotation;
        }
    }

    private void Update()
    {
        HandleInput();
        Lookaround();
    }

    private void FixedUpdate()
    {
        UpdateGrounded();
        UpdateMovement();
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    private void HandleInput()
    {
        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        isMoving = moveInput.sqrMagnitude > 0.0001f;

        if (combat != null)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
                combat.TryAttack();

            if (Input.GetKeyDown(KeyCode.LeftAlt) && isGrounded)
                combat.TryDodge();

            if (Input.GetKeyDown(KeyCode.Mouse1) && isGrounded)
                combat.TryParry(isMoving);
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
            Jump();

        if (Input.GetKeyDown(lockOnKey))
        {
            if (isLockedOn) ClearLockOn();
            else AcquireLockOn();
        }
    }

    private void UpdateGrounded()
    {
        // 발 아래쪽으로 보정된 지점에서 체크
        Vector3 feet = transform.position - Vector3.up * (groundProbeOffsetY + groundProbeDown);
        isGrounded = Physics.CheckSphere(feet, groundProbeRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (isGrounded && animator != null && animator.GetBool("isJump"))
            animator.SetBool("isJump", false);
    }

    private void AcquireLockOn()
    {
        Vector3 center = transform.position;
        Collider[] hits = Physics.OverlapSphere(center, lockOnRadius, lockOnLayer, QueryTriggerInteraction.Collide);

        Transform best = null;
        float bestScore = float.MaxValue;
        Vector3 camFwd = CameraArm.forward;

        foreach (var h in hits)
        {
            if (h == null) continue;
            Transform candidate =
                h.attachedRigidbody != null ? h.attachedRigidbody.transform :
                (h.transform.root != null ? h.transform.root : h.transform);

            if (candidate == transform) continue;

            Vector3 to = candidate.position - (cameraTransform ? cameraTransform.position : center);
            float dist = to.magnitude;
            float angle = Vector3.Angle(camFwd, to);
            float score = dist + angle * 0.1f;

            if (score < bestScore) { bestScore = score; best = candidate; }
        }

        lockOnTarget = best;
        isLockedOn = lockOnTarget != null;
        combat?.SetLockOnTarget(lockOnTarget);
    }

    private void ClearLockOn()
    {
        isLockedOn = false;
        lockOnTarget = null;
        combat?.SetLockOnTarget(null);
    }

    private void UpdateMovement()
    {
        bool canMove = (combat == null) || !combat.IsBusy;

        if (!canMove)
        {
            isMoving = false;
            if (animator != null)
            {
                animator.SetBool("isWalk", false);
                animator.SetBool("isRun", false);
            }
            ClearDirectionalBools();
            return;
        }

        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        isMoving = moveInput.sqrMagnitude > 0.0001f;
        bool running = isMoving && Input.GetKey(KeyCode.LeftShift);

        if (animator != null)
        {
            animator.SetBool("isWalk", isMoving && !running);
            animator.SetBool("isRun", isMoving && running);
        }

        if (isMoving)
        {
            Vector3 moveDir = GetMoveDirection();
            float speed = running ? runSpeed : walkSpeed;

            Vector3 targetPos = rigid.position + moveDir * speed * Time.fixedDeltaTime;
            rigid.MovePosition(targetPos);

            bool isAttacking = (combat != null) && combat.IsAttacking;
            if (!isAttacking && CharacterBody != null)
            {
                if (isLockedOn && lockOnTarget != null)
                {
                    Vector3 to = lockOnTarget.position - CharacterBody.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.001f)
                    {
                        Quaternion t = Quaternion.LookRotation(to);
                        CharacterBody.rotation = Quaternion.Slerp(CharacterBody.rotation, t, turnSpeed * Time.fixedDeltaTime);
                    }
                }
                else
                {
                    Vector3 look = new Vector3(moveDir.x, 0f, moveDir.z);
                    if (look.sqrMagnitude > 0.0001f)
                    {
                        Quaternion t = Quaternion.LookRotation(look);
                        CharacterBody.rotation = Quaternion.Slerp(CharacterBody.rotation, t, turnSpeed * Time.fixedDeltaTime);
                    }
                }
            }

            UpdateLockOnDirectionalAnim(moveDir, canMove);
        }
        else
        {
            ClearDirectionalBools();

            bool isAttacking = (combat != null) && combat.IsAttacking;
            if (isLockedOn && lockOnTarget != null && !isAttacking && CharacterBody != null)
            {
                Vector3 to = lockOnTarget.position - CharacterBody.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f)
                {
                    Quaternion t = Quaternion.LookRotation(to);
                    CharacterBody.rotation = Quaternion.Slerp(CharacterBody.rotation, t, turnSpeed * Time.fixedDeltaTime);
                }
            }
        }
    }

    private void Jump()
    {
        rigid.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
        if (animator != null)
        {
            animator.SetBool("doJump", true);
            animator.SetBool("isJump", true);
        }
    }

    private Vector3 GetMoveDirection()
    {
        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector3 forward, right;

        if (isLockedOn && lockOnTarget != null)
        {
            forward = new Vector3(CharacterBody.forward.x, 0f, CharacterBody.forward.z).normalized;
            right = new Vector3(CharacterBody.right.x, 0f, CharacterBody.right.z).normalized;
        }
        else
        {
            forward = new Vector3(CameraArm.forward.x, 0f, CameraArm.forward.z).normalized;
            right = new Vector3(CameraArm.right.x, 0f, CameraArm.right.z).normalized;
        }
        return (forward * moveInput.y + right * moveInput.x).normalized;
    }

    private void Lookaround()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        float targetYaw = yaw + mouseX;
        float targetPitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);

        if (isLockedOn && lockOnTarget != null)
        {
            Vector3 toTarget = lockOnTarget.position - CharacterBody.position;
            toTarget.y = 0f;
            float desiredYaw = Quaternion.LookRotation(toTarget).eulerAngles.y;
            targetYaw = desiredYaw;
        }

        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVel, yawSmoothTime);
        pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVel, pitchSmoothTime);

        CameraArm.position = CharacterBody.position + Vector3.up * cameraHeight;
        CameraArm.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateCamera()
    {
        if (cameraTransform == null) return;

        float desiredDist = Mathf.Clamp(cameraTargetDistance, cameraMinDistance, cameraMaxDistance);
        Vector3 pivot = CameraArm.position;
        Vector3 desiredPos = pivot - CameraArm.forward * desiredDist;
        Vector3 dir = (desiredPos - pivot).normalized;

        if (Physics.SphereCast(pivot, cameraCollisionRadius, dir, out RaycastHit hit, desiredDist, cameraCollisionMask, QueryTriggerInteraction.Ignore))
            desiredDist = Mathf.Clamp(hit.distance - 0.05f, cameraMinDistance, cameraTargetDistance);

        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDist, ref distVel, distSmoothTime);

        cameraTransform.position = pivot - CameraArm.forward * currentDistance;
        cameraTransform.rotation = CameraArm.rotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isGrounded && animator != null && animator.GetBool("isJump"))
            animator.SetBool("isJump", false);
    }

    private void OnCollisionExit(Collision collision) { }

    private void ClearDirectionalBools()
    {
        if (animator == null) return;
        animator.SetBool("isLeft", false);
        animator.SetBool("isRight", false);
        animator.SetBool("isBack", false);
    }

    private void UpdateLockOnDirectionalAnim(Vector3 moveDir, bool canMove)
    {
        if (animator == null) return;

        if (!isLockedOn || lockOnTarget == null || !canMove || moveDir.sqrMagnitude < 0.0001f)
        {
            ClearDirectionalBools();
            return;
        }

        Vector3 fwd = CharacterBody.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = CharacterBody.right; right.y = 0f; right.Normalize();

        float f = Vector3.Dot(moveDir, fwd);
        float r = Vector3.Dot(moveDir, right);

        bool useForwardBack = Mathf.Abs(f) >= Mathf.Abs(r);
        bool goBack = useForwardBack && (f < -lockOnDirectionDeadzone);
        bool goLeft = !useForwardBack && (r < -lockOnDirectionDeadzone);
        bool goRight = !useForwardBack && (r > lockOnDirectionDeadzone);

        animator.SetBool("isBack", goBack);
        animator.SetBool("isLeft", goLeft);
        animator.SetBool("isRight", goRight);
    }
}
