using UnityEngine;

// Kept for ChapterData / NoteEntry compatibility
public enum NoteType { Normal, ArrowLeft, ArrowDown, ArrowUp, ArrowRight }

public class Note : MonoBehaviour
{
    public const float PerfectWindow = 0.08f;
    public const float GoodWindow    = 0.15f;

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

    // 어프로치 링: 이 배율에서 시작해서 1.0으로 수축
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

        // 어프로치 링 (자식 오브젝트)
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

        // 노트 색상 변화
        if (Mathf.Abs(diff) <= GoodWindow)
            _sr.color = diff >= 0f
                ? Color.Lerp(ColNear, ColLate, diff / GoodWindow)
                : Color.Lerp(ColDefault, ColNear, 1f - Mathf.Abs(diff) / GoodWindow);
        else if (diff < -GoodWindow)
            _sr.color = ColDefault;

        // 어프로치 링 수축: 남은 시간 비율로 크기 계산
        if (_ringsr != null)
        {
            float timeRemaining = HitTime - RhythmManager.Instance.SongTime;
            float progress      = 1f - Mathf.Clamp01(timeRemaining / _spawnAheadTime);
            float ringScale     = Mathf.Lerp(RingStartScale, 1f, progress);
            _ringsr.transform.localScale = Vector3.one * ringScale;

            // 히트존 근접 시 링 색상도 변화
            float alpha = Mathf.Lerp(0.7f, 0.2f, progress);
            _ringsr.color = Mathf.Abs(diff) <= GoodWindow
                ? new Color(1f, 0.85f, 0f, alpha)
                : new Color(1f, 1f, 1f, alpha);
        }

        if (diff > GoodWindow)
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
        JudgementType j;
        if      (diff <= PerfectWindow) j = JudgementType.Perfect;
        else if (diff <= GoodWindow)    j = JudgementType.Good;
        else                            return;
        Resolve(j);
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
        sr.color        = j == JudgementType.Perfect
            ? new Color(1f, 0.92f, 0.10f)
            : new Color(0.25f, 1f, 0.45f);
        sr.sortingOrder = 10;
        fx.AddComponent<HitFX>();
    }
}
