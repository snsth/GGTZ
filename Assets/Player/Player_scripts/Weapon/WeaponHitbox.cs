using System.Collections.Generic;
using UnityEngine;

public class WeaponHitbox : MonoBehaviour
{
    public int damage = 10;
    public Collider hitbox;              // isTrigger = true
    public LayerMask targetLayers;       // Enemy/Boss 레이어들만 포함
    public Transform owner;              // 플레이어 Transform (피해 가해자)

    private readonly HashSet<Collider> alreadyHit = new HashSet<Collider>();

    void Awake()
    {
        if (hitbox != null) hitbox.enabled = false;
    }

    public void Open()
    {
        alreadyHit.Clear();
        if (hitbox != null) hitbox.enabled = true;
    }

    public void Close()
    {
        if (hitbox != null) hitbox.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitbox == null || !hitbox.enabled) return;
        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;
        if (alreadyHit.Contains(other)) return;
        alreadyHit.Add(other);

        var dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null)
        {
            Vector3 hp = other.ClosestPoint(transform.position);
            Vector3 hn = -transform.forward;
            dmg.TakeDamage(damage, hp, hn, owner);
        }
        // 상대가 IDamageable을 구현하지 않았다면, 보스 기존 스크립트에 어댑터를 하나 달아주세요.
    }
}
