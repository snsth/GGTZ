using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("HP")]
    public float maxHp = 100f;
    public float currentHp = 100f;

    [Header("UI (x 이동)")]
    public RectTransform hpFill;
    public float xAtFull = 0f;
    public float xAtZero = -300f;
    public float uiLerpSpeed = 12f;

    [Header("Events")]
    public UnityEvent onDeath;
    public event Action<float, float> onHpChanged; // (current, max)

    [Header("Death")]
    public Animator animator;
    public string deathParameter = "Death";
    public bool deathParameterIsTrigger = false; // Trigger면 true, Bool이면 false
    public MonoBehaviour[] disableOnDeath;       // 이동/입력 스크립트들을 여기 넣기 (예: PlayerMovement, CharacterController 등)
    public bool makeRigidbodyKinematic = true;   // 리지드바디가 있다면 죽으면 정지

    float targetX;
    bool deathHandled = false;
    public bool IsAlive => currentHp > 0f;

    void Awake()
    {
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(); // 자동 할당 시도

        if (hpFill != null)
        {
            if (Mathf.Approximately(xAtFull, xAtZero))
            {
                xAtFull = hpFill.anchoredPosition.x;
                xAtZero = xAtFull - hpFill.rect.width;
            }
            targetX = xAtFull;
            SetFillXImmediate(targetX);
        }

        onHpChanged?.Invoke(currentHp, maxHp);

        // 이미 0이면 즉시 사망 처리
        if (!IsAlive)
            Die();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive) return;

        currentHp = Mathf.Max(0f, currentHp - amount);
        UpdateHpUI();
        onHpChanged?.Invoke(currentHp, maxHp);

        if (!IsAlive)
            Die();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || !IsAlive) return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        UpdateHpUI();
        onHpChanged?.Invoke(currentHp, maxHp);
    }

    void Die()
    {
        if (deathHandled) return;
        deathHandled = true;

        // 애니메이터 Death 파라미터 설정
        if (animator != null && !string.IsNullOrEmpty(deathParameter))
        {
            if (deathParameterIsTrigger) animator.SetTrigger(deathParameter);
            else animator.SetBool(deathParameter, true);
        }

        // 이동/입력 등 비활성화
        if (disableOnDeath != null)
        {
            foreach (var m in disableOnDeath)
            {
                if (m != null) m.enabled = false;
            }
        }

        // 리지드바디 정지/키네마틱 처리(옵션)
        if (makeRigidbodyKinematic)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        onDeath?.Invoke();
    }

    void UpdateHpUI()
    {
        if (hpFill == null) return;
        float t = (maxHp <= 0f) ? 0f : (currentHp / maxHp);
        targetX = Mathf.Lerp(xAtZero, xAtFull, t);
    }

    void Update()
    {
        if (hpFill == null) return;
        var p = hpFill.anchoredPosition;
        p.x = Mathf.Lerp(p.x, targetX, Time.deltaTime * uiLerpSpeed);
        hpFill.anchoredPosition = p;
    }

    void SetFillXImmediate(float x)
    {
        var p = hpFill.anchoredPosition; p.x = x; hpFill.anchoredPosition = p;
    }
}