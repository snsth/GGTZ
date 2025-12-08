using UnityEngine;

public class PlayerDamageable : MonoBehaviour, IDamageable
{
    public PlayerHealth playerHealth;

    // 무적 상태
    public bool invulnerable { get; private set; }
    private float invulnerableUntil;

    public bool IsAlive => playerHealth != null && playerHealth.IsAlive;

    void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (invulnerable && Time.time >= invulnerableUntil)
            invulnerable = false;
    }

    public void SetInvulnerableFor(float duration)
    {
        invulnerable = true;
        invulnerableUntil = Time.time + duration;
    }

    public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal, UnityEngine.Object instigator = null)
    {
        if (!IsAlive || invulnerable) return;
        playerHealth.TakeDamage(amount);
    }
}