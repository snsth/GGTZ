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
public float attackDuration = 0.8f;     // 애니 이벤트가 없을 때 폴백
public bool useAnimEvents = true;       // 클립 이벤트(Open/Close, Finished) 권장
public float minTimeBetweenAttacks = 0.05f; // 입력 디바운스
public float comboInterval = 1.0f;      // 다음 입력이 이 시간 내면 2/3타

[Header("Dodge/Parry")]
public float dodgeCooldown = 1f;
public float dodgeForce = 10f;
public float parryDuration = 0.5f;
public float parryCooldown = 2f;

[Header("Animator States")]
public string attack1State = "Attack_1";
public string attack2State = "Attack_2";
public string attack3State = "Attack_3";
public bool useFullPath = false;        // 서브스테이트면 true로 바꾸고 "Base Layer.Combo.Attack_1" 등 사용
public float crossFadeDuration = 0.05f;

// 상태
public bool IsAttacking { get; private set; }
public bool IsDodging  { get; private set; }
public bool IsParrying { get; private set; }
public bool IsBusy => IsAttacking || IsDodging || IsParrying;

private int currentCombo = 0;           // 0=비공격, 1~3=현재 단계
private const int maxCombo = 3;
private float nextAttackTime = 0f;      // 디바운스용
private float comboTimer = 0f;          // 콤보 유지 타이머

private float nextDodgeTime = 0f;
private float nextParryTime = 0f;
private Transform lockOnTarget;

// 해시
private int attack1Hash, attack2Hash, attack3Hash;
private const int baseLayer = 0;

void Awake()
{
    attack1Hash = Animator.StringToHash(useFullPath ? $"Base Layer.{attack1State}" : attack1State);
    attack2Hash = Animator.StringToHash(useFullPath ? $"Base Layer.{attack2State}" : attack2State);
    attack3Hash = Animator.StringToHash(useFullPath ? $"Base Layer.{attack3State}" : attack3State);
}

void Update()
{
    // 콤보 유지시간(공격 종료 후에만 카운트)
    if (currentCombo > 0 && !IsAttacking)
    {
        comboTimer += Time.deltaTime;
        if (comboTimer >= comboInterval)
        {
            currentCombo = 0;
            comboTimer = 0f;
            animator.SetInteger("AttackCount", 0); // 디버그/모니터용
        }
    }
}

public void SetLockOnTarget(Transform target) => lockOnTarget = target;

// 컨트롤러에서 호출
public void TryAttack()
{
    if (IsBusy) return;
    if (Time.time < nextAttackTime) return;

    // 단계 결정: 이전 타 끝난 뒤 comboInterval 내라면 2/3타, 아니면 1타
    if (currentCombo == 0 || comboTimer >= comboInterval) currentCombo = 1;
    else currentCombo = Mathf.Min(currentCombo + 1, maxCombo);

    comboTimer = 0f;
    StartAttackStage(currentCombo);
    nextAttackTime = Time.time + minTimeBetweenAttacks;
}

private void StartAttackStage(int stage)
{
    IsAttacking = true;
    animator.applyRootMotion = true;

    // 그래프가 OnWeaponAttack 하나만 걸려 있어도, 어떤 상태로 갈지는 코드가 확정
    animator.ResetTrigger("OnWeaponAttack");
    animator.SetTrigger("OnWeaponAttack");
    animator.SetInteger("AttackCount", stage); // 디버그/모니터용

    int hash = stage == 1 ? attack1Hash : stage == 2 ? attack2Hash : attack3Hash;
    animator.CrossFadeInFixedTime(hash, crossFadeDuration, baseLayer, 0f);

    if (!useAnimEvents)
        StartCoroutine(Fallback_EndAfter(attackDuration));
}

private IEnumerator Fallback_EndAfter(float t)
{
    yield return new WaitForSeconds(t);
    Anim_AttackFinished();
}

public void TryDodge()
{
    if (IsBusy) return;
    if (Time.time < nextDodgeTime) return;

    animator.SetTrigger("doDodge");
    Vector3 dir = GetDodgeDirection();
    rigid.AddForce(dir * dodgeForce, ForceMode.Impulse);

    IsDodging = true;
    nextDodgeTime = Time.time + dodgeCooldown;
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
    if (IsBusy) return;
    if (Time.time < nextParryTime) return;
    if (isMoving || currentCombo > 0) return;

    animator.SetTrigger("doParry");
    IsParrying = true;
    nextParryTime = Time.time + parryCooldown;
    StartCoroutine(EndParryAfter(parryDuration));
}

private IEnumerator EndParryAfter(float t)
{
    yield return new WaitForSeconds(t);
    IsParrying = false;
}

// 애니메이션 이벤트(각 Attack_n 클립에 배치)
public void Anim_OpenHitbox() => weapon?.Open();
public void Anim_CloseHitbox() => weapon?.Close();

// Attack_n의 거의 마지막 프레임(Idle 복귀 직전)
public void Anim_AttackFinished()
{
    animator.applyRootMotion = false;
    IsAttacking = false;

    // 마지막 타면 즉시 리셋, 아니면 콤보 유지시간 내 2/3타 대기
    if (currentCombo >= maxCombo) currentCombo = 0;

    animator.ResetTrigger("OnWeaponAttack");
}

private Vector3 GetDodgeDirection()
{
    Vector2 input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    if (input.sqrMagnitude < 0.0001f)
    {
        Vector3 back = -characterBody.forward;
        back.y = 0f;
        return back.normalized;
    }

    Vector3 forward, right;
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