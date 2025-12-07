// 추격 / 스트레이프 이동, 회전, 목적지 세팅, Agent 제어

using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class BossLocomotion : MonoBehaviour
{
    [Header("Nav / Movement")]
    public float maxTurnSpeedDeg = 360f;

    [Header("Distance Gates")]
    public float farEnter = 8f;
    public float midEnter = 4f;
    public float hysteresis = 0.5f;

    [Header("Strafe")]
    public float desiredMidRadius = 5f;
    public float strafeSwitchInterval = 3f;

    private NavMeshAgent agent;
    private Animator animator;
    private Rigidbody rb;

    private float strafeDir = 1f;
    private float strafeTimer = 0f;

    public float AgentSpeed => agent ? agent.velocity.magnitude : 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        if (rb) rb.isKinematic = true;
        if (agent)
        {
            agent.updateRotation = false;
            agent.stoppingDistance = Mathf.Max(0.0f, agent.stoppingDistance);
        }
    }

    public void EnableAgentLocomotion(bool enable)
    {
        if (!agent) return;
        agent.isStopped = !enable;
        agent.updatePosition = enable;
        agent.updateRotation = false;
    }

    public void TickChase(Transform player)
    {
        if (!agent || !player) return;

        Vector3 toTarget = player.position - transform.position;
        Vector3 chasePos = PredictTargetPosition(player.position, Vector3.zero, 0.2f);
        SetDestinationSafe(chasePos);
        ManualRotateTowards(toTarget);
    }

    public void TickStrafe(Transform player)
    {
        if (!agent || !player) return;

        strafeTimer -= Time.deltaTime;
        if (strafeTimer <= 0f)
        {
            strafeDir = -strafeDir;
            strafeTimer = strafeSwitchInterval;
        }

        Vector3 center = player.position;
        Vector3 dirFromCenter = (transform.position - center);
        Vector3 dir = dirFromCenter.sqrMagnitude > 0.001f ? dirFromCenter.normalized : (-player.forward);

        Vector3 tangent = Vector3.Cross(Vector3.up, dir).normalized * strafeDir;
        Vector3 targetOnRing = center + dir * desiredMidRadius + tangent * 1.0f;

        SetDestinationSafe(targetOnRing);
        ManualRotateTowards(center - transform.position);
    }

    public void SyncAgentNextPosition()
    {
        if (agent) agent.nextPosition = transform.position;
    }

    public void ManualRotateTowards(Vector3 toTarget)
    {
        Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(flat.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxTurnSpeedDeg * Time.deltaTime);
    }

    Vector3 PredictTargetPosition(Vector3 pos, Vector3 vel, float leadTime)
    {
        return pos + vel * leadTime;
    }

    void SetDestinationSafe(Vector3 pos)
    {
        if (!agent || !agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(pos, out var hit, 1.5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(transform.position);
    }

    void OnDrawGizmosSelected()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.transform.position, farEnter);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(player.transform.position, midEnter);
        }
    }
}
