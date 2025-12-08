using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IBossAbility
{
    VoiceAction Action { get; }   // 이 능력이 담당하는 액션
    bool IsRunning { get; }       // 실행 중?
    float Cooldown { get; }       // 쿨타임(초)
    bool TryStart();              // 실행 시도(성공 true)
}