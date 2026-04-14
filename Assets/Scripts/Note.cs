using UnityEngine;

/// <summary>
/// Combat beat circle.
/// An outer approach ring shrinks toward the inner circle.
/// The player presses the hit key when the ring overlaps the inner circle.
/// Spawns near the boss; a hit damages the boss, a miss lets the boss attack.
/// </summary>
public class Note : MonoBehaviour
{
    public const float PerfectWindow = 0.08f;
    public const float GoodWindow    = 0.15f;

    public float HitTime  { get; private set; }
    public bool  IsHit    { get; private set; }

    Transform      _ring;
    SpriteRenderer _ringSR;
    SpriteRenderer _circleSR;
    float          _spawnTime;

    const float MaxRingScale = 3.2f;

    static readonly Color ColCircle      = new Color(1.00f, 1.00f, 1.00f, 0.92f);
    static readonly Color ColRingNormal  = new Color(0.40f, 0.75f, 1.00f, 0.95f);
    static readonly Color ColRingPerfect = new Color(1.00f, 0.92f, 0.15f, 1.00f);
    static readonly Color ColRingLate    = new Color(1.00f, 0.35f, 0.25f, 0.85f);

    // ── Initialization ────────────────────────────────────────────────
    public void Initialize(float hitTime, float spawnTime)
    {
        HitTime    = hitTime;
        _spawnTime = spawnTime;

        // Inner filled circle
        _circleSR              = gameObject.AddComponent<SpriteRenderer>();
        _circleSR.sprite       = CircleSprites.GetCircle();
        _circleSR.color        = ColCircle;
        _circleSR.sortingOrder = 5;
        transform.localScale   = Vector3.one * 0.55f;

        // Approach ring child
        var ringGO             = new GameObject("Ring");
        ringGO.transform.SetParent(transform, false);
        _ring                  = ringGO.transform;
        _ring.localScale       = Vector3.one * MaxRingScale;

        _ringSR                = ringGO.AddComponent<SpriteRenderer>();
        _ringSR.sprite         = CircleSprites.GetRing();
        _ringSR.color          = ColRingNormal;
        _ringSR.sortingOrder   = 4;

        NoteManager.Instance.Register(this);
    }

    // ── Update ────────────────────────────────────────────────────────
    void Update()
    {
        if (IsHit) return;

        float songTime = RhythmManager.Instance.SongTime;
        float diff     = songTime - HitTime;

        // Ring scale: MaxRingScale → 1.0 until hitTime, then 1.0 → 0.3 in GoodWindow
        float scale;
        if (diff < 0f)
        {
            float t = Mathf.Clamp01((songTime - _spawnTime) / (HitTime - _spawnTime));
            scale   = Mathf.Lerp(MaxRingScale, 1f, t);
        }
        else
        {
            float t = Mathf.Clamp01(diff / GoodWindow);
            scale   = Mathf.Lerp(1f, 0.3f, t);
        }
        _ring.localScale = Vector3.one * scale;

        // Tint ring based on timing
        float absDiff = Mathf.Abs(diff);
        if (diff >= 0f)
            _ringSR.color = Color.Lerp(ColRingPerfect, ColRingLate, diff / GoodWindow);
        else if (absDiff <= PerfectWindow)
            _ringSR.color = Color.Lerp(ColRingNormal, ColRingPerfect, 1f - absDiff / PerfectWindow);
        else
            _ringSR.color = ColRingNormal;

        // Auto miss after window closes
        if (diff > GoodWindow)
            AutoMiss();
    }

    // ── Hit ───────────────────────────────────────────────────────────
    public void TryHit()
    {
        if (IsHit) return;

        float diff = Mathf.Abs(RhythmManager.Instance.SongTime - HitTime);
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
        {
            SpawnHitFX(j);
            BossController.Instance?.TakeDamage(j);
        }
        else
        {
            BossController.Instance?.AttackPlayer();
        }

        Destroy(gameObject);
    }

    void SpawnHitFX(JudgementType j)
    {
        var fx          = new GameObject("HitFX");
        fx.transform.position = transform.position;
        var sr          = fx.AddComponent<SpriteRenderer>();
        sr.sprite       = CircleSprites.GetCircle();
        sr.color        = j == JudgementType.Perfect
            ? new Color(1f, 0.92f, 0.1f)
            : new Color(0.25f, 1f, 0.45f);
        sr.sortingOrder = 10;
        fx.AddComponent<HitFX>();
    }
}
