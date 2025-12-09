// 인식된 텍스트를 퍼지 매칭(유사어 허용)으로 액션으로 변환하고 롤 실행을 호출.

using System;
using System.Collections.Generic;
using UnityEngine;

public enum VoiceAction
{
    None,
    RollLeft, RollRight, RollForward, RollBack,
    Jump,
    Attack,
    Punch,
    Fall,
    Kick
}

[Serializable]
public class CommandKeywordSet
{
    public VoiceAction action = VoiceAction.RollLeft;
    public List<string> keywords = new List<string>();
    public float cooldown = 0.8f;
    [Tooltip("점수가 같을 때 높은 값이 우선. Punch(10) > Attack(5)처럼 설정 권장")]
    public int priority = 5;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(VoiceInputWindows))]
public class VoiceCommandRouter : MonoBehaviour
{
    [Header("Dependencies")]
    public BossRollAbility roll;
    public BossManualAttackAbility manualAttack;

    [Header("Fuzzy Matching")]
    [Range(0f, 1f)] public float fuzzyThreshold = 0.55f;

    [Header("Commands")]
    public List<CommandKeywordSet> commands = new List<CommandKeywordSet>()
{
    // 롤
    new CommandKeywordSet{ action=VoiceAction.RollLeft,   cooldown=0.8f, keywords=new List<string>{"왼쪽","왼","좌","레프트","left","왼쪽으로","왼구르기","왼굴러","렝쪽","랭쪽"} },
    new CommandKeywordSet{ action=VoiceAction.RollRight,  cooldown=0.8f, keywords=new List<string>{"오른쪽","오른","우","라이트","right","오른쪽으로","오구르기","오굴러"} },
    new CommandKeywordSet{ action=VoiceAction.RollForward,cooldown=0.8f, keywords=new List<string>{"앞","앞으로","전진","포워드","forward","앞구르기"} },
    new CommandKeywordSet{ action=VoiceAction.RollBack,   cooldown=0.8f, keywords=new List<string>{"뒤","뒤로","후진","백","back","뒤구르기"} },

    // 점프
    new CommandKeywordSet{ action=VoiceAction.Jump,       cooldown=1.0f, keywords=new List<string>{"점프","점푸","잠푸","잠피","jump","점프해"} },

    // 공격(칼/베기 같은 일반 공격)
    new CommandKeywordSet{ action=VoiceAction.Attack,     cooldown=0.8f, keywords=new List<string>{"공격","어택","attack","어택해","쳐","치기"} },

    // 펀치(주먹질)
    new CommandKeywordSet{ action=VoiceAction.Punch,      cooldown=0.8f, keywords=new List<string>{"펀치","주먹","퍈치","punch","펀치해"} },
};

    [Header("Debug")]
    public bool allowKeyboardTest = true; // Q/E/R/T=roll, Y=jump, U=attack, I=punch
    public bool logDebug = true;

    public string LastRaw { get; private set; } = "";
    public VoiceAction LastAction { get; private set; } = VoiceAction.None;
    public float LastScore { get; private set; } = 0f;

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

        if (Input.GetKeyDown(KeyCode.Y)) Trigger(VoiceAction.Jump);
        if (Input.GetKeyDown(KeyCode.U)) Trigger(VoiceAction.Attack);
        if (Input.GetKeyDown(KeyCode.I)) Trigger(VoiceAction.Punch);
        if (Input.GetKeyDown(KeyCode.O)) Trigger(VoiceAction.Fall);
        if (Input.GetKeyDown(KeyCode.P)) Trigger(VoiceAction.Kick);
    }

    void HandleRecognized(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        LastRaw = raw;

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
            if (bestScore >= 1f) break;
        }

        if (best != null && bestScore >= fuzzyThreshold)
        {
            LastAction = best.action;
            LastScore = bestScore;
            if (logDebug) Debug.Log($"[VoiceMatch] {best.action} score={bestScore:0.00} from '{raw}'");
            Trigger(best.action, best.cooldown);
        }
        else
        {
            LastAction = VoiceAction.None;
            LastScore = bestScore;
            if (logDebug) Debug.Log($"[VoiceMatch] no match for '{raw}' (best={bestScore:0.00})");
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

            case VoiceAction.Jump:manualAttack?.TryJumpAttack();break;
            case VoiceAction.Attack: manualAttack?.TryAttack(); break;
            case VoiceAction.Punch: manualAttack?.TryPunch(); break;
            case VoiceAction.Kick: manualAttack?.TryKick(); break;
            case VoiceAction.Fall: manualAttack?.TryFallAttack(); break;
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
                d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[n, m];
    }
}