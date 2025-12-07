//  설명: 공격 중 Animator의 루트 모션을 실제 이동/회전에 반영. NavMeshAgent 위치 동기화

using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class BossAnimatorBridge : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private NavMeshAgent agent;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();

        if (rb) rb.isKinematic = true; // 루트모션 이동은 키네마틱으로
    }

    void OnAnimatorMove()
    {
        if (!animator.applyRootMotion) return;

        Vector3 nextPos = animator.rootPosition;
        Quaternion nextRot = animator.rootRotation;

        rb.MovePosition(nextPos);
        rb.MoveRotation(nextRot);

        if (agent) agent.nextPosition = nextPos;
    }
}