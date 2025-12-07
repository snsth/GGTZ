// 공격 후보 평가 후 최적 공격 반환. 쿨타임, 거리, 시야, 정면각 반영

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class BossAttackSelector : MonoBehaviour
{
    [Header("Attacks")]
    public List<AttackOption> attacks = new List<AttackOption>();
    public float attackDecisionInterval = 0.2f;

    private float decisionTimer = 0f;
    private BossLOS los;

    void Awake()
    {
        los = GetComponent<BossLOS>(); // 선택적
    }

    void Update()
    {
        decisionTimer -= Time.deltaTime;
    }

    public AttackOption TrySelect(float distance, Transform player)
    {
        if (decisionTimer > 0f) return null;
        decisionTimer = attackDecisionInterval;

        AttackOption best = null;
        float bestScore = 0f;

        foreach (var opt in attacks)
        {
            if (!opt.IsInRange(distance)) continue;
            if (!opt.IsOffCooldown()) continue;

            if (los && los.useLOS && opt.requiresLOS)
            {
                if (!los.HasLineOfSight(player)) continue;
            }

            float score = opt.weight;
            float norm = opt.NormalizedRange(distance);
            score *= opt.distanceCurve.Evaluate(norm);

            float facing = FacingFactor(player.position, opt.requiredFacingDot);
            score *= facing;

            score *= opt.CooldownFactor();

            if (score > bestScore)
            {
                bestScore = score;
                best = opt;
            }
        }

        return best;
    }

    float FacingFactor(Vector3 targetPos, float requiredDot)
    {
        Vector3 to = (targetPos - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, to);
        if (dot < requiredDot) return 0f;
        return Mathf.InverseLerp(requiredDot, 1f, dot);
    }
}