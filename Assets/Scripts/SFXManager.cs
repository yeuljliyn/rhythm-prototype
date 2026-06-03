using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 절차적으로 생성된 UI 효과음을 재생한다.
/// RuntimeInitialize로 자동 부트스트랩 — 별도 씬 설정 불필요.
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    AudioSource _src;

    AudioClip _click;      // 일반 버튼 클릭 (440 Hz, 짧고 선명)
    AudioClip _navigate;   // 로비 좌우 이동 (660 Hz, 더 가벼운 틱)
    AudioClip _back;       // 뒤로/닫기 (330 Hz, 약간 낮음)
    AudioClip _confirm;    // 선택 확정 (550 Hz → 880 Hz, 두 음)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[SFXManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<SFXManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _src             = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;

        _click    = BuildTone(440f, 0.07f, 14f);
        _navigate = BuildTone(660f, 0.06f, 18f);
        _back     = BuildTone(330f, 0.09f, 10f);
        _confirm  = BuildChord(new[] { 550f, 880f }, new[] { 0f, 0.05f }, 0.12f, 16f);
    }

    // ── Public API ────────────────────────────────────────────────────────

    public static void PlayClick()    => Play(Instance?._click,    0.45f);
    public static void PlayNavigate() => Play(Instance?._navigate, 0.40f);
    public static void PlayBack()     => Play(Instance?._back,     0.40f);
    public static void PlayConfirm()  => Play(Instance?._confirm,  0.50f);

    static void Play(AudioClip clip, float volumeScale)
    {
        if (Instance == null || clip == null) return;
        Instance._src.PlayOneShot(clip, volumeScale * SettingsManager.Volume);
    }

    // ── 사운드 생성 ───────────────────────────────────────────────────────

    static AudioClip BuildTone(float freq, float duration, float decay)
    {
        int sampleRate = 44100;
        int samples    = (int)(sampleRate * duration);
        var data       = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t        = (float)i / sampleRate;
            float envelope = Mathf.Exp(-decay * t);
            data[i]        = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.6f;
        }
        var clip = AudioClip.Create($"SFX_{freq}Hz", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 여러 음을 시간차(offsets)로 이어붙인 클립
    static AudioClip BuildChord(float[] freqs, float[] offsets, float duration, float decay)
    {
        int sampleRate = 44100;
        int samples    = (int)(sampleRate * duration);
        var data       = new float[samples];
        for (int n = 0; n < freqs.Length; n++)
        {
            int start = (int)(offsets[n] * sampleRate);
            for (int i = start; i < samples; i++)
            {
                float t        = (float)(i - start) / sampleRate;
                float envelope = Mathf.Exp(-decay * t);
                data[i]       += Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * envelope * 0.4f;
            }
        }
        var clip = AudioClip.Create("SFX_chord", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
