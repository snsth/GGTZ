using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public Rigidbody rigid;
    public Transform characterBody;
    public Transform cameraArm;
    public WeaponHitbox weapon;

    [Header("Attack")]
    public float attackDuration = 0.8f;   // 기존 필드 유지(이벤트 미사용 대비)
    public float hitboxOpenDelay = 0.1f;  // "
    public float hitboxActiveTime = 0.3f; // "
    public bool useAnimEvents = true;
    public float comboInterval = 1f;      // 기존 필드(미사용 가능)

    [Header("Dodge/Parry")]
    public float dodgeCooldown = 1f;
    public float dodgeForce = 10f;
    public float parryDuration = 0.5f;
    public float parryCooldown = 2f;

    public bool IsAttacking { get; private set; }
    public bool IsDodging { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsBusy => IsAttacking || IsDodging || IsParrying;

    // 기존 comboCount/comboTimer는 사용 안 함
    // private int comboCount = 0;
    // private float comboTimer = 0f;

    private int currentCombo = 0;           // 0=비공격, 1~3 = 현재 단계
    private const int maxCombo = 3;
    private bool canQueueNext = false;      // 콤보 입력 가능한 창
    private bool buffered = false;          // 창 밖에서 누른 입력 버퍼
    private float bufferExpire = 0f;
    public float comboBufferTime = 0.25f;   // 입력 버퍼 유지시간

    private float nextDodgeTime = 0f;
    private float nextParryTime = 0f;
    private Transform lockOnTarget;

    public void SetLockOnTarget(Transform target) => lockOnTarget = target;

    void Update()
    {
        // 입력 버퍼 만료
        if (buffered && Time.time > bufferExpire)
            buffered = false;

        // 콤보 창이 열렸고, 버퍼가 존재하면 즉시 소비
        if (IsAttacking && canQueueNext && buffered && currentCombo < maxCombo)
        {
            buffered = false;
            QueueNextCombo();
        }
    }

    public void TryAttack()
    {
        // 공격 중에도 입력을 받되, 회피/패링만 제한
        if (IsDodging || IsParrying) return;

        if (!IsAttacking)
        {
            // 첫 타 시작
            currentCombo = 1;
            animator.applyRootMotion = true;
            animator.SetInteger("AttackCount", currentCombo);
            animator.SetTrigger("OnWeaponAttack");
            IsAttacking = true;

            if (!useAnimEvents)
                StartCoroutine(AttackRoutine_Timed()); // 히트박스만 시간으로 열고 닫는 경우
                                                       // AttackEndAfter 코루틴은 더 이상 사용하지 않음(클립 끝 이벤트로 종료)
        }
        else
        {
            // 공격 진행 중: 콤보 입력
            if (canQueueNext && currentCombo < maxCombo)
            {
                QueueNextCombo(); // 창이 열려있으면 즉시 큐
            }
            else
            {
                // 창이 닫혀 있으면 버퍼
                buffered = true;
                bufferExpire = Time.time + comboBufferTime;
            }
        }
    }

    private void QueueNextCombo()
    {
        currentCombo = Mathf.Min(currentCombo + 1, maxCombo);
        animator.SetInteger("AttackCount", currentCombo);
        // Attack_1/2의 Exit Time에 도달하면 AttackCount 조건에 의해 다음 상태로 전이됨
    }

    private IEnumerator AttackRoutine_Timed()
    {
        yield return new WaitForSeconds(hitboxOpenDelay);
        weapon?.Open();
        yield return new WaitForSeconds(hitboxActiveTime);
        weapon?.Close();
    }

    // AttackEndAfter(float t) 는 더 이상 호출하지 않음
    // private IEnumerator AttackEndAfter(float t) { ... }  → 사용 중지하거나 삭제

    public void TryDodge()
    {
        if (IsBusy || Time.time < nextDodgeTime) return;

        animator.SetTrigger("doDodge");
        Vector3 dir = GetDodgeDirection();
        rigid.AddForce(dir * dodgeForce, ForceMode.Impulse);

        nextDodgeTime = Time.time + dodgeCooldown;
        IsDodging = true;
        StartCoroutine(EndDodgeAfter(0.85f));
    }

    private IEnumerator EndDodgeAfter(float t)
    {
        yield return new WaitForSeconds(t);
        IsDodging = false;
        rigid.velocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;
    }

    public void TryParry(bool isMoving)
    {
        if (IsBusy || Time.time < nextParryTime) return;
        if (isMoving || currentCombo > 0) return;

        animator.SetTrigger("doParry");
        nextParryTime = Time.time + parryCooldown;
        IsParrying = true;
        StartCoroutine(EndParryAfter(parryDuration));
    }

    private IEnumerator EndParryAfter(float t)
    {
        yield return new WaitForSeconds(t);
        IsParrying = false;
    }

    // 애니메이션 이벤트용
    public void Anim_OpenHitbox() => weapon?.Open();
    public void Anim_CloseHitbox() => weapon?.Close();

    // 콤보 창 열고/닫기(각 클립의 알맞은 구간에 배치)
    public void Anim_ComboWindowOpen() => canQueueNext = true;
    public void Anim_ComboWindowClose() => canQueueNext = false;

    // 각 Attack_n의 거의 마지막 프레임(전이 시점보다 뒤)에서 호출
    public void Anim_AttackFinished()
    {
        // 연결이 되지 않았을 때만 이 이벤트가 실행됨(전이되면 호출되지 않음)
        animator.applyRootMotion = false;
        IsAttacking = false;
        currentCombo = 0;
        canQueueNext = false;
        buffered = false;
        animator.SetInteger("AttackCount", 0);
    }

    private Vector3 GetDodgeDirection()
    {
        Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        if (input.sqrMagnitude < 0.0001f)
            return -new Vector3(characterBody.forward.x, 0f, characterBody.forward.z).normalized;

        Vector3 forward;
        Vector3 right;

        if (lockOnTarget != null)
        {
            forward = new Vector3(characterBody.forward.x, 0f, characterBody.forward.z).normalized;
            right = new Vector3(characterBody.right.x, 0f, characterBody.right.z).normalized;
        }
        else
        {
            forward = new Vector3(cameraArm.forward.x, 0f, cameraArm.forward.z).normalized;
            right = new Vector3(cameraArm.right.x, 0f, cameraArm.right.z).normalized;
        }
        return (forward * input.y + right * input.x).normalized;
    }
}