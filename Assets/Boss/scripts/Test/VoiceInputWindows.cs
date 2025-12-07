// Windows 음성 인식(문장 단위), 결과 텍스트 이벤트로 전달

using UnityEngine;
using System;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

[DisallowMultipleComponent]
public class VoiceInputWindows : MonoBehaviour
{
    public bool autoStart = true;
    public bool logDebug = true;

    // 디버그용
    public string Status { get; private set; } = "Idle";
    public string LastText { get; private set; } = "";
    public string LastHypothesis { get; private set; } = "";

    public event Action<string> OnRecognized;
    public event Action<string> OnHypothesis;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private DictationRecognizer dictation;
#endif

    void OnEnable()
    {
        if (autoStart) StartRecognition();
    }

    void OnDisable()
    {
        StopRecognition();
    }

    public void StartRecognition()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            Status = "Starting";
            if (dictation == null)
            {
                dictation = new DictationRecognizer(ConfidenceLevel.Medium);
                dictation.DictationResult += (text, conf) =>
                {
                    LastText = text;
                    if (logDebug) Debug.Log($"[Voice] Result({conf}): {text}");
                    OnRecognized?.Invoke(text);
                };
                dictation.DictationHypothesis += (text) =>
                {
                    LastHypothesis = text;
                    if (logDebug) Debug.Log($"[Voice] Hypothesis: {text}");
                    OnHypothesis?.Invoke(text);
                };
                dictation.DictationComplete += cause =>
                {
                    Status = "Complete: " + cause;
                    if (logDebug) Debug.Log("[Voice] Complete: " + cause);
                    if (cause != DictationCompletionCause.Complete)
                    {
                        try { dictation.Start(); Status = "Restarting"; } catch { }
                    }
                };
                dictation.DictationError += (err, hr) =>
                {
                    Status = $"Error: {err} (0x{hr:X})";
                    Debug.LogWarning($"[Voice] Error: {err} (0x{hr:X})");
                    try { dictation.Stop(); dictation.Start(); Status = "Restarting"; } catch { }
                };
            }
            dictation.Start();
            Status = "Running";
            if (logDebug) Debug.Log("[Voice] DictationRecognizer started");
        }
        catch (Exception e)
        {
            Status = "Start failed: " + e.Message;
            Debug.LogWarning("[Voice] Start failed: " + e.Message);
        }
#else
        Status = "Unsupported platform";
        Debug.LogWarning("[Voice] Windows 전용 기능입니다.");
#endif
    }

    public void StopRecognition()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (dictation != null)
        {
            if (dictation.Status == SpeechSystemStatus.Running) dictation.Stop();
            dictation.Dispose();
            dictation = null;
        }
        Status = "Stopped";
#endif
    }
}
