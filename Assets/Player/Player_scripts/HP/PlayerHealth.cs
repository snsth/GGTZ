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

    [Header("UI (x ¿Ãµø)")]
    public RectTransform hpFill;
    public float xAtFull = 0f;
    public float xAtZero = -300f;
    public float uiLerpSpeed = 12f;

    [Header("Events")]
    public UnityEvent onDeath;
    public event Action<float, float> onHpChanged; // (current, max)

    float targetX;
    public bool IsAlive => currentHp > 0f;

    void Awake()
    {
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
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
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || !IsAlive) return;
        currentHp = Mathf.Max(0f, currentHp - amount);
        UpdateHpUI();
        onHpChanged?.Invoke(currentHp, maxHp);
        if (!IsAlive) onDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || !IsAlive) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        UpdateHpUI();
        onHpChanged?.Invoke(currentHp, maxHp);
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