// Windows 음성 인식(문장 단위), 결과 텍스트 이벤트로 전달

using UnityEngine;
using System;
using System.Collections;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

[DisallowMultipleComponent]
public class VoiceInputWindows : MonoBehaviour
{
    [Header("Behavior")]
    public bool autoStart = true;            // 활성화 시 자동 시작
    public bool autoRestart = true;          // 멈추면 자동 재시작
    public float restartDelay = 0.35f;       // 재시작 지연(초)
    public bool setRunInBackground = true;   // 백그라운드에서도 실행

    [Header("Debug")]
    public bool logDebug = true;
    public string Status { get; private set; } = "Idle";
    public string LastText { get; private set; } = "";
    public string LastHypothesis { get; private set; } = "";

    public event Action<string> OnRecognized;
    public event Action<string> OnHypothesis;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private DictationRecognizer dictation;
#endif

    [Header("Watchdog")]
    public bool enableWatchdog = true;
    public float watchdogInterval = 3.0f;
    private float watchdogTimer = 0f;

    void Awake()
    {
        if (setRunInBackground) Application.runInBackground = true;
    }

    void OnEnable()
    {
        if (autoStart) StartRecognition();
    }

    void OnDisable()
    {
        StopRecognition();
    }

    void Update()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!enableWatchdog) return;
        watchdogTimer -= Time.unscaledDeltaTime;
        if (watchdogTimer <= 0f)
        {
            watchdogTimer = watchdogInterval;
            if (autoRestart && (dictation == null || dictation.Status != SpeechSystemStatus.Running))
            {
                if (logDebug) Debug.Log("[Voice] Watchdog restart");
                StartCoroutine(RestartAfterDelay(restartDelay));
            }
        }
#endif
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus && autoRestart) StartCoroutine(RestartAfterDelay(0.25f));
    }

    void OnApplicationPause(bool pause)
    {
        if (!pause && autoRestart) StartCoroutine(RestartAfterDelay(0.25f));
    }

    public void StartRecognition()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            if (dictation == null)
            {
                dictation = new DictationRecognizer(ConfidenceLevel.Medium);

                // 이벤트 구독(명명된 핸들러 사용)
                dictation.DictationResult += HandleResult;
                dictation.DictationHypothesis += HandleHypothesis;
                dictation.DictationComplete += HandleComplete;
                dictation.DictationError += HandleError;
            }

            dictation.Start();
            Status = "Running";
            if (logDebug) Debug.Log("[Voice] DictationRecognizer started");
        }
        catch (Exception e)
        {
            Status = "Start failed: " + e.Message;
            Debug.LogWarning("[Voice] Start failed: " + e.Message);
            if (autoRestart) StartCoroutine(RestartAfterDelay(1.0f));
        }
#else
Status = "Unsupported platform";
Debug.LogWarning("[Voice] Windows 전용 기능입니다.");
#endif
    }

    public void StopRecognition()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            if (dictation != null)
            {
                // 이벤트 해제는 -= 로
                dictation.DictationResult -= HandleResult;
                dictation.DictationHypothesis -= HandleHypothesis;
                dictation.DictationComplete -= HandleComplete;
                dictation.DictationError -= HandleError;

                if (dictation.Status == SpeechSystemStatus.Running) dictation.Stop();
                dictation.Dispose();
                dictation = null;
            }
            Status = "Stopped";
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Voice] Stop failed: " + e.Message);
        }
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // 이벤트 핸들러(명명된 메서드)
    void HandleResult(string text, ConfidenceLevel conf)
    {
        LastText = text;
        if (logDebug) Debug.Log($"[Voice] Result({conf}): {text}");
        OnRecognized?.Invoke(text);
    }

    void HandleHypothesis(string text)
    {
        LastHypothesis = text;
        if (logDebug) Debug.Log($"[Voice] Hypothesis: {text}");
        OnHypothesis?.Invoke(text);
    }

    void HandleComplete(DictationCompletionCause cause)
    {
        Status = "Complete: " + cause;
        if (logDebug) Debug.Log("[Voice] Complete: " + cause);
        if (autoRestart) StartCoroutine(RestartAfterDelay(restartDelay));
    }

    void HandleError(string error, int hresult)
    {
        Status = $"Error: {error} (0x{hresult:X})";
        Debug.LogWarning($"[Voice] Error: {error} (0x{hresult:X})");
        if (autoRestart) StartCoroutine(RestartAfterDelay(restartDelay));
    }

    IEnumerator RestartAfterDelay(float delay)
    {
        // 중복 재시작 방지용 간단 표기
        if (Status.StartsWith("Restarting")) yield break;

        Status = "Restarting...";
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, delay));
        StopRecognition();
        yield return new WaitForSecondsRealtime(0.05f);
        StartRecognition();
    }
#endif
}