using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player_Attack : MonoBehaviour
{
    public Slider HP;   // Slider 형식의 HP 변수 생성 
    public float damageCooldown = 0.5f; // 데미지 쿨타임 (초 단위)
    public int damage;

    Rigidbody rigid;
    BoxCollider boxCollider;
    bool canTakeDamage = true; // 데미지를 받을 수 있는 상태인지 확인

    void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
    }

    void OnTriggerEnter(Collider other)
    {
        // 충돌한 객체가 보스이고, 플레이어가 보스에 닿았는지 확인
        if (other.tag == "Boss" && canTakeDamage)
        {
            HP.value -= damage;
            StartCoroutine(TakeDamageCooldown());
        }
    }

    IEnumerator TakeDamageCooldown()
    {
        canTakeDamage = false; // 데미지를 받을 수 없는 상태로 변경
        yield return new WaitForSeconds(damageCooldown); // 쿨타임 대기
        canTakeDamage = true; // 다시 데미지를 받을 수 있는 상태로 변경
    }
}
