using UnityEngine;

public class BossDamageable : MonoBehaviour, IDamageable
{
    public BossHealth bossHealth;

    public bool IsAlive => bossHealth != null && bossHealth.currentHp > 0f;

    void Awake()
    {
        if (bossHealth == null)
            bossHealth = GetComponent<BossHealth>();
    }

    public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal, UnityEngine.Object instigator = null)
    {
        if (bossHealth == null || !IsAlive) return;

        bossHealth.TakeDamage(amount);

        // TODO: 연출 필요 시 여기서 처리
        // - 피격 이펙트: hitPoint, hitNormal 사용
        // - 카메라 쉐이크/사운드
        // - instigator가 Transform/Component면 가해자 정보 활용
    }
}