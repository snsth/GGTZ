using UnityEngine;
using UnityEngine.AI;

public abstract class BossAbilityBase : MonoBehaviour, IBossAbility
{
    public abstract VoiceAction Action { get; }
    public abstract float Cooldown { get; }
    public bool IsRunning { get; protected set; }

    protected BossBrain brain;
    protected NavMeshAgent agent;
    protected Animator animator;
    protected Rigidbody rb;

    protected virtual void Awake()
    {
        brain = GetComponent<BossBrain>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
    }

    public abstract bool TryStart();
    protected void DisableAgent()
    {
        if (agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }
        if (brain) brain.BeginExternalAction();
    }
    protected void EnableAgentAndResume()
    {
        if (!agent.enabled)
        {
            var pos = transform.position;
            if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                agent.Warp(hit.position);
            else agent.Warp(pos);
            agent.enabled = true;
            agent.isStopped = false;
        }
        if (brain) brain.EndExternalAction();
    }
}