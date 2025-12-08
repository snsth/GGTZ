// 음성으로 강제 “공격/펀치” 실행. 루트모션 사용(애니에 이동이 있으면 실제 이동).

using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class BossManualAttackAbility : MonoBehaviour
{
    [Header("Animator Triggers")]
    public string attackTrigger = "Voice_Attack";
    public string punchTrigger = "Voice_Punch";

    [Header("Safety")]
    public float fallbackDuration = 0.9f; // 애니 이벤트가 없을 때 자동 종료 시간
    public bool logDebug = true;

    public bool InAction { get; private set; } = false;
    public string LastTrigger { get; private set; } = "";

    Animator animator;
    NavMeshAgent agent;
    Rigidbody rb;
    BossBrain brain;

    bool endByEvent = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        brain = GetComponent<BossBrain>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public bool TryAttack() => TryStart(attackTrigger);
    public bool TryPunch() => TryStart(punchTrigger);

    bool TryStart(string trigger)
    {
        if (InAction) return false;
        StartCoroutine(ActionRoutine(trigger));
        return true;
    }

    IEnumerator ActionRoutine(string trigger)
    {
        InAction = true;
        endByEvent = false;
        LastTrigger = trigger;

        if (brain) brain.BeginExternalAction();

        if (agent.enabled)
        {
            if (logDebug) Debug.Log("[ManualAttack] disabling NavMeshAgent");
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        animator.applyRootMotion = true;
        animator.ResetTrigger("Idle");
        animator.SetTrigger(trigger);
        if (logDebug) Debug.Log("[ManualAttack] start: " + trigger);

        float t = 0f;
        while (!endByEvent && t < fallbackDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        EndInternal();
        if (logDebug) Debug.Log("[ManualAttack] end: " + trigger);
    }

    // 애니메이션 끝에 이벤트를 달아 호출(함수명: OnVoiceActionEnd)
    public void OnVoiceActionEnd()
    {
        endByEvent = true;
    }

    void EndInternal()
    {
        InAction = false;
        animator.applyRootMotion = false;

        if (!agent.enabled)
        {
            Vector3 pos = transform.position;
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                agent.Warp(pos);

            agent.enabled = true;
            agent.isStopped = false;
            if (logDebug) Debug.Log("[ManualAttack] NavMeshAgent re-enabled");
        }

        if (brain) brain.EndExternalAction();
    }
}