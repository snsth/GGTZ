using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform CharacterBody;
    [SerializeField] private Transform CameraArm;

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

    void Start()
    {
        animator = CharacterBody.GetComponent<Animator>();
        rigid = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!isDodging && !isParrying)
        {
            HandleInput();
            UpdateMovement();
            Lookaround();
        }
        UpdateComboTimer();
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

    private void StartAttack()
    {
        animator.SetTrigger("OnWeaponAttack");
        comboCount++;
        comboTimer = 0f;
        isAttacking = true;
        isMovingEnabled = false; // 공격 중에 움직임 비활성화
        Invoke("EndAttack", 0.8f); // 공격 애니메이션 지속 시간 이후 isAttacking을 false로 변경
    }

    private void EndAttack()
    {
        isAttacking = false;
        isMovingEnabled = true; // 공격 종료 후 움직임 활성화
    }

    private void StartDodge()
    {
        animator.SetTrigger("doDodge");
        Vector3 dodgeDirection = isMoving ? GetMoveDirection() : CharacterBody.forward;
        rigid.AddForce(dodgeDirection * dodgeForce, ForceMode.Impulse);
        nextDodgeTime = Time.time + dodgeCooldown;
        isDodging = true;
        isMovingEnabled = false; // 구르기 중에 움직임 비활성화
        Invoke("EndDodge", 0.85f);
    }

    private void EndDodge()
    {
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
        if (isMovingEnabled)
        {
            Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            isMoving = moveInput.magnitude > 0;

            animator.SetBool("isMove", isMoving);
            animator.SetBool("isRun", isMoving && Input.GetKey(KeyCode.LeftShift));

            if (isMoving)
            {
                Vector3 moveDir = GetMoveDirection();
                // CharacterBody.forward = moveDir;
                float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
                transform.position += moveDir * Time.deltaTime * speed;
            }
        }
        else
        {
            isMoving = false;
            animator.SetBool("isMove", false);
            animator.SetBool("isRun", false);
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
        Vector3 lookforward = new Vector3(CameraArm.forward.x, 0f, CameraArm.forward.z).normalized;
        Vector3 lookright = new Vector3(CameraArm.right.x, 0f, CameraArm.right.z).normalized;
        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        return (lookforward * moveInput.y + lookright * moveInput.x).normalized;
    }

    private float pitch = 0f;

    private void Lookaround()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        // 1) 캐릭터는 좌우 회전만
        CharacterBody.Rotate(Vector3.up * mouseX);

        // 2) 카메라는 상하 회전만 (pitch만 변화)
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -40f, 60f);

        CameraArm.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
