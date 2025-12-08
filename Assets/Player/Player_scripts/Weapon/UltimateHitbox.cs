using System.Collections.Generic;
using UnityEngine;

public class UltimateHitbox : MonoBehaviour
{
    [Header("Config")]
    public LayerMask targetLayers;     // 보스/적 레이어
    public int damage;
    public Transform instigator;       // 플레이어(가해자)

    private readonly HashSet<object> alreadyHit = new HashSet<object>();

    void OnEnable()
    {
        alreadyHit.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        // 레이어 필터
        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg == null || !dmg.IsAlive) return;

        // 중복 타격 방지(같은 대상 1회만)
        if (!alreadyHit.Add(dmg)) return;

        Vector3 hp = other.ClosestPoint(transform.position);
        Vector3 hn = (other.transform.position - transform.position).normalized;

        dmg.TakeDamage(damage, hp, hn, instigator);
    }
}