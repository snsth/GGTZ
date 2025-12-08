using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [System.Flags]
    public enum AbilityLockFlags { None = 0, Move = 1 << 0, Attack = 1 << 1, Dodge = 1 << 2, Jump = 1 << 3, Parry = 1 << 4, OtherAbilities = 1 << 5 }

    [Header("Refs")]
    public Animator animator;
    public Rigidbody rigid;
    public Transform characterBody;
    public Transform cameraArm;
    public WeaponHitbox weapon;
    public PlayerAbilities abilities;

    [Header("Attack")]
    public float attackDuration = 0.8f;
    public bool useAnimEvents = true;
    public float minTimeBetweenAttacks = 0.05f;
    public float comboInterval = 1.0f;

    public float comboWindowOpenNormalized = 0.6f;
    public float comboWindowCloseNormalized = 0.8f;
    public float comboBufferTime = 0.25f;

    [Header("Dodge/Parry")]
    public float dodgeCooldown = 1f;
    public float dodgeForce = 10f;

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
    public AbilityVFX abilityVfx;  // 인스펙터 할당

    [Header("Ability Casting Lock")]
    public bool lockOnAbilityCast = true;
    public float healCastTimeout = 1.0f;      // H
    public float buffCastTimeout = 1.0f;      // E
    public float ultimateCastTimeout = 1.5f;  // R

    public AbilityLockFlags defaultCastLock =
        AbilityLockFlags.Move | AbilityLockFlags.Attack | AbilityLockFlags.Dodge |
        AbilityLockFlags.Jump | AbilityLockFlags.Parry | AbilityLockFlags.OtherAbilities;

    public bool IsAttacking { get; private set; }
    public bool IsDodging { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsBusy => IsAttacking || IsDodging || IsParrying || isCastingAbility;

    private int currentCombo = 0;
    private const int maxCombo = 3;
    private float nextAttackTime = 0f;
    private float comboTimer = 0f;

    private float nextDodgeTime = 0f;
    private Transform lockOnTarget;

    private bool canQueueNext = false;
    private bool buffered = false;
    private float bufferExpire = 0f;
    private bool queuedThisWindow = false;
    private Coroutine stageTimingCo = null;

    private bool isCastingAbility = false;
    private AbilityLockFlags activeLockMask = AbilityLockFlags.None;
    private Coroutine castLockCo;
    private PlayerAbilities.AbilityKind castingWhich = PlayerAbilities.AbilityKind.None;

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

        if (buffered && Time.time > bufferExpire)
            buffered = false;

        if (!useAnimEvents && IsAttacking && canQueueNext && buffered && currentCombo < maxCombo && !queuedThisWindow)
        {
            buffered = false;
            QueueNextCombo();
        }

        if (Input.GetKeyDown(KeyCode.H) && !IsBusy)
        {
            if (abilities != null && abilities.TryHeal())
            {
                BeginCastLock(healCastTimeout, defaultCastLock, PlayerAbilities.AbilityKind.Heal);
                animator.SetTrigger(healTrigger);
            }
        }
        if (Input.GetKeyDown(KeyCode.E) && !IsBusy)
        {
            if (abilities != null && abilities.TryBuff())
            {
                BeginCastLock(buffCastTimeout, defaultCastLock, PlayerAbilities.AbilityKind.Buff);
                animator.SetTrigger(buffTrigger);
            }
        }
        if (Input.GetKeyDown(KeyCode.R) && !IsBusy)
        {
            if (abilities != null && abilities.TryUltimate())
            {
                BeginCastLock(ultimateCastTimeout, defaultCastLock, PlayerAbilities.AbilityKind.Ultimate);
                animator.SetTrigger(ultimateTrigger);
            }
        }
    }

    public void SetLockOnTarget(Transform target) => lockOnTarget = target;

    public void TryAttack()
    {
        if (IsBusy) return;

        if (!IsAttacking)
        {
            if (Time.time < nextAttackTime) return;
            if (currentCombo == 0 || comboTimer >= comboInterval) currentCombo = 1;
            else currentCombo = Mathf.Min(currentCombo + 1, maxCombo);

            comboTimer = 0f;
            StartAttackStage(currentCombo);
            nextAttackTime = Time.time + minTimeBetweenAttacks;
        }
        else
        {
            if (!useAnimEvents)
            {
                if (canQueueNext && currentCombo < maxCombo && !queuedThisWindow) QueueNextCombo();
                else { buffered = true; bufferExpire = Time.time + comboBufferTime; }
            }
        }
    }

    private void QueueNextCombo()
    {
        currentCombo = Mathf.Min(currentCombo + 1, maxCombo);
        queuedThisWindow = true;
        StartAttackStage(currentCombo);
        animator.SetInteger("AttackCount", currentCombo);
        nextAttackTime = Time.time + minTimeBetweenAttacks;
    }

    private void StartAttackStage(int stage)
    {
        if (stageTimingCo != null) { StopCoroutine(stageTimingCo); stageTimingCo = null; }
        canQueueNext = false;
        queuedThisWindow = false;

        IsAttacking = true;
        animator.applyRootMotion = true;

        animator.SetInteger("AttackCount", stage);
        int hash = stage == 1 ? attack1Hash : stage == 2 ? attack2Hash : attack3Hash;
        animator.CrossFadeInFixedTime(hash, crossFadeDuration, baseLayer, 0f);

        if (!useAnimEvents) stageTimingCo = StartCoroutine(AttackStageTiming());
    }

    private IEnumerator AttackStageTiming()
    {
        float dur = Mathf.Max(attackDuration, 0.05f);
        float openT = Mathf.Clamp01(comboWindowOpenNormalized) * dur;
        float closeT = Mathf.Clamp01(comboWindowCloseNormalized) * dur;

        if (openT > 0f) yield return new WaitForSeconds(openT);
        canQueueNext = true; queuedThisWindow = false;

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

    // 구르기
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

    // 패링
    public void TryParry(bool isMoving)
    {
        if (IsBusy || isMoving || currentCombo > 0) return;

        if (!(abilities?.TryGuard() ?? false)) return;
        animator.SetTrigger("doParry");
        IsParrying = true;
        StartCoroutine(EndParryAfter(0.5f));
    }

    private IEnumerator EndParryAfter(float t)
    {
        yield return new WaitForSeconds(t);
        IsParrying = false;
    }

    // 히트박스 이벤트
    public void Anim_OpenHitbox() => weapon?.Open();
    public void Anim_CloseHitbox() => weapon?.Close();

    // 스킬 애니메이션 이벤트
    public void Anim_HealApply()
    {
        abilities?.ApplyHealIfPending();
        abilityVfx?.PlayHeal();
        EndCastLock(PlayerAbilities.AbilityKind.Heal);
    }
    public void Anim_BuffApply()
    {
        abilities?.ApplyBuffIfPending();
        abilityVfx?.PlayBuff();
        EndCastLock(PlayerAbilities.AbilityKind.Buff);
    }
    public void Anim_UltimateApply()
    {
        // 궁극기 효과 적용을 히트박스에 위임
        abilities?.ApplyUltimateIfPending_Collider(abilityVfx, transform);
        EndCastLock(PlayerAbilities.AbilityKind.Ultimate);
    }

    // 캐스팅 락
    void BeginCastLock(float timeout, AbilityLockFlags mask, PlayerAbilities.AbilityKind which)
    {
        if (!lockOnAbilityCast) return;
        isCastingAbility = true;
        activeLockMask = mask;
        castingWhich = which;

        if ((activeLockMask & AbilityLockFlags.Move) != 0 && rigid != null)
        {
            rigid.velocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }

        if (castLockCo != null) { StopCoroutine(castLockCo); castLockCo = null; }
        if (timeout > 0f) castLockCo = StartCoroutine(CastLockTimeoutRealtime(timeout));
    }

    IEnumerator CastLockTimeoutRealtime(float t)
    {
        float end = Time.realtimeSinceStartup + t;
        while (Time.realtimeSinceStartup < end) yield return null;

        // 이벤트를 못 받았더라도 효과를 적용해 준다(안전)
        if (abilities != null)
        {
            switch (castingWhich)
            {
                case PlayerAbilities.AbilityKind.Heal:
                    abilities.ApplyHealIfPending();
                    break;
                case PlayerAbilities.AbilityKind.Buff:
                    abilities.ApplyBuffIfPending();
                    break;
                case PlayerAbilities.AbilityKind.Ultimate:
                    abilities.ApplyUltimateIfPending_Collider(abilityVfx, transform);
                    break;
            }
        }

        isCastingAbility = false;
        activeLockMask = AbilityLockFlags.None;
        castingWhich = PlayerAbilities.AbilityKind.None;
        castLockCo = null;
    }

    void EndCastLock(PlayerAbilities.AbilityKind which)
    {
        if (!lockOnAbilityCast) return;
        if (castingWhich != which) return;

        isCastingAbility = false;
        activeLockMask = AbilityLockFlags.None;
        castingWhich = PlayerAbilities.AbilityKind.None;
        if (castLockCo != null) { StopCoroutine(castLockCo); castLockCo = null; }
    }

    public void ForceCancelCast()
    {
        if (!isCastingAbility) return;
        abilities?.CancelPending(castingWhich);
        isCastingAbility = false;
        activeLockMask = AbilityLockFlags.None;
        castingWhich = PlayerAbilities.AbilityKind.None;
        if (castLockCo != null) { StopCoroutine(castLockCo); castLockCo = null; }
    }

    void OnDisable() => ForceCancelCast();

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
            Vector3 back = -characterBody.forward; back.y = 0f;
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
