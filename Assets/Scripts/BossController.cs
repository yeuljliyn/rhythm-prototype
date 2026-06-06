using UnityEngine;
using System.Collections;

public class BossController : MonoBehaviour
{
    static BossController _instance;
    public static BossController Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<BossController>(); return _instance; }
        private set { _instance = value; }
    }

    SpriteRenderer     _sr;
    Sprite[]           _dirSprites;   // 상(0) 하(1) 좌(2) 우(3)
    Sprite[]           _idleSprites;  // 둠칫둠칫 2프레임
    Vector3            _basePos;
    Coroutine          _shakeRoutine;
    Coroutine          _beatCycleCr;
    Coroutine          _idleCr;
    BossDirectionEffect _dirEffect;

    const float IdleBeat = 60f / 80f; // 80 BPM — 한 비트마다 프레임 전환

    float _refWorldHeight; // 방향 스프라이트 높이 기준 (스케일 보정용)

    static readonly Color ColHit = new Color(1.00f, 0.25f, 0.10f);

    void Awake() { Instance = this; }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Start()
    {
        _basePos   = transform.position;
        _dirEffect = GetComponent<BossDirectionEffect>();
        _sr = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = 3;

        _dirSprites  = Resources.LoadAll<Sprite>("물귀신_상하좌우");
        _idleSprites = Resources.LoadAll<Sprite>("광대물귀신_둠칫둠칫");
        _sr.color    = Color.white;

        transform.localScale = Vector3.one * 2.5f;

        // 방향 스프라이트 높이를 기준값으로 기록
        if (_dirSprites != null && _dirSprites.Length >= 4)
            _refWorldHeight = (_dirSprites[0].rect.height / _dirSprites[0].pixelsPerUnit) * 2.5f;

        StartIdleLoop(); // 인트로부터 둠칫둠칫 재생
    }

    public void StartBeatCycle()
    {
        // 플레이어 턴 — 보스는 둠칫둠칫 idle
        if (_beatCycleCr != null) { StopCoroutine(_beatCycleCr); _beatCycleCr = null; }
        StartIdleLoop();
    }

    public void StopBeatCycle()
    {
        // 보스 턴 시작 — idle 멈추고 ShowDirection 대기
        if (_beatCycleCr != null) { StopCoroutine(_beatCycleCr); _beatCycleCr = null; }
        StopIdleLoop();
        if (_dirSprites != null && _dirSprites.Length >= 4)
            SetSprite(_dirSprites[0]);
    }

    public void ShowDirection(ArrowDir dir)
    {
        StopIdleLoop();
        if (_beatCycleCr != null) { StopCoroutine(_beatCycleCr); _beatCycleCr = null; }
        if (_dirSprites == null || _dirSprites.Length < 4) return;
        int idx = dir == ArrowDir.Up ? 0 : dir == ArrowDir.Down ? 1 : dir == ArrowDir.Left ? 2 : 3;
        SetSprite(_dirSprites[idx]);
        _dirEffect?.PlayEffect(dir);
        BattleSFXController.Instance?.PlayBossDir(dir);
    }

    void StartIdleLoop()
    {
        if (_idleCr != null) return;
        if (_idleSprites == null || _idleSprites.Length == 0) return;
        _idleCr = StartCoroutine(IdleLoop());
    }

    void StopIdleLoop()
    {
        if (_idleCr != null) { StopCoroutine(_idleCr); _idleCr = null; }
    }

    IEnumerator IdleLoop()
    {
        int i = 0;
        while (true)
        {
            SetSprite(_idleSprites[i % _idleSprites.Length]);
            i++;
            yield return new WaitForSeconds(IdleBeat);
        }
    }

    // 스프라이트를 교체하면서 높이를 방향 스프라이트 기준으로 스케일 보정
    void SetSprite(Sprite s)
    {
        if (s == null) return;
        _sr.sprite = s;
        if (_refWorldHeight <= 0f) return;
        float spriteH = s.rect.height / s.pixelsPerUnit;
        float newScale = _refWorldHeight / spriteH;
        transform.localScale = new Vector3(newScale, newScale, 1f);
    }

    public void TakeDamage(JudgementType j)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(HitShake(j));
    }

    IEnumerator HitShake(JudgementType j)
    {
        float intensity = 0.15f;
        float duration  = 0.30f, elapsed = 0f;
        if (_sr != null) _sr.color = ColHit;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = _basePos + (Vector3)(Random.insideUnitCircle * intensity * (1f - t));
            if (_sr != null) _sr.color = Color.Lerp(ColHit, Color.white, t);
            yield return null;
        }
        transform.position = _basePos;
        if (_sr != null) _sr.color = Color.white;
    }
}
