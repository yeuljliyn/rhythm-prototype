using UnityEngine;

public class Note : MonoBehaviour
{
    public const float PerfectWindow = 0.06f;
    public const float GoodWindow    = 0.12f;

    public int   Lane    { get; private set; }
    public float HitTime { get; private set; }
    public bool  IsHit   { get; private set; }

    float   noteSpeed;
    Vector3 moveDir;

    public void Initialize(int lane, float hitTime, float speed, Vector3 direction)
    {
        Lane      = lane;
        HitTime   = hitTime;
        noteSpeed = speed;
        moveDir   = direction;

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            sr.sprite = ArrowSprite.Get();

        NoteManager.Instance.Register(this);
    }

    void Update()
    {
        transform.Translate(moveDir * noteSpeed * Time.deltaTime, Space.World);

        if (!IsHit)
        {
            float diff = RhythmManager.Instance.SongTime - HitTime;
            if (diff > GoodWindow)
                AutoMiss();
        }
    }

    public void TryHit()
    {
        if (IsHit) return;

        float diff = Mathf.Abs(RhythmManager.Instance.SongTime - HitTime);

        JudgementType j;
        if      (diff <= PerfectWindow) j = JudgementType.Perfect;
        else if (diff <= GoodWindow)    j = JudgementType.Good;
        else    return;

        Resolve(j);
    }

    void AutoMiss() => Resolve(JudgementType.Miss);

    void Resolve(JudgementType j)
    {
        IsHit = true;
        NoteManager.Instance.Unregister(this);
        GameManager.Instance.RegisterJudgement(j);

        if (j != JudgementType.Miss)
            SpawnHitFX();

        Destroy(gameObject);
    }

    void SpawnHitFX()
    {
        var fx = new GameObject("HitFX");
        fx.transform.position = RhythmManager.ReceptorPos[Lane];
        fx.transform.rotation = transform.rotation;   // ← 노트 방향 그대로 복사
        var sr = fx.AddComponent<SpriteRenderer>();
        sr.sprite       = GetComponent<SpriteRenderer>()?.sprite;
        sr.color        = Color.white;
        sr.sortingOrder = 10;
        fx.AddComponent<HitFX>();
    }
}
