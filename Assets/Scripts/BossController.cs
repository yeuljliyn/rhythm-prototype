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

    SpriteRenderer _sr;
    Sprite[]       _dirSprites;   // 상(0) 하(1) 좌(2) 우(3)
    Vector3        _basePos;
    Coroutine      _shakeRoutine;

    static readonly Color ColHit = new Color(1.00f, 0.25f, 0.10f);

    void Awake() { Instance = this; }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Start()
    {
        _basePos = transform.position;
        _sr = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = 3;

        _dirSprites = Resources.LoadAll<Sprite>("잭두_상하좌우");
        if (_dirSprites != null && _dirSprites.Length >= 4)
        {
            _sr.sprite = _dirSprites[0];
            _sr.color  = Color.white;
        }

        transform.localScale = Vector3.one * 2.5f;
        StartCoroutine(BeatCycleDirections());
    }

    // Called by BattleDirector to show a specific direction during boss turn
    public void ShowDirection(ArrowDir dir)
    {
        if (_dirSprites == null || _dirSprites.Length < 4) return;
        int idx = dir == ArrowDir.Up ? 0 : dir == ArrowDir.Down ? 1 : dir == ArrowDir.Left ? 2 : 3;
        if (_sr != null && _dirSprites[idx] != null) _sr.sprite = _dirSprites[idx];
    }

    IEnumerator BeatCycleDirections()
    {
        int i = 0;
        while (true)
        {
            float beat = RhythmManager.Instance != null
                ? RhythmManager.Instance.BeatInterval
                : 0.469f;  // 128 BPM fallback
            if (_dirSprites != null && _dirSprites.Length >= 4 && _sr != null)
                _sr.sprite = _dirSprites[i % 4];
            i++;
            yield return new WaitForSeconds(beat);
        }
    }

    public void TakeDamage(JudgementType j)
    {
        if (!gameObject.activeInHierarchy) return;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(HitShake(j));
    }

    IEnumerator HitShake(JudgementType j)
    {
        float intensity = j == JudgementType.Perfect ? 0.18f : 0.10f;
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
