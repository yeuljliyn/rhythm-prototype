using UnityEngine;

// Kept for ChapterData / NoteEntry compatibility
public enum NoteType { Normal, ArrowLeft, ArrowDown, ArrowUp, ArrowRight }

public class Note : MonoBehaviour
{
    public const float HitWindow = 0.15f;

    public float HitTime { get; private set; }
    public bool  IsHit   { get; private set; }

    float          _speed;
    float          _hitZoneX;
    float          _spawnAheadTime;
    SpriteRenderer _sr;
    SpriteRenderer _ringsr;

    static readonly Color ColDefault = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    static readonly Color ColNear    = new Color(1.00f, 0.85f, 0.00f, 1.00f);
    static readonly Color ColLate    = new Color(1.00f, 0.25f, 0.10f, 1.00f);

    // Approach ring: starts at this scale and shrinks to 1.0 at hit time
    const float RingStartScale = 3.0f;

    public void Initialize(float hitTime, float spawnX, float hitZoneX, float noteY, float spawnAheadTime)
    {
        HitTime         = hitTime;
        _hitZoneX       = hitZoneX;
        _spawnAheadTime = Mathf.Max(0.01f, spawnAheadTime);
        _speed          = (spawnX - hitZoneX) / _spawnAheadTime;

        transform.position   = new Vector3(spawnX, noteY, 0f);
        transform.localScale = Vector3.one * 0.75f;

        _sr              = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = CircleSprites.GetCircle();
        _sr.color        = ColDefault;
        _sr.sortingOrder = 5;

        // Approach ring (child object)
        var ringGO = new GameObject("ApproachRing");
        ringGO.transform.SetParent(transform, false);
        ringGO.transform.localPosition = Vector3.zero;
        ringGO.transform.localScale    = Vector3.one * RingStartScale;
        _ringsr              = ringGO.AddComponent<SpriteRenderer>();
        _ringsr.sprite       = CircleSprites.GetRing();
        _ringsr.color        = new Color(1f, 1f, 1f, 0.7f);
        _ringsr.sortingOrder = 4;

        NoteManager.Instance?.Register(this);
    }

    void Update()
    {
        if (IsHit) return;
        if (_sr == null || RhythmManager.Instance == null) return;

        transform.position += Vector3.left * _speed * Time.deltaTime;

        float diff = RhythmManager.Instance.SongTime - HitTime;

        // Note color based on timing
        if (Mathf.Abs(diff) <= HitWindow)
            _sr.color = diff >= 0f
                ? Color.Lerp(ColNear, ColLate, diff / HitWindow)
                : Color.Lerp(ColDefault, ColNear, 1f - Mathf.Abs(diff) / HitWindow);
        else if (diff < -HitWindow)
            _sr.color = ColDefault;

        // Approach ring shrinks based on remaining time to hit
        if (_ringsr != null)
        {
            float timeRemaining = HitTime - RhythmManager.Instance.SongTime;
            float progress      = 1f - Mathf.Clamp01(timeRemaining / _spawnAheadTime);
            float ringScale     = Mathf.Lerp(RingStartScale, 1f, progress);
            _ringsr.transform.localScale = Vector3.one * ringScale;

            float alpha = Mathf.Lerp(0.7f, 0.2f, progress);
            _ringsr.color = Mathf.Abs(diff) <= HitWindow
                ? new Color(1f, 0.85f, 0f, alpha)
                : new Color(1f, 1f, 1f, alpha);
        }

        if (diff > HitWindow)
            AutoMiss();
    }

    public void TryHit()
    {
        if (IsHit || RhythmManager.Instance == null) return;

        // judgementOffsetMs applied here only — single application point
        double rawSongTime     = RhythmManager.Instance.SongTimeD;
        double adjustedInputTime = CalibrationManager.Instance != null
            ? CalibrationManager.Instance.GetAdjustedInputTimeS(rawSongTime)
            : rawSongTime;

        float diff = Mathf.Abs((float)adjustedInputTime - HitTime);
        if (diff > HitWindow) return;
        Resolve(JudgementType.Hit);
    }

    void AutoMiss() => Resolve(JudgementType.Miss);

    void Resolve(JudgementType j)
    {
        IsHit = true;
        NoteManager.Instance.Unregister(this);
        GameManager.Instance.RegisterJudgement(j);

        if (j != JudgementType.Miss)
            SpawnHitFX(j);

        BossController.Instance?.TakeDamage(j);
        Destroy(gameObject);
    }

    void SpawnHitFX(JudgementType j)
    {
        var fx = new GameObject("HitFX");
        fx.transform.position = new Vector3(_hitZoneX, transform.position.y, 0f);
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite       = CircleSprites.GetCircle();
        sr.color        = new Color(1f, 0.92f, 0.10f);
        sr.sortingOrder = 10;
        fx.AddComponent<HitFX>();
    }
}
