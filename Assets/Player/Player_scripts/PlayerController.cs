using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform CharacterBody;
    [SerializeField] private Transform CameraArm;
    [SerializeField] private KeyCode lockOnKey = KeyCode.Q;
    [SerializeField] private float lockOnRadius = 15f;
    [SerializeField] private LayerMask lockOnLayer; // 적 레이어/태그에 맞게 셋
    [SerializeField] private Transform cameraTransform; // Main Camera 할당
    [SerializeField] private float cameraTargetDistance = 4.5f;
    [SerializeField] private float cameraMinDistance = 1.0f;
    [SerializeField] private float cameraMaxDistance = 6.0f;
    [SerializeField] private float cameraHeight = 1.6f; // 피벗 높이
    [SerializeField] private float cameraCollisionRadius = 0.25f;
    [SerializeField] private LayerMask cameraCollisionMask; // 환경/지형 레이어. Player는 제외
    [SerializeField] private float yawSmoothTime = 0.03f;
    [SerializeField] private float pitchSmoothTime = 0.03f;
    [SerializeField] private float distSmoothTime = 0.05f;
    [SerializeField] private float turnSpeed = 12f; // 캐릭터 회전 속도
    private float yaw = 0f, pitch = 10f;
    private float yawVel = 0f, pitchVel = 0f;
    private float currentDistance = 0f, distVel = 0f;
    private Transform lockOnTarget;
    private bool isLockedOn = false;
    private Animator animator;
    private Rigidbody rigid;
    private bool isGrounded = true;
    private bool isMoving = false;
    private bool isMovingEnabled = true;
    private bool isAttacking = false;
    private bool isDodging = false;
    private bool isParrying = false;
    private int comboCount = 0;
    private float nextAttackTime = 0f;
    private float nextDodgeTime = 0f;
    private float nextParryTime = 0f;
    private float comboTimer = 0f;
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float jumpPower = 10f;
    public float sensitivity = 2f;
    public float dodgeCooldown = 1.0f;
    public float dodgeForce = 10f;
    public float parryDuration = 0.5f;
    public float parryCooldown = 2.0f;
    public float comboInterval = 1f;
    public float accel = 20f;          // 가속
    public float decel = 25f;          // 감속
    private Vector3 planarVelocity = Vector3.zero;
    private Vector2 cachedMoveInput;

    void Start()
    {
        animator = CharacterBody.GetComponent<Animator>();
        rigid = GetComponent<Rigidbody>();

        // 초기 카메라 각/거리
        yaw = CharacterBody.eulerAngles.y; // 캐릭터 뒤에서 시작
        currentDistance = cameraTargetDistance;

        // 피벗 위치/회전 초기화
        CameraArm.position = CharacterBody.position + Vector3.up * cameraHeight;
        CameraArm.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // 카메라를 즉시 원하는 위치로 이동
        if (cameraTransform != null)
        {
            cameraTransform.position = CameraArm.position - CameraArm.forward * currentDistance;
            cameraTransform.rotation = CameraArm.rotation;
        }
    }

    private void Update()
    {
        if (!isDodging && !isParrying)
        {
            HandleInput();
            cachedMoveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            Lookaround();
        }
        UpdateComboTimer();
    }
    private void FixedUpdate()
    {
        if (!isDodging && !isParrying)
        {
            UpdateMovement();
        }
    }

    private void HandleInput()
    {
        if (!isAttacking && Input.GetKeyDown(KeyCode.Mouse0) && Time.time >= nextAttackTime && isGrounded && !isParrying)
        {
            StartAttack();
        }

        if (!isDodging && Input.GetKeyDown(KeyCode.LeftAlt) && Time.time >= nextDodgeTime && isGrounded && !isParrying && !isAttacking)
        {
            StartDodge();
        }

        if (Input.GetKeyDown(KeyCode.Mouse1) && Time.time >= nextParryTime && isGrounded && !isDodging && !isMoving && !isAttacking && comboCount == 0)
        {
            StartParry();
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            Jump();
        }

    }
    private void AcquireLockOn()
    {
        Collider[] hits = Physics.OverlapSphere(CharacterBody.position, lockOnRadius, lockOnLayer, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
        {
            isLockedOn = false;
            lockOnTarget = null;
            return;
        }

        // 카메라 전방에 가까운 타겟 우선
        Transform best = null;
        float bestScore = float.MaxValue;
        Vector3 camFwd = CameraArm.forward;

        foreach (var h in hits)
        {
            Transform t = h.transform;
            if (t == this.transform) continue;

            Vector3 to = t.position - cameraTransform.position;
            float dist = to.magnitude;
            float angle = Vector3.Angle(camFwd, to);
            float score = dist + angle * 0.1f; // 가중치는 취향대로
            if (score < bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        lockOnTarget = best;
        isLockedOn = lockOnTarget != null;
    }

    private void ClearLockOn()
    {
        isLockedOn = false;
        lockOnTarget = null;
    }

    private void StartAttack()
    {
        animator.applyRootMotion = true;
        animator.SetTrigger("OnWeaponAttack");
        comboCount++;
        comboTimer = 0f;
        isAttacking = true;
        isMovingEnabled = false;
        Invoke("EndAttack", 0.8f);
    }
    private void EndAttack()
    {
        animator.applyRootMotion = false;
        isAttacking = false;
        isMovingEnabled = true;
    }

    private void StartDodge()
    {
        animator.SetTrigger("doDodge");

        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector3 dodgeDirection;

        if (input.sqrMagnitude < 0.0001f)
        {
            dodgeDirection = -CharacterBody.forward; // 백스텝
        }
        else
        {
            if (isLockedOn && lockOnTarget != null)
            {
                Vector3 fwd = new Vector3(CharacterBody.forward.x, 0f, CharacterBody.forward.z).normalized;
                Vector3 right = new Vector3(CharacterBody.right.x, 0f, CharacterBody.right.z).normalized;
                dodgeDirection = (fwd * input.y + right * input.x).normalized;
            }
            else
            {
                dodgeDirection = GetMoveDirection();
            }
        }

        rigid.AddForce(dodgeDirection * dodgeForce, ForceMode.Impulse);
        nextDodgeTime = Time.time + dodgeCooldown;
        isDodging = true;
        isMovingEnabled = false;
        Invoke("EndDodge", 0.85f);
    }

    private void EndDodge()
    {
        animator.applyRootMotion = false;
        isDodging = false;
        isMovingEnabled = true; // 구르기 종료 후 움직임 활성화
        rigid.velocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;
    }

    private void StartParry()
    {
        animator.SetTrigger("doParry");
        isParrying = true;
        nextParryTime = Time.time + parryCooldown;
        Invoke("EndParry", parryDuration);
    }

    private void EndParry()
    {
        isParrying = false;
    }
    public bool IsDodgingOrParrying()
    {
        return isDodging || isParrying;
    }
    private void UpdateMovement()
    {
        if (!isMovingEnabled)
        {
            isMoving = false;
            animator.SetBool("isWalk", false);
            animator.SetBool("isRun", false);
            return;
        }

        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        isMoving = moveInput.sqrMagnitude > 0.0001f;
        bool running = isMoving && Input.GetKey(KeyCode.LeftShift);

        animator.SetBool("isWalk", isMoving && !running);
        animator.SetBool("isRun", isMoving && Input.GetKey(KeyCode.LeftShift));

        if (isMoving)
        {
            Vector3 moveDir = GetMoveDirection();
            float speed = running ? runSpeed : walkSpeed;
            transform.position += moveDir * Time.deltaTime * speed;

            if (!isAttacking)
            {
                Vector3 look = new Vector3(moveDir.x, 0f, moveDir.z);
                if (look.sqrMagnitude > 0.0001f)
                {
                    Quaternion t = Quaternion.LookRotation(look);
                    CharacterBody.rotation = Quaternion.Slerp(CharacterBody.rotation, t, 12f * Time.deltaTime);
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

    public float minPitch = -40f;
    public float maxPitch = 60f;

    private void Lookaround()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        float targetYaw = yaw + mouseX;
        float targetPitch = Mathf.Clamp(pitch - mouseY, -40f, 60f);

        // 록온 중이면 카메라 yaw를 타겟 방향으로 부드럽게 보정
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
    private void LateUpdate()
    {
        UpdateCamera();
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

    private void UpdateComboTimer()
    {
        if (comboCount > 0)
        {
            comboTimer += Time.deltaTime;
            if (comboTimer >= comboInterval)
            {
                comboCount = 0;
                comboTimer = 0f;
            }
        }
    }
}
