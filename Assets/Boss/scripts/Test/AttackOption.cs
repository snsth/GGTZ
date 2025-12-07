using UnityEngine;

[System.Serializable]
public class AttackOption
{
    public string name;
    public string animatorTrigger = "AttackA";     // Animator 트리거 이름
    public float minRange = 0f;
    public float maxRange = 3f;
    public float cooldown = 3f;
    public float weight = 1f;
    public AnimationCurve distanceCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public bool requiresLOS = false;
    [Range(-1f, 1f)] public float requiredFacingDot = 0.0f; // 0.0 ~= 전방 90도 이내

    [HideInInspector] public float lastUsedTime = -999f;

    public bool IsInRange(float d) => d >= minRange && d <= maxRange;
    public float NormalizedRange(float d)
    {
        if (maxRange <= minRange) return 1f;
        return Mathf.Clamp01((d - minRange) / (maxRange - minRange));
    }
    public bool IsOffCooldown() => Time.time >= lastUsedTime + cooldown;
    public float CooldownFactor()
    {
        float t = Mathf.Clamp01((Time.time - lastUsedTime) / cooldown);
        return Mathf.Lerp(0.25f, 1f, t); // 직전 반복 억제
    }
    public void MarkUsed() => lastUsedTime = Time.time;
}