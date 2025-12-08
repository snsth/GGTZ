using System.Collections.Generic;
using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [Header("HP")]
    public float maxHp = 100f;
    public float currentHp = 100f;

    [Header("UI (x 이동)")]
    public RectTransform hpFill;   // GameManager.BossHP를 드래그
    public float xAtFull = 0f;
    public float xAtZero = -300f;
    public float uiLerpSpeed = 12f;

    float targetX;

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
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        currentHp = Mathf.Max(0f, currentHp - amount);
        UpdateHpUI();
        if (currentHp <= 0f)
        {
            // TODO: 사망 처리
        }
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
        var p = hpFill.anchoredPosition;
        p.x = x;
        hpFill.anchoredPosition = p;
    }
}