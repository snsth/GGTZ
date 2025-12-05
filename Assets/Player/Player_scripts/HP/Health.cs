using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour, IDamageable
{
    public int maxHP = 100;
    public float hitCooldown = 0.5f;
    public Slider hpSlider; // 플레이어만 할당
    public bool destroyOnDeath = false;

    private int currentHP;
    private float nextHit;

    void Awake()
    {
        currentHP = maxHP;
        UpdateUI();
    }

    public bool IsAlive => currentHP > 0;

    public void TakeDamage(int amount, Vector3 p, Vector3 n, Object instigator = null)
    {
        if (!IsAlive || Time.time < nextHit) return;
        nextHit = Time.time + hitCooldown;
        currentHP = Mathf.Max(0, currentHP - amount);
        UpdateUI();
        if (currentHP == 0) Die();
    }

    private void UpdateUI()
    {
        if (hpSlider != null)
            hpSlider.value = (float)currentHP / maxHP;
    }

    private void Die()
    {
        // 죽음 처리(애니메이션 등) 필요 시 추가
        if (destroyOnDeath) Destroy(gameObject);
    }
}