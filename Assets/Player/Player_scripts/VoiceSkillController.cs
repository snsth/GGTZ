#if UNITY_STANDALONE_WIN || UNITY_WSA
using UnityEngine;
using UnityEngine.Windows.Speech;
using System;

public class VoiceSkillController : MonoBehaviour
{
    private KeywordRecognizer keywordRecognizer;
    private readonly string[] skillWords = { "meteor", "heal", "buff" };

    // 음성 입력 실패 대비 변수
    [SerializeField] private float failThreshold = 3f;
    private float lastRecognizedTime = 0f;
    private bool waitingForRecognize = false;

    void Start()
    {
        try
        {
            keywordRecognizer = new KeywordRecognizer(skillWords, ConfidenceLevel.Medium);
            keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
            keywordRecognizer.Start();
            Debug.Log("[VoiceSkill] KeywordRecognizer started");
        }
        catch (Exception e)
        {
            Debug.LogError("[VoiceSkill] Failed to start recognizer: " + e.Message);
            enabled = false;
        }
    }

    void OnDisable()
    {
        StopAndDisposeRecognizer();
    }

    void OnDestroy()
    {
        StopAndDisposeRecognizer();
    }

    private void StopAndDisposeRecognizer()
    {
        if (keywordRecognizer != null)
        {
            keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;
            if (keywordRecognizer.IsRunning) keywordRecognizer.Stop();
            keywordRecognizer.Dispose();
            keywordRecognizer = null;
        }
    }

    void Update()
    {
        if (waitingForRecognize && Time.time - lastRecognizedTime > failThreshold)
        {
            waitingForRecognize = false;
            OnVoiceFail();
        }
    }

    // 이벤트 핸들러(한 번만 정의)
    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        lastRecognizedTime = Time.time;
        waitingForRecognize = false;

        string word = args.text.ToLowerInvariant();
        switch (word)
        {
            case "meteor":
                CastMeteor();
                break;
            case "heal":
                CastHeal();
                break;
            case "buff":
                CastBuff();
                break;
            default:
                Debug.Log("[VoiceSkill] Unhandled word: " + word);
                break;
        }
    }

    // 외부에서 “이번에 말해보세요” 같은 타이밍에 호출
    public void StartListening()
    {
        waitingForRecognize = true;
        lastRecognizedTime = Time.time;
    }

    private void OnVoiceFail()
    {
        string detectedLanguage = DetectApproxLanguage();
        Debug.Log("[VoiceSkill] Voice input failed. Detected language: " + detectedLanguage);
    }

    private string DetectApproxLanguage()
    {
        // KeywordRecognizer는 언어 감지를 제공하지 않습니다. 필요 시 외부 라이브러리 사용.
        return "unknown";
    }

    private void CastMeteor()
    {
        Debug.Log("[VoiceSkill] Meteor activated");
        // 실제 스킬 로직 호출
    }

    private void CastHeal()
    {
        Debug.Log("[VoiceSkill] Heal activated");
    }

    private void CastBuff()
    {
        Debug.Log("[VoiceSkill] Buff activated");
    }
}
#else
using UnityEngine;
public class VoiceSkillController : MonoBehaviour
{
void Start()
{
Debug.LogWarning("[VoiceSkill] Windows Speech API는 이 플랫폼에서 지원되지 않습니다.");
enabled = false;
}
}
#endif