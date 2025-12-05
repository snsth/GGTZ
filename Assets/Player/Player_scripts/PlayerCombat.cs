using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public Rigidbody rigid;
    public Transform characterBody;
    public Transform cameraArm;          // 카메라 피벗(플레이어 뒤 기준 회피/이동용)
    public WeaponHitbox weapon;

    [Header("Attack")]
    public float attackDuration = 0.8f;  // 총 모션 길이
    public float hitboxOpenDelay = 0.1f; // 선딜(이벤트 미사용시)
    public float hitboxActiveTime = 0.3f;// 타격창(이벤트 미사용시)
    public bool useAnimEvents = true;    // 애니메이션 이벤트로 Open/Close 제어할지
    public float comboInterval = 1f;

    [Header("Dodge/Parry")]
    public float dodgeCooldown = 1f;
    public float dodgeForce = 10f;
    public float parryDuration = 0.5f;
    public float parryCooldown = 2f;

 
    public bool IsAttacking { get; private set; }
    public bool IsDodging { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsBusy => IsAttacking || IsDodging || IsParrying;

    private int comboCount = 0;
    private float comboTimer = 0f;
    private float nextDodgeTime = 0f;
    private float nextParryTime = 0f;
    private Transform lockOnTarget;

    public void SetLockOnTarget(Transform target) => lockOnTarget = target;

    void Update()
    {
        // 콤보 리셋
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

    public void TryAttack()
    {
        if (IsBusy) return;
        animator.applyRootMotion = true;
        animator.SetTrigger("OnWeaponAttack");
        IsAttacking = true;
        comboCount++; comboTimer = 0f;

        if (!useAnimEvents)
            StartCoroutine(AttackRoutine_Timed());
        StartCoroutine(AttackEndAfter(attackDuration));
    }

    private IEnumerator AttackRoutine_Timed()
    {
        yield return new WaitForSeconds(hitboxOpenDelay);
        weapon?.Open();
        yield return new WaitForSeconds(hitboxActiveTime);
        weapon?.Close();
    }

    private IEnumerator AttackEndAfter(float t)
    {
        yield return new WaitForSeconds(t);
        animator.applyRootMotion = false;
        IsAttacking = false;
    }

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
        if (isMoving || comboCount > 0) return; // 기존 조건 유지

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

    // 애니메이션 이벤트용 메서드
    public void Anim_OpenHitbox() => weapon?.Open();
    public void Anim_CloseHitbox() => weapon?.Close();

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
