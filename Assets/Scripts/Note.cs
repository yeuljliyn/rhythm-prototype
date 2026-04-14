using UnityEngine;

public enum NoteType
{
    Normal,      // SPACE or Z
    ArrowLeft,   // ← / A
    ArrowDown,   // ↓ / S
    ArrowUp,     // ↑ / W
    ArrowRight,  // → / D
}

/// <summary>
/// Combat beat circle.
/// Normal notes: white ring — press SPACE/Z.
/// Special notes: colored ring with arrow — press the matching direction key.
/// </summary>
public class Note : MonoBehaviour
{
    public const float PerfectWindow = 0.08f;
    public const float GoodWindow    = 0.15f;

    public float    HitTime  { get; private set; }
    public bool     IsHit    { get; private set; }
    public NoteType Type     { get; private set; }

    Transform      _ring;
    SpriteRenderer _ringSR;
    float          _spawnTime;
    Color          _ringBase;

    const float MaxRingScale     = 3.5f;
    const float SpecialNoteScale = 0.90f;
    const float NormalNoteScale  = 0.75f;

    static readonly Color ColCircleNormal  = new Color(1.00f, 1.00f, 1.00f, 0.92f);
    static readonly Color ColRingPerfect   = new Color(1.00f, 0.92f, 0.15f, 1.00f);
    static readonly Color ColRingLate      = new Color(1.00f, 0.35f, 0.25f, 0.85f);

    // Direction ring colors (same palette as original lanes)
    static readonly Color[] TypeColors = {
        new Color(0.40f, 0.75f, 1.00f, 0.95f), // Normal   — steel blue
        new Color(0.30f, 0.60f, 1.00f, 0.95f), // Left     — blue
        new Color(0.30f, 1.00f, 0.45f, 0.95f), // Down     — green
        new Color(1.00f, 0.90f, 0.20f, 0.95f), // Up       — yellow
        new Color(1.00f, 0.35f, 0.35f, 0.95f), // Right    — red
    };

    // ── Initialization ────────────────────────────────────────────────
    public void Initialize(float hitTime, float spawnTime, NoteType type = NoteType.Normal)
    {
        HitTime    = hitTime;
        _spawnTime = spawnTime;
        Type       = type;

        bool special = type != NoteType.Normal;
        _ringBase    = TypeColors[(int)type];

        // Inner circle
        var circleSR         = gameObject.AddComponent<SpriteRenderer>();
        circleSR.sprite      = CircleSprites.GetCircle();
        circleSR.color       = ColCircleNormal;
        circleSR.sortingOrder = 5;
        transform.localScale = Vector3.one * (special ? SpecialNoteScale : NormalNoteScale);

        // Direction arrow indicator (special notes only)
        if (special)
            AddArrowIndicator(type);

        // Approach ring child
        var ringGO           = new GameObject("Ring");
        ringGO.transform.SetParent(transform, false);
        _ring                = ringGO.transform;
        _ring.localScale     = Vector3.one * MaxRingScale;

        _ringSR              = ringGO.AddComponent<SpriteRenderer>();
        _ringSR.sprite       = CircleSprites.GetRing();
        _ringSR.color        = _ringBase;
        _ringSR.sortingOrder = 4;

        NoteManager.Instance.Register(this);
    }

    void AddArrowIndicator(NoteType type)
    {
        float rotation = type == NoteType.ArrowLeft  ? 180f :
                         type == NoteType.ArrowDown  ? -90f :
                         type == NoteType.ArrowUp    ?  90f : 0f;

        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(transform, false);
        arrowGO.transform.localScale    = Vector3.one * 0.50f;
        arrowGO.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        var sr          = arrowGO.AddComponent<SpriteRenderer>();
        sr.sprite       = ArrowSprite.Get();
        sr.color        = TypeColors[(int)type];
        sr.sortingOrder = 6;
    }

    // ── Update ────────────────────────────────────────────────────────
    void Update()
    {
        if (IsHit) return;

        float songTime = RhythmManager.Instance.SongTime;
        float diff     = songTime - HitTime;

        // Ring shrinks MaxRingScale → 1.0 by hitTime, then 1.0 → 0.3 during GoodWindow
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

        // Ring tint feedback
        float absDiff = Mathf.Abs(diff);
        if (diff >= 0f)
            _ringSR.color = Color.Lerp(ColRingPerfect, ColRingLate, diff / GoodWindow);
        else if (absDiff <= PerfectWindow)
            _ringSR.color = Color.Lerp(_ringBase, ColRingPerfect, 1f - absDiff / PerfectWindow);
        else
            _ringSR.color = _ringBase;

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
            PlayerHealthManager.Instance?.LoseHeart();
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
