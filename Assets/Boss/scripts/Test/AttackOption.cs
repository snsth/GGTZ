using UnityEngine;

[System.Serializable]
public class AttackOption
{
    public string name;
    public string animatorTrigger = "AttackA";
    public float minRange = 0f;
    public float maxRange = 3f;
    public float cooldown = 3f;
    public float weight = 1f;
    public AnimationCurve distanceCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public bool requiresLOS = false;
    [Range(-1f, 1f)] public float requiredFacingDot = 0.0f;

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
        return Mathf.Lerp(0.25f, 1f, t);
    }

    public void MarkUsed() => lastUsedTime = Time.time;
}