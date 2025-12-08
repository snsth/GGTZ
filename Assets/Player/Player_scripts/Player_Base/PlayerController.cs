using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform CharacterBody;
    [SerializeField] private Transform CameraArm;
    [SerializeField] private Transform cameraTransform; // Main Camera 할당
    [SerializeField] private PlayerCombat combat;       // 추가: 전투 컴포넌트 참조

    [Header("Lock-On")]
    [SerializeField] private KeyCode lockOnKey = KeyCode.Q;
    [SerializeField] private float lockOnRadius = 15f;
    [SerializeField] private LayerMask lockOnLayer; // 적 레이어/태그에 맞게 셋
    [SerializeField] private float lockOnDirectionDeadzone = 0.2f;

    [Header("Camera")]
    [SerializeField] private float cameraTargetDistance = 4.5f;
    [SerializeField] private float cameraMinDistance = 1.0f;
    [SerializeField] private float cameraMaxDistance = 6.0f;
    [SerializeField] private float cameraHeight = 1.6f; // 피벗 높이
    [SerializeField] private float cameraCollisionRadius = 0.25f;
    [SerializeField] private LayerMask cameraCollisionMask; // 환경/지형 레이어. Player는 제외
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
        animator = CharacterBody.GetComponent<Animator>();
        rigid = GetComponent<Rigidbody>();
        if (cameraTransform == null)
            cameraTransform = Camera.main != null ? Camera.main.transform : null;

        // PlayerCombat 연결
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
        else
        {
            Debug.LogWarning("PlayerCombat가 없습니다. 전투 입력이 동작하지 않습니다.");
        }

        // 초기 카메라
        yaw = CharacterBody.eulerAngles.y;
        currentDistance = cameraTargetDistance;
        CameraArm.position = CharacterBody.position + Vector3.up * cameraHeight;
        CameraArm.rotation = Quaternion.Euler(pitch, yaw, 0f);

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
        UpdateMovement();
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    private void HandleInput()
    {
        // 이동 입력 판단(패링 조건 전달용)
        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        isMoving = moveInput.sqrMagnitude > 0.0001f;

        // 공격/회피/패링은 PlayerCombat에 위임
        if (combat != null)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0) && isGrounded)
                combat.TryAttack();

            if (Input.GetKeyDown(KeyCode.LeftAlt) && isGrounded)
                combat.TryDodge();

            if (Input.GetKeyDown(KeyCode.Mouse1) && isGrounded)
                combat.TryParry(isMoving);
        }

        // 점프
        if (Input.GetButtonDown("Jump") && isGrounded)
            Jump();

        // 락온 토글
        if (Input.GetKeyDown(lockOnKey))
        {
            if (isLockedOn) ClearLockOn();
            else AcquireLockOn();
        }
    }

    private void AcquireLockOn()
    {
        Vector3 center = transform.position;
        Collider[] hits = Physics.OverlapSphere(center, lockOnRadius, lockOnLayer, QueryTriggerInteraction.Collide);

        if (hits == null || hits.Length == 0)
        {
            isLockedOn = false;
            lockOnTarget = null;
            if (combat != null) combat.SetLockOnTarget(null);
            return;
        }

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

            Vector3 to = candidate.position - cameraTransform.position;
            float dist = to.magnitude;
            float angle = Vector3.Angle(camFwd, to);
            float score = dist + angle * 0.1f;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        lockOnTarget = best;
        isLockedOn = lockOnTarget != null;
        if (combat != null) combat.SetLockOnTarget(lockOnTarget);
    }

    private void ClearLockOn()
    {
        isLockedOn = false;
        lockOnTarget = null;
        if (combat != null) combat.SetLockOnTarget(null);
    }

    private void UpdateMovement()
    {
        bool canMove = (combat == null) || !combat.IsBusy;

        if (!canMove)
        {
            isMoving = false;
            animator.SetBool("isWalk", false);
            animator.SetBool("isRun", false);
            ClearDirectionalBools();
            return;
        }

        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        isMoving = moveInput.sqrMagnitude > 0.0001f;
        bool running = isMoving && Input.GetKey(KeyCode.LeftShift);

        animator.SetBool("isWalk", isMoving && !running);
        animator.SetBool("isRun", isMoving && running);

        if (isMoving)
        {
            Vector3 moveDir = GetMoveDirection();
            float speed = running ? runSpeed : walkSpeed;

            // 이동
            transform.position += moveDir * Time.fixedDeltaTime * speed;

            // 회전
            bool isAttacking = (combat != null) && combat.IsAttacking;
            if (!isAttacking)
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
            if (isLockedOn && lockOnTarget != null && !isAttacking)
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
        animator.SetBool("doJump", true);
        animator.SetBool("isJump", true);
        rigid.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
        isGrounded = false;
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
        {
            desiredDist = Mathf.Clamp(hit.distance - 0.05f, cameraMinDistance, cameraTargetDistance);
        }

        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDist, ref distVel, distSmoothTime);

        cameraTransform.position = pivot - CameraArm.forward * currentDistance;
        cameraTransform.rotation = CameraArm.rotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            animator.SetBool("isJump", false);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;
        }
    }

    private void ClearDirectionalBools()
    {
        animator.SetBool("isLeft", false);
        animator.SetBool("isRight", false);
        animator.SetBool("isBack", false);
    }

    private void UpdateLockOnDirectionalAnim(Vector3 moveDir, bool canMove)
    {
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