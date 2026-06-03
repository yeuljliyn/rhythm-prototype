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
    SpriteRenderer _sr;

    static readonly Color ColDefault = new Color(1.00f, 1.00f, 1.00f, 1.00f);  // 흰색
    static readonly Color ColNear    = new Color(1.00f, 0.85f, 0.00f, 1.00f);  // 노란색 (히트 근접)
    static readonly Color ColLate    = new Color(1.00f, 0.25f, 0.10f, 1.00f);  // 빨간색 (늦음)

    public void Initialize(float hitTime, float spawnX, float hitZoneX, float noteY, float spawnAheadTime)
    {
        HitTime   = hitTime;
        _hitZoneX = hitZoneX;
        _speed    = (spawnX - hitZoneX) / Mathf.Max(0.01f, spawnAheadTime);

        transform.position   = new Vector3(spawnX, noteY, 0f);
        transform.localScale = Vector3.one * 0.75f;

        _sr              = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = CircleSprites.GetCircle();
        _sr.color        = ColDefault;
        _sr.sortingOrder = 5;

        NoteManager.Instance?.Register(this);
    }

    void Update()
    {
        if (IsHit) return;
        if (_sr == null || RhythmManager.Instance == null) return;

        transform.position += Vector3.left * _speed * Time.deltaTime;

        float diff = RhythmManager.Instance.SongTime - HitTime;

        if (Mathf.Abs(diff) <= GoodWindow)
            _sr.color = diff >= 0f
                ? Color.Lerp(ColNear, ColLate, diff / GoodWindow)
                : Color.Lerp(ColDefault, ColNear, 1f - Mathf.Abs(diff) / GoodWindow);
        else if (diff < -GoodWindow)
            _sr.color = ColDefault;

        if (diff > GoodWindow)
            AutoMiss();
    }

    public void TryHit()
    {
        if (IsHit || RhythmManager.Instance == null) return;
        float offset = SettingsManager.InputOffset;
        float diff = Mathf.Abs(RhythmManager.Instance.SongTime + offset - HitTime);
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
