// 점프는 “코드 이동(수직 속도 + 중력 + 캡슐캐스트)”로 처리해서 실제 위치가 움직입니다. 애니메이션은 연출용 트리거.

using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class BossJumpAbility : MonoBehaviour
{
    [Header("Animator")]
    public string jumpTrigger = "Jump";   // 애니메이터 트리거 이름

    [Header("Jump Physics")]
    public float jumpForce = 5.0f;        // 초기 상승 속도
    public float gravity = 20.0f;         // 중력 가속도
    public float maxAirTime = 1.5f;       // 안전 차단 시간(초)

    [Header("Collision")]
    public LayerMask obstacleMask;        // 벽/지면 레이어
    public float skinWidth = 0.05f;       // 여유 거리

    [Header("Debug")]
    public bool logDebug = true;

    public bool IsJumping { get; private set; } = false;

    Animator animator;
    NavMeshAgent agent;
    Rigidbody rb;
    CapsuleCollider capsule;
    BossBrain brain;

    float verticalVelocity = 0f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        brain = GetComponent<BossBrain>();

        rb.isKinematic = true;
        rb.useGravity = false; // 수동 중력
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public bool TryJump()
    {
        if (IsJumping) return false;
        StartCoroutine(JumpRoutine());
        return true;
    }

    IEnumerator JumpRoutine()
    {
        IsJumping = true;

        // AI/Agent 잠시 중지
        if (brain) brain.BeginExternalAction();
        if (agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        // 연출 애니메이션
        animator.ResetTrigger("Idle");
        animator.SetTrigger(jumpTrigger);

        verticalVelocity = jumpForce;
        float t = 0f;

        if (logDebug) Debug.Log("[Jump] start");

        while (t < maxAirTime)
        {
            float dt = Time.deltaTime;
            t += dt;

            // 위/아래 충돌 검사 + 이동
            Vector3 p1, p2;
            GetCapsuleWorldPoints(out p1, out p2);

            if (verticalVelocity > 0f)
            {
                // 상승: 천장 체크
                if (Physics.CapsuleCast(p1, p2, capsule.radius - skinWidth, Vector3.up, out var hitUp, verticalVelocity * dt + skinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    float moveUp = Mathf.Max(0f, hitUp.distance - skinWidth);
                    rb.MovePosition(rb.position + Vector3.up * moveUp);
                    verticalVelocity = 0f; // 더 이상 상승 금지
                }
                else
                {
                    rb.MovePosition(rb.position + Vector3.up * (verticalVelocity * dt));
                }
            }
            else
            {
                // 하강: 바닥 체크
                if (Physics.CapsuleCast(p1, p2, capsule.radius - skinWidth, Vector3.down, out var hitDown, Mathf.Abs(verticalVelocity) * dt + skinWidth, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    float moveDown = Mathf.Max(0f, hitDown.distance - skinWidth);
                    rb.MovePosition(rb.position + Vector3.down * moveDown);
                    break; // 착지
                }
                else
                {
                    rb.MovePosition(rb.position + Vector3.up * (verticalVelocity * dt));
                }
            }

            // 중력 적용
            verticalVelocity -= gravity * dt;

            yield return null;
        }

        // 종료 복구
        if (!agent.enabled)
        {
            Vector3 pos = transform.position;
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                agent.Warp(pos);
            agent.enabled = true;
            agent.isStopped = false;
        }

        if (brain) brain.EndExternalAction();
        IsJumping = false;
        if (logDebug) Debug.Log("[Jump] end");
    }

    void GetCapsuleWorldPoints(out Vector3 p1, out Vector3 p2)
    {
        float height = Mathf.Max(capsule.height, capsule.radius * 2f);
        Vector3 center = transform.TransformPoint(capsule.center);
        float half = Mathf.Max(0f, (height * 0.5f) - capsule.radius);
        Vector3 up = transform.up;
        p1 = center + up * half;
        p2 = center - up * half;
    }
}