using UnityEngine;

public class AbilityVFX : MonoBehaviour
{
    [Header("Refs (optional)")]
    public Transform characterRoot;     // ºñ¿ì¸é this.transform
    public Animator animatorRef;        // ºñ¿ì¸é GetComponentInChildren

    void Awake()
    {
        if (characterRoot == null) characterRoot = transform;
        if (animatorRef == null) animatorRef = GetComponentInChildren<Animator>();

        if (healSpawn == null) healSpawn = FindBestAnchor();
        if (buffSpawn == null) buffSpawn = FindBestAnchor();
        if (ultSpawn == null) ultSpawn = characterRoot;
    }

    Transform FindBestAnchor()
    {
        if (animatorRef != null && animatorRef.isHuman)
        {
            var t = animatorRef.GetBoneTransform(HumanBodyBones.Chest);
            if (t != null) return t;
            t = animatorRef.GetBoneTransform(HumanBodyBones.Spine);
            if (t != null) return t;
        }
        return characterRoot;
    }

    // Heal
    [Header("Heal VFX")]
    public GameObject healPrefab;
    public Transform healSpawn;
    public bool healParentToPlayer = true;
    public Vector3 healLocalPosOffset;
    public Vector3 healLocalEulerOffset;
    public float healFallbackLife = 2f;

    [Header("Heal SFX")]
    public AudioClip healSfx;
    [Range(0f, 1f)] public float healSfxVolume = 1f;

    // Buff
    [Header("Buff VFX")]
    public GameObject buffPrefab;
    public Transform buffSpawn;
    public bool buffParentToPlayer = true;
    public Vector3 buffLocalPosOffset;
    public Vector3 buffLocalEulerOffset;
    public float buffFallbackLife = 2f;

    [Header("Buff SFX")]
    public AudioClip buffSfx;
    [Range(0f, 1f)] public float buffSfxVolume = 1f;

    // Ultimate
    [Header("Ultimate VFX")]
    public GameObject ultPrefab;
    public Transform ultSpawn;
    public bool ultParentToPlayer = false;
    public float ultForwardOffset = 1.0f;
    public Vector3 ultLocalPosOffset;
    public Vector3 ultLocalEulerOffset;
    public bool ultAlignToForward = true;
    public float ultFallbackLife = 2.5f;

    [Header("Ultimate SFX")]
    public AudioClip ultimateSfx;
    [Range(0f, 1f)] public float ultSfxVolume = 1f;

    public GameObject PlayHeal()
    {
        var go = SpawnOnce(healPrefab, healSpawn, healParentToPlayer, healLocalPosOffset, healLocalEulerOffset, healFallbackLife);
        if (healSfx != null)
            AudioManager.Instance?.PlaySFX3DAt(healSfx, (go != null ? go.transform.position : healSpawn.position), healSfxVolume);
        return go;
    }

    public GameObject PlayBuff()
    {
        var go = SpawnOnce(buffPrefab, buffSpawn, buffParentToPlayer, buffLocalPosOffset, buffLocalEulerOffset, buffFallbackLife);
        if (buffSfx != null)
            AudioManager.Instance?.PlaySFX3DAt(buffSfx, (go != null ? go.transform.position : buffSpawn.position), buffSfxVolume);
        return go;
    }

    // ´Ü¼ø ±Ã±Ø±â VFX
    public GameObject PlayUltimate()
    {
        if (ultPrefab == null || ultSpawn == null) return null;
        var go = Instantiate(ultPrefab);
        if (ultParentToPlayer) go.transform.SetParent(ultSpawn, false);

        var basePos = ultSpawn.position + ultSpawn.forward * ultForwardOffset;
        go.transform.position = basePos + (ultAlignToForward ? ultSpawn.TransformDirection(ultLocalPosOffset) : ultLocalPosOffset);

        var rot = ultAlignToForward ? Quaternion.LookRotation(ultSpawn.forward, Vector3.up) : ultSpawn.rotation;
        go.transform.rotation = rot * Quaternion.Euler(ultLocalEulerOffset);

        AutoDestroyByParticle(go, ultFallbackLife);
        if (ultimateSfx != null)
            AudioManager.Instance?.PlaySFX3DAt(ultimateSfx, go.transform.position, ultSfxVolume);
        return go;
    }

    // ±Ã±Ø±â È÷Æ®¹Ú½º ¹öÀü
    public GameObject PlayUltimateWithHitbox(Transform instigator, int damage, LayerMask targets)
    {
        var go = PlayUltimate();
        if (go == null) return null;

        var hb = go.GetComponentInChildren<UltimateHitbox>(true);
        if (hb != null)
        {
            hb.instigator = instigator;
            hb.damage = damage;
            hb.targetLayers = targets;
        }
        return go;
    }

    // Helpers
    GameObject SpawnOnce(GameObject prefab, Transform anchor, bool parentToAnchor, Vector3 localPosOffset, Vector3 localEulerOffset, float fallbackLife)
    {
        if (prefab == null || anchor == null) return null;

        var go = Instantiate(prefab);
        if (parentToAnchor) go.transform.SetParent(anchor, false);

        go.transform.position = anchor.TransformPoint(localPosOffset);
        go.transform.rotation = anchor.rotation * Quaternion.Euler(localEulerOffset);

        AutoDestroyByParticle(go, fallbackLife);
        return go;
    }

    void AutoDestroyByParticle(GameObject go, float fallbackLife)
    {
        float maxLife = 0f;
        var pss = go.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in pss)
        {
            var m = ps.main;
            float life = m.duration + m.startLifetimeMultiplier;
            if (life > maxLife) maxLife = life;
            ps.Play(true);
        }
        Destroy(go, Mathf.Max(fallbackLife, maxLife));
    }
}