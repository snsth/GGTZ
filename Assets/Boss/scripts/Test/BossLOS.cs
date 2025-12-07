// 시야(LOS) 계산.눈 위치에서 플레이어까지 레이캐스트

using UnityEngine;

[DisallowMultipleComponent]
public class BossLOS : MonoBehaviour
{
    [Header("LOS (optional)")]
    public bool useLOS = false;
    public Transform eye;                 // 눈 위치
    public LayerMask losBlockMask;        // 시야 차단 레이어
    public float losMaxDistance = 40f;    // 최대 시야 거리

    public bool HasLineOfSight(Transform player)
    {
        if (!useLOS) return true;
        if (!eye || !player) return true; // 확실하지 않음: 눈이 없으면 통과

        Vector3 toHead = (player.position + Vector3.up * 1.0f) - eye.position;
        float dist = toHead.magnitude;
        if (dist > losMaxDistance) return false;

        return !Physics.Raycast(
            eye.position, toHead.normalized, dist,
            losBlockMask, QueryTriggerInteraction.Ignore);
    }
}