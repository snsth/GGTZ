using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GolemBossController : MonoBehaviour
{
    Animator anim;

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public void SetMove(bool isMoving)
    {
        anim.SetBool("IsWalking", isMoving);
    }

    public void PlayAttack01()
    {
        anim.SetTrigger("Attack01");
    }

    public void PlayHit()
    {
        anim.SetTrigger("Hit");
    }

    public void PlayDie()
    {
        anim.SetTrigger("Die");
    }
}