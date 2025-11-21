using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class Boss_AI : MonoBehaviour
{
    [Header("Target")]
    public Transform player;                // 플레이어 Transform. 비워두면 태그 "Player" 자동 탐색
    public Transform eye;                   // 시야 체크용 눈 위치(선택)

    [Header("Nav / Movement")]
    public NavMeshAgent agent;
    public Animator animator;
    public Rigidbody rb;

    [Tooltip("초당 최대 회전 속도(도/초)")]
    public float maxTurnSpeedDeg = 360f;

    [Header("Distance Gates")]
    [Tooltip("중거리 상한(이 이하면 스트레이프 진입 고려)")]
    public float farEnter = 8f;
    [Tooltip("근거리 상한(이 이하면 공격 판단 활성)")]
    public float midEnter = 4f;
    [Tooltip("상태 전이 튐 방지 여유")]
    public float hysteresis = 0.5f;

    [Header("Strafe")]
    [Tooltip("스트레이프 유지 반경(플레이어 중심)")]
    public float desiredMidRadius = 5f;
    [Tooltip("스트레이프 방향 전환 주기(초)")]
    public float strafeSwitchInterval = 3f;
    float strafeDir = 1f;
    float strafeTimer;

    [Header("LOS (optional)")]
    public bool useLOS = false;             // 시야 체크 사용 여부
    public LayerMask losBlockMask;          // 시야 차단 레이어
    public float losMaxDistance = 40f;      // 시야 최대 거리

    [Header("Attacks")]
    public List<AttackOption> attacks = new List<AttackOption>();
    public float attackDecisionInterval = 0.2f;
    float decisionTimer;

    enum State { Chase, Strafe, Attacking, Recover }
    State state = State.Chase;

    void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
    }

    void Awake()
    {
        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;                         // NavMeshAgent와 혼용 안정화
        agent.updateRotation = false;                  // 회전은 수동
        agent.stoppingDistance = Mathf.Max(0.0f, agent.stoppingDistance);
    }

    void Start()
    {
        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform; // 확실하지 않음: 태그가 다르면 직접 할당 필요
        }

        // 시작 시 NavMesh 위로 보정(씬 로드/워프 안정화)
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }
    }

    void Update()
    {
        if (!player || !agent) return;

        float dt = Time.deltaTime;
        decisionTimer -= dt;
        strafeTimer -= dt;

        Vector3 toTarget = player.position - transform.position;
        float distance = toTarget.magnitude;

        // 상태 전이(Attacking은 애니메이션 이벤트로 종료)
        if (state != State.Attacking)
            state = ChooseLocomotionState(distance);

        // 상태 동작
        if (state == State.Chase)
        {
            EnableAgentLocomotion(true);
            Vector3 chasePos = PredictTargetPosition(player.position, Vector3.zero, 0.2f); // 플레이어 속도 연결 가능(확실하지 않음)
            SetDestinationSafe(chasePos);
            ManualRotateTowards(toTarget);
            TryDecideAttack(distance);
        }
        else if (state == State.Strafe)
        {
            EnableAgentLocomotion(true);
            if (strafeTimer <= 0f)
            {
                strafeDir = -strafeDir;
                strafeTimer = strafeSwitchInterval;
            }

            Vector3 center = player.position;
            Vector3 dirFromCenter = (transform.position - center);
            Vector3 dir = dirFromCenter.sqrMagnitude > 0.001f
                ? dirFromCenter.normalized
                : (-player.forward); // 초기 보정

            // 목표: 원형 궤도상 한 지점(접선 방향으로 살짝 밀어 스트레이프)
            Vector3 tangent = Vector3.Cross(Vector3.up, dir).normalized * strafeDir;
            Vector3 targetOnRing = center + dir * desiredMidRadius + tangent * 1.0f;

            SetDestinationSafe(targetOnRing);
            ManualRotateTowards(center - transform.position);
            TryDecideAttack(distance);
        }

        // Agent-Transform 동기화(공격 중이 아닐 때)
        if (state != State.Attacking)
        {
            agent.nextPosition = transform.position;
        }

        // 애니메이션 파라미터
        animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetFloat("DistanceToPlayer", distance);
    }

    State ChooseLocomotionState(float distance)
    {
        // 히스테리시스 적용
        if (distance > farEnter + hysteresis) return State.Chase;
        if (distance < midEnter - hysteresis) return State.Chase; // 근접은 추격 후 공격 판단
        // 중거리에서 측면 이동
        if (distance > midEnter - hysteresis && distance < farEnter + hysteresis) return State.Strafe;
        return State.Chase;
    }

    void TryDecideAttack(float distance)
    {
        if (decisionTimer > 0f) return;
        decisionTimer = attackDecisionInterval;

        AttackOption best = null;
        float bestScore = 0f;

        foreach (var opt in attacks)
        {
            if (!opt.IsInRange(distance)) continue;
            if (!opt.IsOffCooldown()) continue;

            // 시야 필요 시 체크
            if (useLOS && opt.requiresLOS && !HasLineOfSight()) continue;

            float score = opt.weight;

            // 거리 적합도(0~1)
            float norm = opt.NormalizedRange(distance);
            score *= opt.distanceCurve.Evaluate(norm);

            // 정면각 보정
            float facing = FacingFactor(player.position, opt.requiredFacingDot);
            score *= facing;

            // 최근 사용 억제
            score *= opt.CooldownFactor();

            if (score > bestScore)
            {
                bestScore = score;
                best = opt;
            }
        }

        if (best != null)
            BeginAttack(best);
    }

    void BeginAttack(AttackOption opt)
    {
        state = State.Attacking;
        EnableAgentLocomotion(false);
        animator.applyRootMotion = true;
        animator.ResetTrigger("Idle");
        animator.SetTrigger(opt.animatorTrigger);
        opt.MarkUsed();
    }

    // 애니메이션 이벤트로 호출(공격 클립 끝에 배치)
    public void EndAttack()
    {
        animator.applyRootMotion = false;
        EnableAgentLocomotion(true);
        state = State.Recover; // 간단 복귀. 필요 시 타이머로 Chase/Strafe로 전환
    }

    void EnableAgentLocomotion(bool enable)
    {
        agent.isStopped = !enable;
        agent.updatePosition = enable;
        agent.updateRotation = false; // 항상 수동 회전
    }

    void ManualRotateTowards(Vector3 toTarget)
    {
        Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, maxTurnSpeedDeg * Time.deltaTime);
    }

    bool HasLineOfSight()
    {
        if (!useLOS) return true;
        if (!eye || !player) return true; // 확실하지 않음: 눈 오브젝트 없으면 통과
        Vector3 toHead = (player.position + Vector3.up * 1.0f) - eye.position;
        float dist = toHead.magnitude;
        if (dist > losMaxDistance) return false;
        return !Physics.Raycast(eye.position, toHead.normalized, dist, losBlockMask, QueryTriggerInteraction.Ignore);
    }

    float FacingFactor(Vector3 targetPos, float requiredDot)
    {
        // requiredDot: -1(전방/후방 무관) ~ 1(완전 정면)
        Vector3 to = (targetPos - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, to);
        if (dot < requiredDot) return 0f;
        return Mathf.InverseLerp(requiredDot, 1f, dot); // 요구치보다 정면일수록 1에 가까움
    }

    Vector3 PredictTargetPosition(Vector3 pos, Vector3 vel, float leadTime)
    {
        // 평지/저속 전제에선 0으로 두어도 무방. 필요 시 플레이어 속도를 연결
        return pos + vel * leadTime;
    }

    void SetDestinationSafe(Vector3 pos)
    {
        if (!agent.isOnNavMesh) return;

        // 목적지 근처 샘플링(바닥 위 안전지점)
        if (NavMesh.SamplePosition(pos, out var hit, 1.5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(transform.position); // 실패 시 정지
    }

    void OnAnimatorMove()
    {
        if (!animator.applyRootMotion) return;

        // 루트모션으로 이동(키네마틱 Rigidbody)
        Vector3 nextPos = animator.rootPosition;
        Quaternion nextRot = animator.rootRotation;

        rb.MovePosition(nextPos);
        rb.MoveRotation(nextRot);

        // Agent와 동기화(떨림 방지)
        if (agent) agent.nextPosition = nextPos;
    }

    void OnDrawGizmosSelected()
    {
        if (player)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, farEnter);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(player.position, midEnter);
        }
    }
}

[System.Serializable]
public class AttackOption
{
    public string name;
    public string animatorTrigger = "AttackA";     // Animator 트리거 이름
    public float minRange = 0f;
    public float maxRange = 3f;
    public float cooldown = 3f;
    public float weight = 1f;
    public AnimationCurve distanceCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public bool requiresLOS = false;
    [Range(-1f, 1f)] public float requiredFacingDot = 0.0f; // 0.0 ~= 전방 90도 이내

    [HideInInspector] public float lastUsedTime = -999f;

    public bool IsInRange(float d) => d >= minRange && d <= maxRange;
    public float NormalizedRange(float d)
    {
        if (maxRange <= minRange) return 1f;
        return Mathf.Clamp01((d - minRange) / (maxRange - minRange));
    }
    public bool IsOffCooldown() => Time.time >= lastUsedTime + cooldown;
    public float CooldownFactor()
    {
        float t = Mathf.Clamp01((Time.time - lastUsedTime) / cooldown);
        return Mathf.Lerp(0.25f, 1f, t); // 직전 반복 억제
    }
    public void MarkUsed() => lastUsedTime = Time.time;
}