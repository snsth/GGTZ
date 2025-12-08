using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossAbilityRunner : MonoBehaviour
{
    [Header("Auto Discover")]
    public bool autoDiscover = true;

    private Dictionary<VoiceAction, IBossAbility> map = new Dictionary<VoiceAction, IBossAbility>();
    private Dictionary<VoiceAction, float> lastUsed = new Dictionary<VoiceAction, float>();

    void Awake()
    {
        if (!autoDiscover) return;

        var abilities = GetComponents<MonoBehaviour>();
        foreach (var m in abilities)
        {
            if (m is IBossAbility a)
            {
                if (!map.ContainsKey(a.Action))
                    map.Add(a.Action, a);
            }
        }
    }

    public void Register(IBossAbility ability)
    {
        if (!map.ContainsKey(ability.Action))
            map.Add(ability.Action, ability);
    }

    public void Trigger(VoiceAction action)
    {
        if (!map.TryGetValue(action, out var ability)) return;

        float cd = ability.Cooldown;
        if (!lastUsed.ContainsKey(action)) lastUsed[action] = -999f;
        if (Time.time - lastUsed[action] < cd) return;

        if (ability.TryStart())
            lastUsed[action] = Time.time;
    }
}