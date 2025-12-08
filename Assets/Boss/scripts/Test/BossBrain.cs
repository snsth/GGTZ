// 상태 전이, 각 모듈 호출, 애니메이터 파라미터 업데이트, 공격 시작/종료 제어

using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BossLocomotion))]
[RequireComponent(typeof(BossAttackSelector))]
[RequireComponent(typeof(BossAnimatorBridge))]
public class BossBrain : MonoBehaviour
{
    [Header("Target")]
    public Transform player; // 비우면 태그 "Player" 탐색

    private NavMeshAgent agent;
    private Animator animator;
    private BossLocomotion locomotion;
    private BossAttackSelector attackSelector;

    private enum State { Chase, Strafe, Attacking, Recover }
    private State state = State.Chase;

    // 외부 액션(롤/점프 등) 중에는 AI 갱신 중지
    public bool IsExternalBusy { get; private set; } = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        locomotion = GetComponent<BossLocomotion>();
        attackSelector = GetComponent<BossAttackSelector>();
    }

    void Start()
    {
        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
        }

        if (agent && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
    }

    void Update()
    {
        if (!player || !agent || !locomotion) return;

        // 외부 액션 중이면 로코모션/공격 판단 정지
        if (IsExternalBusy)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("DistanceToPlayer", Vector3.Distance(transform.position, player.position));
            return;
        }

        Vector3 toTarget = player.position - transform.position;
        float distance = toTarget.magnitude;

        if (state != State.Attacking)
            state = ChooseLocomotionState(distance);

        if (state == State.Chase)
        {
            locomotion.EnableAgentLocomotion(true);
            locomotion.TickChase(player);

            var chosen = attackSelector.TrySelect(distance, player);
            if (chosen != null) BeginAttack(chosen);
        }
        else if (state == State.Strafe)
        {
            locomotion.EnableAgentLocomotion(true);
            locomotion.TickStrafe(player);

            var chosen = attackSelector.TrySelect(distance, player);
            if (chosen != null) BeginAttack(chosen);
        }

        if (state != State.Attacking)
            locomotion.SyncAgentNextPosition();

        animator.SetFloat("Speed", locomotion.AgentSpeed);
        animator.SetFloat("DistanceToPlayer", distance);
    }

    State ChooseLocomotionState(float distance)
    {
        float farEnter = locomotion.farEnter;
        float midEnter = locomotion.midEnter;
        float hysteresis = locomotion.hysteresis;

        if (distance > farEnter + hysteresis) return State.Chase;
        if (distance < midEnter - hysteresis) return State.Chase;
        if (distance > midEnter - hysteresis && distance < farEnter + hysteresis) return State.Strafe;
        return State.Chase;
    }

    void BeginAttack(AttackOption opt)
    {
        state = State.Attacking;

        locomotion.EnableAgentLocomotion(false);

        animator.applyRootMotion = true;
        animator.ResetTrigger("Idle");
        animator.SetTrigger(opt.animatorTrigger);

        opt.MarkUsed();
    }

    // 애니메이션 이벤트에서 호출
    public void EndAttack()
    {
        animator.applyRootMotion = false;
        locomotion.EnableAgentLocomotion(true);
        state = State.Recover;
    }

    // 외부 액션 시작/종료(BossRollAbility 등에서 호출)
    public void BeginExternalAction()
    {
        IsExternalBusy = true;
        locomotion.EnableAgentLocomotion(false);
    }

    public void EndExternalAction()
    {
        IsExternalBusy = false;
        locomotion.EnableAgentLocomotion(true);
    }
}