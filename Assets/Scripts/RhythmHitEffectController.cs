using UnityEngine;
using System.Collections;

[System.Serializable]
public class HitJudgementConfig
{
    public Color streamColor        = Color.white;
    public int   streamCount        = 22;
    public float streamSpeed        = 8f;
    public float streamSize         = 0.2f;

    public Color sparksColor        = Color.white;
    public int   sparksCount        = 14;
    public float sparksSpeed        = 3f;
    public float sparksSize         = 0.12f;

    public Color ringColor          = Color.white;
    public int   ringCount          = 18;
    public float ringSpeed          = 6f;
    public float ringSize           = 0.35f;
    public bool  showRing           = true;

    public Color  charFlashColor    = Color.white;
    public float  charFlashDuration = 0.14f;

    public float popScale    = 1.4f;
    public float popDuration = 0.13f;
}

public class RhythmHitEffectController : MonoBehaviour
{
    public static RhythmHitEffectController Instance { get; private set; }

    [Header("References")]
    [SerializeField] ParticleSystem _psStream;
    [SerializeField] ParticleSystem _psSparks;
    [SerializeField] ParticleSystem _psRing;
    [SerializeField] Transform      _playerTransform;
    [SerializeField] SpriteRenderer _playerRenderer;

    [Header("Perfect — 금색 + 청록 영력")]
    [SerializeField] HitJudgementConfig _perfect = new HitJudgementConfig
    {
        streamColor       = new Color(1.00f, 0.85f, 0.10f),
        streamCount       = 30, streamSpeed = 10f, streamSize = 0.22f,
        sparksColor       = new Color(0.15f, 1.00f, 0.90f),
        sparksCount       = 20, sparksSpeed = 4f,  sparksSize = 0.14f,
        ringColor         = new Color(1.00f, 0.92f, 0.35f),
        ringCount         = 22, ringSpeed   = 8f,  ringSize   = 0.42f, showRing = true,
        charFlashColor    = new Color(1.00f, 0.92f, 0.40f), charFlashDuration = 0.20f,
        popScale = 1.55f, popDuration = 0.14f
    };

    [Header("Good — 보라 영기")]
    [SerializeField] HitJudgementConfig _good = new HitJudgementConfig
    {
        streamColor       = new Color(0.65f, 0.25f, 1.00f),
        streamCount       = 18, streamSpeed = 7f,  streamSize = 0.18f,
        sparksColor       = new Color(0.80f, 0.50f, 1.00f),
        sparksCount       = 12, sparksSpeed = 2.8f, sparksSize = 0.11f,
        ringColor         = new Color(0.55f, 0.20f, 0.90f),
        ringCount         = 16, ringSpeed   = 6f,  ringSize   = 0.30f, showRing = true,
        charFlashColor    = new Color(0.72f, 0.32f, 1.00f), charFlashDuration = 0.14f,
        popScale = 1.35f, popDuration = 0.12f
    };

    [Header("Miss — 크림슨 파편")]
    [SerializeField] HitJudgementConfig _miss = new HitJudgementConfig
    {
        streamColor       = new Color(0.90f, 0.10f, 0.15f),
        streamCount       = 10, streamSpeed = 4f,  streamSize = 0.14f,
        sparksColor       = new Color(0.60f, 0.05f, 0.10f),
        sparksCount       = 7,  sparksSpeed = 1.8f, sparksSize = 0.09f,
        ringColor         = new Color(0.50f, 0.05f, 0.08f),
        ringCount         = 0,  ringSpeed   = 3f,  ringSize   = 0.20f, showRing = false,
        charFlashColor    = new Color(1.00f, 0.12f, 0.12f), charFlashDuration = 0.24f,
        popScale = 1.0f, popDuration = 0.1f
    };

    // ArrowDir: Left=0, Right=1, Up=2, Down=3
    static readonly float[] DirAngles = { 90f, -90f, 0f, 180f };

    Coroutine _squashCr;
    Coroutine _flashCr;
    Vector3   _baseScale;

    void Awake() { Instance = this; }

    void Start()
    {
        if (_playerTransform != null)
            _baseScale = _playerTransform.localScale;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void PlayHitEffect(ArrowDir dir, JudgementType j)
    {
        var cfg = j == JudgementType.Hit ? _perfect : _miss;
        EmitStream(dir, cfg);
        EmitSparks(cfg);
        if (cfg.showRing && cfg.ringCount > 0) EmitRing(cfg);
        FlashCharacter(cfg);
        if (_playerTransform != null && cfg.popScale > 1.01f)
            PlaySquash(cfg);
    }

    // ── 파티클 발사 ──────────────────────────────────────────────────────────

    void EmitStream(ArrowDir dir, HitJudgementConfig cfg)
    {
        if (_psStream == null) return;
        if (_playerTransform != null) _psStream.transform.position = _playerTransform.position;
        _psStream.transform.rotation = Quaternion.Euler(0f, 0f, DirAngles[(int)dir]);
        ApplyMain(_psStream, cfg.streamColor, cfg.streamSpeed, cfg.streamSize);
        _psStream.Emit(cfg.streamCount);
    }

    void EmitSparks(HitJudgementConfig cfg)
    {
        if (_psSparks == null) return;
        if (_playerTransform != null) _psSparks.transform.position = _playerTransform.position;
        _psSparks.transform.rotation = Quaternion.identity;
        ApplyMain(_psSparks, cfg.sparksColor, cfg.sparksSpeed, cfg.sparksSize);
        _psSparks.Emit(cfg.sparksCount);
    }

    void EmitRing(HitJudgementConfig cfg)
    {
        if (_psRing == null) return;
        if (_playerTransform != null) _psRing.transform.position = _playerTransform.position;
        _psRing.transform.rotation = Quaternion.identity;
        ApplyMain(_psRing, cfg.ringColor, cfg.ringSpeed, cfg.ringSize);
        _psRing.Emit(cfg.ringCount);
    }

    void ApplyMain(ParticleSystem ps, Color color, float speed, float size)
    {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
        main.startSize  = new ParticleSystem.MinMaxCurve(size  * 0.7f, size  * 1.3f);
    }

    // ── 캐릭터 플래시 ────────────────────────────────────────────────────────

    void FlashCharacter(HitJudgementConfig cfg)
    {
        if (_playerRenderer == null) return;
        if (_flashCr != null) StopCoroutine(_flashCr);
        _flashCr = StartCoroutine(FlashRoutine(cfg.charFlashColor, cfg.charFlashDuration));
    }

    IEnumerator FlashRoutine(Color color, float duration)
    {
        _playerRenderer.color = color;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _playerRenderer.color = Color.Lerp(color, Color.white, t / duration);
            yield return null;
        }
        _playerRenderer.color = Color.white;
        _flashCr = null;
    }

    // ── 스쿼시 팝 ────────────────────────────────────────────────────────────

    void PlaySquash(HitJudgementConfig cfg)
    {
        if (_squashCr != null) StopCoroutine(_squashCr);
        _squashCr = StartCoroutine(SquashRoutine(cfg.popScale, cfg.popDuration));
    }

    IEnumerator SquashRoutine(float targetScale, float duration)
    {
        float half = duration * 0.5f, t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            _playerTransform.localScale = _baseScale * Mathf.Lerp(1f, targetScale, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            _playerTransform.localScale = _baseScale * Mathf.Lerp(targetScale, 1f, t / half);
            yield return null;
        }
        _playerTransform.localScale = _baseScale;
        _squashCr = null;
    }
}
