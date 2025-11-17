using UnityEngine;

public class SlashController : MonoBehaviour
{
    public GameObject slashObject;      // Slash1
    public ParticleSystem slashEffect;  // 파티클 (있다면)
    public float duration = 0.15f;      // 애니메이션 길이와 동일하게

    private Animator anim;

    void Start()
    {
        anim = slashObject.GetComponent<Animator>();
        slashObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartCoroutine(PlaySlash());
        }
    }

    System.Collections.IEnumerator PlaySlash()
    {
        slashObject.SetActive(true);
        anim.Play("SlashSwing", -1, 0f);

        if (slashEffect != null)
            slashEffect.Play();

        yield return new WaitForSeconds(duration);

        slashObject.SetActive(false);
    }
}
