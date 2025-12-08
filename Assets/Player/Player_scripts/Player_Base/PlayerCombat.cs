using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    public Animator animator;
    public Rigidbody rigid;
    public Transform characterBody;
    public Transform cameraArm;
    public WeaponHitbox weapon;
    public PlayerAbilities abilities;

    [Header("Attack")]
    public float attackDuration = 0.8f;     // Use Anim Events=false일 때 타이머 기준
    public bool useAnimEvents = true;       // true면 클립 이벤트 사용
    public float minTimeBetweenAttacks = 0.05f;
    public float comboInterval = 1.0f;

    // 딜레이/콤보창(Use Anim Events=false에서만 사용)
    public float comboWindowOpenNormalized = 0.6f; // 현재 타의 60% 지점부터 다음 타 입력 허용
    public float comboWindowCloseNormalized = 0.8f; // 80% 지점에서 창 닫음
    public float comboBufferTime = 0.25f;           // 창 닫힌 동안 입력 버퍼 유지

    [Header("Dodge/Parry")]
    public float dodgeCooldown = 1f;
    public float dodgeForce = 10f;
    public float parryDuration = 0.5f;
    public float parryCooldown = 2f;
    public PlayerAbilityUI abilityUI; // 인스펙터 할당

    [Header("Animator States")]
    public string attack1State = "Attack_1";
    public string attack2State = "Attack_2";
    public string attack3State = "Attack_3";
    public bool useFullPath = false;
    public float crossFadeDuration = 0.05f;

    [Header("Ability Anim Triggers")]
    public string healTrigger = "doHeal";
    public string buffTrigger = "doBuff";
    public string ultimateTrigger = "doUltimate";

    [Header("Ability Casting Lock")]
    public bool lockOnAbilityCast = true;
    public float healCastTimeout = 1.0f;
    public float buffCastTimeout = 1.0f;
    public float ultimateCastTimeout = 1.5f;
    private bool isCastingAbility = false;
    private Coroutine castLockCo;

    // 상태
    public bool IsAttacking { get; private set; }
    public bool IsDodging { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsBusy => IsAttacking || IsDodging || IsParrying || isCastingAbility;

    private int currentCombo = 0;           // 0=비공격, 1~3
    private const int maxCombo = 3;
    private float nextAttackTime = 0f;      // 디바운스
    private float comboTimer = 0f;          // 콤보 유지 타이머

    private float nextDodgeTime = 0f;
    private Transform lockOnTarget;


    // 콤보창/버퍼(이벤트 미사용일 때만)
    private bool canQueueNext = false;
    private bool buffered = false;
    private float bufferExpire = 0f;
    private bool queuedThisWindow = false;  // 한 창당 1번만 단계 증가
    private Coroutine stageTimingCo = null;

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
                animator.SetInteger("AttackCount", 0);
            }
        }

        // 입력 버퍼 만료
        if (buffered && Time.time > bufferExpire)
            buffered = false;

        // 창이 열렸고 버퍼가 있으면 즉시 소비(이벤트 미사용일 때만)
        if (!useAnimEvents && IsAttacking && canQueueNext && buffered && currentCombo < maxCombo && !queuedThisWindow)
        {
            buffered = false;
            QueueNextCombo();
        }

        if (Input.GetKeyDown(KeyCode.H))
        {
            if (!IsBusy && abilities != null && abilities.TryHeal())
            {
                BeginCastLock(healCastTimeout);
                animator.SetTrigger(healTrigger);
            }
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!IsBusy && abilities != null && abilities.TryBuff())
            {
                BeginCastLock(buffCastTimeout);
                animator.SetTrigger(buffTrigger);
            }
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!IsBusy && abilities != null && abilities.TryUltimate())
            {
                BeginCastLock(ultimateCastTimeout);
                animator.SetTrigger(ultimateTrigger);
            }
        }
    }

    public void SetLockOnTarget(Transform target) => lockOnTarget = target;

    // 컨트롤러에서 호출
    public void TryAttack()
    {
        if (IsBusy) return;

        if (!IsAttacking)
        {
            if (Time.time < nextAttackTime) return; // 디바운스
                                                    // 첫 타 혹은 콤보 유지 내 다음 타
            if (currentCombo == 0 || comboTimer >= comboInterval) currentCombo = 1;
            else currentCombo = Mathf.Min(currentCombo + 1, maxCombo);

            comboTimer = 0f;
            StartAttackStage(currentCombo);
            nextAttackTime = Time.time + minTimeBetweenAttacks;
        }
        else
        {
            // 공격 중 입력: 콤보 창이 열릴 때만 다음 타, 아니면 버퍼
            if (!useAnimEvents)
            {
                if (canQueueNext && currentCombo < maxCombo && !queuedThisWindow)
                {
                    QueueNextCombo(); // 창이 열려 있으면 즉시
                }
                else
                {
                    buffered = true;                     // 창이 닫혀 있으면 버퍼
                    bufferExpire = Time.time + comboBufferTime;
                }
            }
        }
    }

    private void QueueNextCombo()
    {
        currentCombo = Mathf.Min(currentCombo + 1, maxCombo);
        queuedThisWindow = true; // 이번 창에서 더는 증가하지 않음
        StartAttackStage(currentCombo);
        animator.SetInteger("AttackCount", currentCombo);
        nextAttackTime = Time.time + minTimeBetweenAttacks;
    }

    private void StartAttackStage(int stage)
    {
        // 이전 타이머 종료
        if (stageTimingCo != null) { StopCoroutine(stageTimingCo); stageTimingCo = null; }
        canQueueNext = false;
        queuedThisWindow = false;

        IsAttacking = true;
        animator.applyRootMotion = true;

        // 트리거는 사용하지 않음(딜레이/콤보창이 무의미해지므로)
        // animator.ResetTrigger("OnWeaponAttack");
        // animator.SetTrigger("OnWeaponAttack");

        animator.SetInteger("AttackCount", stage);

        int hash = stage == 1 ? attack1Hash : stage == 2 ? attack2Hash : attack3Hash;
        animator.CrossFadeInFixedTime(hash, crossFadeDuration, baseLayer, 0f);

        if (!useAnimEvents)
            stageTimingCo = StartCoroutine(AttackStageTiming());
        else
            stageTimingCo = null;
    }

    // Use Anim Events=false일 때 콤보 창/종료를 시간으로 제어
    private IEnumerator AttackStageTiming()
    {
        float dur = Mathf.Max(attackDuration, 0.05f);
        float openT = Mathf.Clamp01(comboWindowOpenNormalized) * dur;
        float closeT = Mathf.Clamp01(comboWindowCloseNormalized) * dur;

        if (openT > 0f) yield return new WaitForSeconds(openT);
        canQueueNext = true;
        queuedThisWindow = false;

        // 창이 열리자마자 버퍼 소비
        if (buffered && currentCombo < maxCombo)
        {
            buffered = false;
            QueueNextCombo();
            yield break;
        }

        float remainToClose = Mathf.Max(0f, closeT - openT);
        if (remainToClose > 0f) yield return new WaitForSeconds(remainToClose);
        canQueueNext = false;

        float remainToEnd = Mathf.Max(0f, dur - closeT);
        if (remainToEnd > 0f) yield return new WaitForSeconds(remainToEnd);

        Anim_AttackFinished();
        stageTimingCo = null;
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
        if (isMoving || currentCombo > 0) return;

        // 쿨타임 준비됐을 때만 애니메이션 재생
        if (!(abilities?.TryGuard() ?? false)) return;

        animator.SetTrigger("doParry");
        IsParrying = true;
        StartCoroutine(EndParryAfter(0.5f));
    }

    private IEnumerator EndParryAfter(float t)
    {
        yield return new WaitForSeconds(t);
        IsParrying = false;
        // abilityUI?.SetGuardActive(false); // PlayerAbilities가 처리하므로 제거
    }

    // 히트박스 이벤트(있으면 그대로 사용 가능)
    public void Anim_OpenHitbox() => weapon?.Open();
    public void Anim_CloseHitbox() => weapon?.Close();

    // Ability 애니메이션 이벤트에서 호출
    public void Anim_HealApply() { abilities?.ApplyHealIfPending(); EndCastLock(); }
    public void Anim_BuffApply() { abilities?.ApplyBuffIfPending(); EndCastLock(); }
    public void Anim_UltimateApply() { abilities?.ApplyUltimateIfPending(); EndCastLock(); }

    // 캐스팅 락
    void BeginCastLock(float timeout)
    {
        if (!lockOnAbilityCast) return;
        isCastingAbility = true;
        if (castLockCo != null) { StopCoroutine(castLockCo); castLockCo = null; }
        if (timeout > 0f) castLockCo = StartCoroutine(CastLockTimeout(timeout));
    }
    IEnumerator CastLockTimeout(float t)
    {
        yield return new WaitForSeconds(t);
        isCastingAbility = false;       // 안전 해제
        castLockCo = null;
    }
    void EndCastLock()
    {
        if (!lockOnAbilityCast) return;
        isCastingAbility = false;
        if (castLockCo != null) { StopCoroutine(castLockCo); castLockCo = null; }
    }

    // Attack_n 마지막 프레임(Idle 복귀 직전)
    public void Anim_AttackFinished()
    {
        animator.applyRootMotion = false;
        IsAttacking = false;

        canQueueNext = false;
        buffered = false;
        queuedThisWindow = false;
        if (stageTimingCo != null) { StopCoroutine(stageTimingCo); stageTimingCo = null; }

        if (currentCombo >= maxCombo) currentCombo = 0;

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