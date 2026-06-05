using UnityEngine;
using System.Collections;

[System.Serializable]
public class HitJudgementConfig
{
    public Color particleColor = Color.white;
    public int   burstCount    = 20;
    public float startSpeed    = 6f;
    public float startSize     = 0.25f;
    public float popScale      = 1.4f;
    public float popDuration   = 0.13f;
}

public class RhythmHitEffectController : MonoBehaviour
{
    public static RhythmHitEffectController Instance { get; private set; }

    [Header("References")]
    [SerializeField] ParticleSystem _particles;
    [SerializeField] Transform      _playerTransform;

    [Header("Perfect")]
    [SerializeField] HitJudgementConfig _perfect = new HitJudgementConfig
    {
        particleColor = new Color(1f, 0.92f, 0.15f),
        burstCount    = 28,
        startSpeed    = 8f,
        startSize     = 0.30f,
        popScale      = 1.5f,
        popDuration   = 0.14f
    };

    [Header("Good")]
    [SerializeField] HitJudgementConfig _good = new HitJudgementConfig
    {
        particleColor = new Color(0.3f, 1f, 0.5f),
        burstCount    = 15,
        startSpeed    = 5f,
        startSize     = 0.20f,
        popScale      = 1.3f,
        popDuration   = 0.12f
    };

    [Header("Miss")]
    [SerializeField] HitJudgementConfig _miss = new HitJudgementConfig
    {
        particleColor = new Color(0.5f, 0.5f, 0.55f),
        burstCount    = 8,
        startSpeed    = 3f,
        startSize     = 0.15f,
        popScale      = 1.0f,
        popDuration   = 0.10f
    };

    // ArrowDir: Left=0, Right=1, Up=2, Down=3  →  Z 회전각
    static readonly float[] DirAngles = { 90f, -90f, 0f, 180f };

    Coroutine _squashCr;
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
        HitJudgementConfig cfg = j == JudgementType.Perfect ? _perfect
                               : j == JudgementType.Good    ? _good
                                                            : _miss;
        EmitParticles(dir, cfg);

        if (_playerTransform != null && cfg.popScale > 1.01f)
            PlaySquash(cfg);
    }

    void EmitParticles(ArrowDir dir, HitJudgementConfig cfg)
    {
        if (_particles == null) return;

        if (_playerTransform != null)
            _particles.transform.position = _playerTransform.position;

        _particles.transform.rotation = Quaternion.Euler(0f, 0f, DirAngles[(int)dir]);

        var main = _particles.main;
        main.startColor = new ParticleSystem.MinMaxGradient(cfg.particleColor);
        main.startSpeed = new ParticleSystem.MinMaxCurve(cfg.startSpeed);
        main.startSize  = new ParticleSystem.MinMaxCurve(cfg.startSize);

        _particles.Emit(cfg.burstCount);
    }

    void PlaySquash(HitJudgementConfig cfg)
    {
        if (_squashCr != null) StopCoroutine(_squashCr);
        _squashCr = StartCoroutine(SquashRoutine(cfg.popScale, cfg.popDuration));
    }

    IEnumerator SquashRoutine(float targetScale, float duration)
    {
        float half = duration * 0.5f;
        float t = 0f;
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
