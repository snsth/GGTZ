using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAbilities : MonoBehaviour
{
    [Header("Refs")]
    public PlayerHealth health;
    public PlayerDamageable damageReceiver;
    public PlayerAbilityUI ui;
    public WeaponHitbox weapon;
    // public SlashVFX slashVfx; -> 궁 이펙트
    public LayerMask ultimateTargets;

    [Header("Balance")]
    public float guardCooldown = 10f;
    public float guardDuration = 0.5f;

    public float healCooldown = 45f;
    public float healPercent = 0.25f;       // MaxHP 25%

    public float buffCooldown = 30f;
    public float buffDuration = 5f;
    public float buffDamageMultiplier = 1.5f; // 필요시 변경

    public float ultimateCooldown = 120f;
    public float ultimateDamageByMaxHpPercent = 0.5f; // MaxHP 50%
    public float ultimateRadius = 2.5f;
    public float ultimateForwardOffset = 1.5f;

    // 쿨다운 타임스탬프
    private float guardCdEnd, healCdEnd, buffCdEnd, ultCdEnd;

    // 버프 상태
    private bool buffActive;
    private float buffEndTime;
    private int baseWeaponDamage;

    // 애니메이션 이벤트용 Pending 플래그
    private bool healPending, buffPending, ultPending;

    void Start()
    {
        if (weapon != null) baseWeaponDamage = weapon.damage;
    }

    // 가드(즉시 적용)
    public bool TryGuard()
    {
        if (Time.time < guardCdEnd) return false;
        guardCdEnd = Time.time + guardCooldown;

        damageReceiver?.SetInvulnerableFor(guardDuration);
        ui?.SetGuardActive(true);
        ui?.StartGuardCooldown(guardCooldown);
        StartCoroutine(EndGuardAfter(guardDuration));
        return true;
    }

    IEnumerator EndGuardAfter(float t)
    {
        yield return new WaitForSeconds(t);
        ui?.SetGuardActive(false);
    }

    // 힐: 쿨만 시작, 효과는 이벤트에서
    public bool TryHeal()
    {
        if (Time.time < healCdEnd) return false;
        if (health == null || !health.IsAlive) return false;
        if (health.currentHp >= health.maxHp - 0.001f) return false;

        healCdEnd = Time.time + healCooldown;
        ui?.StartHealCooldown(healCooldown);
        healPending = true; // 이벤트 대기
        return true;
    }

    public void ApplyHealIfPending()
    {
        if (!healPending) return;
        healPending = false;

        if (health != null && health.IsAlive)
        {
            float amount = health.maxHp * healPercent;
            health.Heal(amount);
        }
    }

    // 버프: 쿨만 시작, 효과는 이벤트에서
    public bool TryBuff()
    {
        if (Time.time < buffCdEnd || buffActive || buffPending) return false;

        buffCdEnd = Time.time + buffCooldown;
        ui?.StartBuffCooldown(buffCooldown);
        buffPending = true;
        return true;
    }

    public void ApplyBuffIfPending()
    {
        if (!buffPending) return;
        buffPending = false;

        buffActive = true;
        buffEndTime = Time.time + buffDuration;

        ui?.SetBuffActive(true);
        if (weapon != null) weapon.damage = Mathf.RoundToInt(baseWeaponDamage * buffDamageMultiplier);
        StartCoroutine(BuffTimer());
    }

    IEnumerator BuffTimer()
    {
        while (Time.time < buffEndTime) yield return null;

        buffActive = false;
        if (weapon != null) weapon.damage = baseWeaponDamage;
        ui?.SetBuffActive(false);
    }

    // 궁극기: 쿨만 시작, 효과는 이벤트에서
    public bool TryUltimate()
    {
        if (Time.time < ultCdEnd) return false;

        ultCdEnd = Time.time + ultimateCooldown;
        ui?.StartUltimateCooldown(ultimateCooldown);
        ultPending = true;
        return true;
    }

    public void ApplyUltimateIfPending()
    {
        if (!ultPending) return;
        ultPending = false;

        float dmg = (health != null ? health.maxHp : 100f) * ultimateDamageByMaxHpPercent;
        Vector3 center = transform.position + transform.forward * ultimateForwardOffset;

        var cols = Physics.OverlapSphere(center, ultimateRadius, ultimateTargets, QueryTriggerInteraction.Ignore);
        var alreadyHit = new HashSet<object>();
        foreach (var c in cols)
        {
            var target = c.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive && alreadyHit.Add(target))
            {
                Vector3 hp = c.ClosestPoint(center);
                Vector3 hn = -transform.forward;
                target.TakeDamage(Mathf.RoundToInt(dmg), hp, hn, this);
            }
        }

        // slashVfx?.PlayOnce(); // 연출 선택
    }
}