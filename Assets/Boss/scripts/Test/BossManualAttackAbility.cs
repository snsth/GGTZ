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
    public string jumpAttackTrigger = "Voice_JumpAttack"; // 점프 공격을 이 트리거로 실행(원하면 비워둬도 됨)
    public string FallAttackTrigger = "Voice_FallAttack";
    public string KickAttackTrigger = "Voice_Kick";

    [Header("Timing")]
    public float fallbackDuration = 0.9f; // 애니 이벤트가 없을 때 자동 종료 시간(초)
    public bool logDebug = true;

    // 현재 액션 상태
    public bool InAction { get; private set; } = false;
    public string LastTrigger { get; private set; } = "";

    // 히트박스: 공격 구간에만 잠깐 켤 Trigger 콜라이더들(처음엔 반드시 꺼둬야 함)
    [Header("Hitboxes (Trigger colliders, disabled by default)")]
    public Collider[] attackHitboxes;     // Voice_Attack 시 켤 히트박스들
    public Collider[] punchHitboxes;      // Voice_Punch 시 켤 히트박스들
    public Collider[] jumpAttackHitboxes; // Voice_JumpAttack 시 켤 히트박스들(사용 시)

    private Collider[] currentHitGroup = null;

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

        // 시작 시 모든 히트박스 OFF
        DisableAllHitboxes();
    }

    public bool TryAttack() => TryStart(attackTrigger);
    public bool TryPunch() => TryStart(punchTrigger);
    public bool TryJumpAttack() => TryStart(jumpAttackTrigger);
    public bool TryFallAttack() => TryStart(FallAttackTrigger);
    public bool TryKick() => TryStart(KickAttackTrigger);

    bool TryStart(string trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return false;
        if (InAction) { if (logDebug) Debug.Log("[ManualAttack] Busy"); return false; }

        // 이번 트리거에 맞는 히트박스 그룹 선택
        if (trigger == attackTrigger) currentHitGroup = attackHitboxes;
        else if (trigger == punchTrigger) currentHitGroup = punchHitboxes;
        else if (trigger == jumpAttackTrigger) currentHitGroup = jumpAttackHitboxes;
        else currentHitGroup = null;

        StartCoroutine(ActionRoutine(trigger));
        return true;
    }

    IEnumerator ActionRoutine(string trigger)
    {
        InAction = true;
        endByEvent = false;
        LastTrigger = trigger;

        if (brain) brain.BeginExternalAction();

        // NavMeshAgent 일시 비활성화(러버밴딩 방지)
        if (agent.enabled)
        {
            if (logDebug) Debug.Log("[ManualAttack] disabling NavMeshAgent");
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        // 루트모션 사용(필요 시) — 프로젝트 운영 방식에 맞게 유지/삭제 가능
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

    // 애니메이션 끝에서 호출(클립 끝 95% 지점에 Animation Event로 추가)
    public void OnVoiceActionEnd()
    {
        endByEvent = true;
    }

    void EndInternal()
    {
        InAction = false;

        // 혹시 이벤트 누락 대비: 히트박스 무조건 OFF
        ToggleCurrentHitboxes(false);

        animator.applyRootMotion = false;

        // NavMeshAgent 복구 + 위치 동기화
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

    // 애니메이션 이벤트(타격 창 시작/끝)에 붙일 함수 — 파라미터 없음
    public void HitboxOn() { ToggleCurrentHitboxes(true); }
    public void HitboxOff() { ToggleCurrentHitboxes(false); }

    void ToggleCurrentHitboxes(bool on)
    {
        if (currentHitGroup == null) return;
        for (int i = 0; i < currentHitGroup.Length; i++)
        {
            var c = currentHitGroup[i];
            if (!c) continue;
            c.enabled = on;
        }
    }

    void DisableAllHitboxes()
    {
        void OffAll(Collider[] arr)
        {
            if (arr == null) return;
            foreach (var c in arr) if (c) c.enabled = false;
        }
        OffAll(attackHitboxes);
        OffAll(punchHitboxes);
        OffAll(jumpAttackHitboxes);
    }
}