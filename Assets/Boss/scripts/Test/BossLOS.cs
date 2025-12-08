// 시야(LOS) 계산.눈 위치에서 플레이어까지 레이캐스트

using UnityEngine;

[DisallowMultipleComponent]
public class BossLOS : MonoBehaviour
{
    [Header("LOS (optional)")]
    public bool useLOS = false;
    public Transform eye;
    public LayerMask losBlockMask;
    public float losMaxDistance = 40f;

    public bool HasLineOfSight(Transform player)
    {
        if (!useLOS) return true;
        if (!eye || !player) return true;

        Vector3 toHead = (player.position + Vector3.up * 1.0f) - eye.position;
        float dist = toHead.magnitude;
        if (dist > losMaxDistance) return false;

        return !Physics.Raycast(eye.position, toHead.normalized, dist, losBlockMask, QueryTriggerInteraction.Ignore);
    }
}
