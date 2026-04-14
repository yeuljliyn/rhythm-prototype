using UnityEngine;

public class Note : MonoBehaviour
{
    // ── Judgement windows (seconds) ────────────────────────────────
    public const float PerfectWindow = 0.06f;
    public const float GoodWindow    = 0.12f;

    // ── Public state ───────────────────────────────────────────────
    public int   Lane    { get; private set; }
    public float HitTime { get; private set; }
    public bool  IsHit   { get; private set; }

    private float noteSpeed;

    // ── Init ───────────────────────────────────────────────────────
    public void Initialize(int lane, float hitTime, float speed)
    {
        Lane      = lane;
        HitTime   = hitTime;
        noteSpeed = speed;

        NoteManager.Instance.Register(this);
    }

    // ── Movement ───────────────────────────────────────────────────
    void Update()
    {
        transform.Translate(Vector3.left * noteSpeed * Time.deltaTime);

        if (!IsHit)
        {
            float diff = RhythmManager.Instance.SongTime - HitTime;
            if (diff > GoodWindow)
                AutoMiss();
        }
    }

    // ── Hit / Miss ─────────────────────────────────────────────────
    public void TryHit()
    {
        if (IsHit) return;

        float diff = Mathf.Abs(RhythmManager.Instance.SongTime - HitTime);

        JudgementType j;
        if      (diff <= PerfectWindow) j = JudgementType.Perfect;
        else if (diff <= GoodWindow)    j = JudgementType.Good;
        else    return;                 // outside window — ignore

        Resolve(j);
    }

    void AutoMiss()
    {
        Resolve(JudgementType.Miss);
    }

    void Resolve(JudgementType j)
    {
        IsHit = true;
        NoteManager.Instance.Unregister(this);
        GameManager.Instance.RegisterJudgement(j);

        if (j != JudgementType.Miss)
            SpawnHitFX();

        Destroy(gameObject);
    }

    // ── Hit VFX ────────────────────────────────────────────────────
    void SpawnHitFX()
    {
        var fx = new GameObject("HitFX");
        fx.transform.position = transform.position;
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite        = GetComponent<SpriteRenderer>().sprite;
        sr.color         = Color.white;
        sr.sortingOrder  = 10;
        fx.AddComponent<HitFX>();
    }

}
