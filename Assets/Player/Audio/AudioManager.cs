using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("-------------Audio Source-----------")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource; // 2D SFX용(버튼/UI 등)

    [Header("-------------Audio Clip-----------")]
    public AudioClip background;
    public AudioClip death;
    public AudioClip check;
    public AudioClip walk;
    public AudioClip enemy;

    [Header("Mixer(Optional)")]
    public AudioMixerGroup sfxMixerGroup;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (background != null)
        {
            musicSource.clip = background;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    public void PlaySFX2D(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        SFXSource.PlayOneShot(clip, volume);
    }

    // 새로 추가: 3D 위치에서 원샷 재생
    public void PlaySFX3DAt(AudioClip clip, Vector3 pos, float volume = 1f,
                            float pitchMin = 0.98f, float pitchMax = 1.02f,
                            float minDistance = 1.5f, float maxDistance = 18f)
    {
        if (clip == null) return;

        var go = new GameObject($"SFX_{clip.name}");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        if (sfxMixerGroup != null) src.outputAudioMixerGroup = sfxMixerGroup;

        src.clip = clip;
        src.volume = volume;

        src.spatialBlend = 1f; // 3D
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;

        src.pitch = Random.Range(pitchMin, pitchMax);
        src.playOnAwake = false;

        src.Play();
        Destroy(go, clip.length / Mathf.Max(0.01f, src.pitch) + 0.1f);
    }
}