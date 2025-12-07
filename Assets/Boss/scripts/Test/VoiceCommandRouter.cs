// 인식된 텍스트를 퍼지 매칭(유사어 허용)으로 액션으로 변환하고 롤 실행을 호출.

using System;
using System.Collections.Generic;
using UnityEngine;

public enum VoiceAction
{
    None,
    RollLeft,
    RollRight,
    RollForward,
    RollBack
}

[Serializable]
public class CommandKeywordSet
{
    public VoiceAction action = VoiceAction.RollLeft;
    public List<string> keywords = new List<string>();
    public float cooldown = 0.8f; // 연타 방지
}

[DisallowMultipleComponent]
[RequireComponent(typeof(VoiceInputWindows))]
public class VoiceCommandRouter : MonoBehaviour
{
    [Header("Dependencies")]
    public BossRollAbility roll;    // 롤 실행 대상

    [Header("Fuzzy Matching")]
    [Range(0f, 1f)] public float fuzzyThreshold = 0.55f;

    [Header("Commands")]
    public List<CommandKeywordSet> commands = new List<CommandKeywordSet>()
{
    new CommandKeywordSet{
        action = VoiceAction.RollLeft,
        cooldown = 0.8f,
        keywords = new List<string>{"왼쪽","왼","좌","레프트","left","왼쪽으로","왼다","왼구르기","왼굴러"}
    },
    new CommandKeywordSet{
        action = VoiceAction.RollRight,
        cooldown = 0.8f,
        keywords = new List<string>{"오른쪽","오른","우","라이트","right","오른쪽으로","오다","오구르기","오굴러"}
    },
    new CommandKeywordSet{
        action = VoiceAction.RollForward,
        cooldown = 0.8f,
        keywords = new List<string>{"앞","전진","앞으로","포워드","forward","앞구르기"}
    },
    new CommandKeywordSet{
        action = VoiceAction.RollBack,
        cooldown = 0.8f,
        keywords = new List<string>{"뒤","후진","뒤로","백","back","뒤구르기"}
    },
};

    [Header("Debug")]
    public bool allowKeyboardTest = true; // Q왼 E오 R앞 T뒤 테스트
    public bool logDebug = true;

    private VoiceInputWindows voice;
    private Dictionary<VoiceAction, float> lastUsed = new Dictionary<VoiceAction, float>();

    void Awake()
    {
        voice = GetComponent<VoiceInputWindows>();
        voice.OnRecognized += HandleRecognized;
    }

    void OnDestroy()
    {
        if (voice != null) voice.OnRecognized -= HandleRecognized;
    }

    void Update()
    {
        if (!allowKeyboardTest) return;

        if (Input.GetKeyDown(KeyCode.Q)) Trigger(VoiceAction.RollLeft);
        if (Input.GetKeyDown(KeyCode.E)) Trigger(VoiceAction.RollRight);
        if (Input.GetKeyDown(KeyCode.R)) Trigger(VoiceAction.RollForward);
        if (Input.GetKeyDown(KeyCode.T)) Trigger(VoiceAction.RollBack);
    }

    void HandleRecognized(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        string input = Normalize(raw);
        CommandKeywordSet best = null;
        float bestScore = 0f;

        foreach (var set in commands)
        {
            foreach (var key in set.keywords)
            {
                string k = Normalize(key);
                if (input.Contains(k)) { best = set; bestScore = 1f; break; }

                float score = Similarity(input, k);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = set;
                }
            }
        }

        if (best != null && bestScore >= fuzzyThreshold)
        {
            if (logDebug) Debug.Log($"[Voice] {best.action} matched score={bestScore:0.00}");
            Trigger(best.action, best.cooldown);
        }
        else
        {
            if (logDebug) Debug.Log($"[Voice] no match: {raw} (best {bestScore:0.00})");
        }
    }

    void Trigger(VoiceAction action, float cooldown = 0.8f)
    {
        if (!lastUsed.ContainsKey(action)) lastUsed[action] = -999f;
        if (Time.time - lastUsed[action] < cooldown) return;

        lastUsed[action] = Time.time;

        switch (action)
        {
            case VoiceAction.RollLeft: roll?.TryRollLeft(); break;
            case VoiceAction.RollRight: roll?.TryRollRight(); break;
            case VoiceAction.RollForward: roll?.TryRollForward(); break;
            case VoiceAction.RollBack: roll?.TryRollBack(); break;
        }
    }

    string Normalize(string s) => s.Trim().ToLower().Replace(" ", "");

    float Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0f;
        int dist = Levenshtein(a, b);
        int maxLen = Mathf.Max(a.Length, b.Length);
        return 1f - (float)dist / Mathf.Max(1, maxLen);
    }

    int Levenshtein(string s, string t)
    {
        int n = s.Length, m = t.Length;
        if (n == 0) return m; if (m == 0) return n;
        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(
                    Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        return d[n, m];
    }
}