using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
    public enum Type { Melee };
    public Type type;
    public float rate;
    public BoxCollider AttackArea;
    private bool isSwinging = false;
    private void Start()
    {
        int weaponLayer = LayerMask.NameToLayer("Boss"); // Weapon과 Ground 레이어 번호 가져오기
        int groundLayer = LayerMask.NameToLayer("Ground");
        Physics.IgnoreLayerCollision(weaponLayer, groundLayer, true); // 무기 콜라이더와 Ground 레이어 간 충돌 무시
    }
    public bool IsSwinging()
    {
        return isSwinging; //TPS player controller와 연동시키기 위하여 씀
    }
    public void Use()
    {
        if (type == Type.Melee && !isSwinging)
        {
            StartCoroutine(Swing());
        }
    }

    IEnumerator Swing()
    {
        isSwinging = true; // 공격 중 상태로 설정
        yield return new WaitForSeconds(0.1f); // 공격 시작 전 대기 시간
        AttackArea.enabled = true; // 공격 시작 시 콜라이더 활성화
        yield return new WaitForSeconds(rate); // rate 변수를 사용하여 공격 지속 시간 대기
        AttackArea.enabled = false; // 공격 종료 시 콜라이더 비활성화
        isSwinging = false; // 공격 종료 상태로 설정
    }
}
