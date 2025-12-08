// 또는 다른 트리거)로 롤을 실행. 롤 동안 NavMeshAgent를 꺼서 간섭 제거, 루트모션으로 실제 이동, 끝나면 Agent 재활성화+Warp.

using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class BossRollAbility : MonoBehaviour
{
    [Header("Animator Triggers")]
    public string rollLeftTrigger = "Roll_Left";
    public string rollRightTrigger = "Roll_Right";
    public string rollForwardTrigger = "Roll_Forward";
    public string rollBackTrigger = "Roll_Back";

    [Header("Safety")]
    public float fallbackDuration = 0.9f; // 이벤트가 없을 때 자동 종료 시간
    public bool logDebug = true;

    public bool IsRolling => rolling;
    public string LastRollTrigger { get; private set; } = "";

    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private BossBrain brain;

    private bool rolling = false;
    private bool endByEvent = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        brain = GetComponent<BossBrain>();

        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    public bool TryRollLeft() => TryStartRoll(rollLeftTrigger);
    public bool TryRollRight() => TryStartRoll(rollRightTrigger);
    public bool TryRollForward() => TryStartRoll(rollForwardTrigger);
    public bool TryRollBack() => TryStartRoll(rollBackTrigger);

    bool TryStartRoll(string trigger)
    {
        if (rolling) { if (logDebug) Debug.Log("[Roll] busy"); return false; }

        StartCoroutine(RollRoutine(trigger));
        return true;
    }

    IEnumerator RollRoutine(string trigger)
    {
        rolling = true;
        endByEvent = false;
        LastRollTrigger = trigger;

        if (brain) brain.BeginExternalAction();

        if (agent.enabled)
        {
            if (logDebug) Debug.Log("[Roll] disabling NavMeshAgent");
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        animator.applyRootMotion = true;
        animator.ResetTrigger("Idle");
        animator.SetTrigger(trigger);
        if (logDebug) Debug.Log("[Roll] start trigger=" + trigger);

        float t = 0f;
        while (!endByEvent && t < fallbackDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        EndRollInternal();
        if (logDebug) Debug.Log("[Roll] end trigger=" + trigger);
    }

    // 롤 애니메이션 끝에서 이벤트로 호출
    public void OnRollEnd()
    {
        if (!rolling) return;
        if (logDebug) Debug.Log("[Roll] OnRollEnd event");
        endByEvent = true;
    }

    void EndRollInternal()
    {
        rolling = false;
        animator.applyRootMotion = false;

        if (!agent.enabled)
        {
            Vector3 pos = transform.position;
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                if (logDebug) Debug.Log("[Roll] Warp to NavMesh position");
            }
            else
            {
                agent.Warp(pos); // NavMesh 밖일 수도 있음(확실하지 않음)
                if (logDebug) Debug.LogWarning("[Roll] Warp to current pos (off NavMesh)");
            }
            agent.enabled = true;
            agent.isStopped = false;
            if (logDebug) Debug.Log("[Roll] NavMeshAgent re-enabled");
        }

        if (brain) brain.EndExternalAction();
    }
}
