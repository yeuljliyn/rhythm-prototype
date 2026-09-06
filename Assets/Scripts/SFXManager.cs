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

    AudioClip _click;      // 일반 버튼 클릭 — Assets/Resources/SFX/클릭.wav (없으면 440 Hz 합성음)
    AudioClip _navigate;   // 로비 좌우 이동 — Assets/Resources/SFX/이동.wav (없으면 660 Hz 합성음)
    AudioClip _back;       // 뒤로/닫기 (330 Hz 합성음 — 전용 파일 없음)
    AudioClip _confirm;    // 선택 확정 — Assets/Resources/SFX/선택.wav (없으면 550→880 Hz 합성음)

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

        // 전용 wav가 있으면 그걸, 없으면 기존 합성음으로 폴백. wav 원본이 매우 조용하게(피크
        // -18dBFS 수준) 뽑혀 있어서 그대로 쓰면 거의 안 들림 — 로드 후 코드에서 피크 정규화해
        // 합성음과 비슷한 레벨로 맞춤.
        _click    = Normalize(Resources.Load<AudioClip>("SFX/클릭")) ?? BuildTone(440f, 0.07f, 14f);
        _navigate = Normalize(Resources.Load<AudioClip>("SFX/이동")) ?? BuildTone(660f, 0.06f, 18f);
        _confirm  = Normalize(Resources.Load<AudioClip>("SFX/선택")) ?? BuildChord(new[] { 550f, 880f }, new[] { 0f, 0.05f }, 0.12f, 16f);
        _back     = BuildTone(330f, 0.09f, 10f);
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

    // 로드한 클립을 피크 targetPeak로 정규화한 새 클립으로 복제(원본 에셋은 안 건드림).
    // null이면 null 반환 → 위의 ?? 폴백으로 넘어감.
    static AudioClip Normalize(AudioClip src, float targetPeak = 0.9f)
    {
        if (src == null) return null;
        var data = new float[src.samples * src.channels];
        src.GetData(data, 0);
        float peak = 0f;
        foreach (var s in data) { float a = Mathf.Abs(s); if (a > peak) peak = a; }
        if (peak > 1e-4f)
        {
            float g = targetPeak / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
        }
        var clip = AudioClip.Create(src.name, src.samples, src.channels, src.frequency, false);
        clip.SetData(data, 0);
        return clip;
    }

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
