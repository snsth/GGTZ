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
    public float fallbackDuration = 0.9f; // 애니 이벤트를 못 걸었을 때 종료 타임(초)
    public bool logDebug = true;

    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private BossBrain brain;

    private bool rolling = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        brain = GetComponent<BossBrain>();

        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false; // 루트모션에서 중력 사용 안 함
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    public bool TryRollLeft() => TryStartRoll(rollLeftTrigger);
    public bool TryRollRight() => TryStartRoll(rollRightTrigger);
    public bool TryRollForward() => TryStartRoll(rollForwardTrigger);
    public bool TryRollBack() => TryStartRoll(rollBackTrigger);

    bool TryStartRoll(string trigger)
    {
        if (rolling) return false;

        StartCoroutine(RollRoutine(trigger));
        return true;
    }

    IEnumerator RollRoutine(string trigger)
    {
        rolling = true;

        // AI와 NavMeshAgent 일시 정지
        if (brain) brain.BeginExternalAction();

        bool prevEnabled = agent.enabled;
        if (agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false; // 루트모션과 충돌 방지
        }

        // 루트모션으로 실제 이동
        animator.applyRootMotion = true;
        animator.ResetTrigger("Idle");
        animator.SetTrigger(trigger);

        if (logDebug) Debug.Log("[Roll] start: " + trigger);

        // 종료는 애니메이션 이벤트 OnRollEnd()로 받는 것을 권장.
        // 이벤트가 없다면 fallbackDuration 후 자동 종료.
        float t = 0f;
        while (t < fallbackDuration && rolling)
        {
            t += Time.deltaTime;
            yield return null;
        }

        EndRollInternal(prevEnabled);

        if (logDebug) Debug.Log("[Roll] end: " + trigger);
    }

    // 애니메이션 이벤트로 호출(롤 클립 끝에 이벤트 배치, 함수명: OnRollEnd)
    public void OnRollEnd()
    {
        if (!rolling) return;
        EndRollInternal(prevAgentEnabledCache: true); // prevAgentEnabled는 내부에서 기억되지 않았으므로 true로 재활성화 시도
    }

    void EndRollInternal(bool prevAgentEnabledCache)
    {
        rolling = false;

        animator.applyRootMotion = false;

        // NavMeshAgent 재활성화 + 현재 위치로 동기화
        if (!agent.enabled)
        {
            // 현재 위치가 NavMesh 위인지 확인 후 Warp
            Vector3 pos = transform.position;
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                agent.Warp(pos); // 확실하지 않음: NavMesh 밖이면 경로 추적이 바로 되지 않을 수 있음

            agent.enabled = true;
            agent.isStopped = false;
        }

        if (brain) brain.EndExternalAction();
    }
}