using UnityEngine;
using UnityEngine.UI;

public class PlayerAbilityUI : MonoBehaviour
{
    [System.Serializable]
    public class IconPair
    {
        public Image baseIcon;  // 항상 보이는 아이콘(색은 인스펙터 설정 유지)
        public Image cdMask;    // Filled Radial360, 검정 반투명
    }

    [Header("Refs")]
    public PlayerHealth playerHealth;

    [Header("Icons")]
    public IconPair guard;
    public IconPair heal;
    public IconPair buff;
    public IconPair ultimate;

    [Header("Mask Appearance")]
    public Color coolingMaskColor = new Color(0f, 0f, 0f, 0.55f);

    // 내부 상태
    float curHp, maxHp;
    float guardCdRemain, guardCdTotal;
    float healCdRemain, healCdTotal;
    float buffCdRemain, buffCdTotal;
    float ultCdRemain, ultCdTotal;

    bool isGuarding = false;
    bool isBuffActive = false;

    void Awake()
    {
        if (playerHealth != null)
        {
            maxHp = playerHealth.maxHp;
            curHp = playerHealth.currentHp;
            playerHealth.onHpChanged += OnHpChanged;
        }

        SetupPair(guard);
        SetupPair(heal);
        SetupPair(buff);
        SetupPair(ultimate);
    }

    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.onHpChanged -= OnHpChanged;
    }

    void SetupPair(IconPair p)
    {
        if (p.baseIcon != null) p.baseIcon.enabled = true;

        if (p.cdMask != null)
        {
            p.cdMask.enabled = true;
            p.cdMask.type = Image.Type.Filled;
            p.cdMask.fillMethod = Image.FillMethod.Radial360;
            p.cdMask.fillOrigin = 2;     // Top
            p.cdMask.fillClockwise = true;
            p.cdMask.fillAmount = 0f;    // 기본: 마스크 안 보임
            p.cdMask.color = coolingMaskColor;
            p.cdMask.raycastTarget = false;
        }
    }

    void OnHpChanged(float current, float max)
    {
        curHp = current; maxHp = max;
        // 베이스 색은 건드리지 않음. 힐 가능 여부는 쿨다운/버튼에서 통제.
    }

    void Update()
    {
        Tick(ref guardCdRemain);
        Tick(ref healCdRemain);
        Tick(ref buffCdRemain);
        Tick(ref ultCdRemain);

        UpdateMask(guard.cdMask, guardCdRemain, guardCdTotal);
        UpdateMask(heal.cdMask, healCdRemain, healCdTotal);
        UpdateMask(buff.cdMask, buffCdRemain, buffCdTotal);
        UpdateMask(ultimate.cdMask, ultCdRemain, ultCdTotal);
    }

    void Tick(ref float remain)
    {
        if (remain > 0f) remain = Mathf.Max(0f, remain - Time.deltaTime);
    }

    void UpdateMask(Image mask, float remain, float total)
    {
        if (mask == null) return;
        mask.fillAmount = (total > 0f) ? remain / total : 0f; // 1 → 0으로 줄어드는 링
    }

    // 외부에서 호출하는 API (색은 변경하지 않음)
    public void SetGuardActive(bool active) { isGuarding = active; }
    public void StartGuardCooldown(float seconds) { guardCdTotal = seconds; guardCdRemain = seconds; }

    public void StartHealCooldown(float seconds) { healCdTotal = seconds; healCdRemain = seconds; }

    public void SetBuffActive(bool active) { isBuffActive = active; }
    public void StartBuffCooldown(float seconds) { buffCdTotal = seconds; buffCdRemain = seconds; }

    public void StartUltimateCooldown(float seconds) { ultCdTotal = seconds; ultCdRemain = seconds; }
}