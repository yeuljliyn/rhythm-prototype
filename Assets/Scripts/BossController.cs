using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Boss entity on the right side of the screen.
/// Sprite frames assigned via Inspector (set up by editor setup script).
/// </summary>
public class BossController : MonoBehaviour
{
    static BossController _instance;
    public static BossController Instance
    {
        get
        {
            if (_instance == null) _instance = FindObjectOfType<BossController>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("Stats")]
    public float maxHP = 400f;
    public float HP    { get; private set; }

    [Header("Sprite Animation")]
    public Sprite[] attackFrames;
    [Range(1, 30)] public float animFPS = 10f;

    SpriteRenderer _sr;
    Vector3        _basePos;
    Coroutine      _shakeRoutine;
    Coroutine      _animRoutine;

    static readonly Color ColHit    = new Color(1.00f, 0.25f, 0.10f);
    static readonly Color ColAttack = new Color(1.00f, 0.80f, 0.20f);

    void Awake()
    {
        Instance = this;
        HP = maxHP;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        _basePos = transform.position;

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = 3;

        if (attackFrames != null && attackFrames.Length > 0)
        {
            _sr.sprite = attackFrames[0];
            _sr.color  = Color.white;
            _animRoutine = StartCoroutine(AnimateLoop());
        }
        else
        {
            // Fallback procedural sprite
            _sr.sprite = BuildFallbackSprite();
            _sr.color  = new Color(0.60f, 0.05f, 0.75f);
        }

        transform.localScale = Vector3.one * 2.5f;
    }

    // ── Combat ────────────────────────────────────────────────────────

    public void TakeDamage(JudgementType j)
    {
        if (HP <= 0f || !gameObject.activeInHierarchy) return;
        float dmg = j == JudgementType.Perfect ? 20f : 12f;
        HP = Mathf.Clamp(HP - dmg, 1f, maxHP);

        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(HitShake(j));
    }

    public void AttackPlayer()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(AttackFlash());

        FindObjectOfType<PlayerController>()?.FlashDamage();
    }

    // ── Coroutines ────────────────────────────────────────────────────

    IEnumerator AnimateLoop()
    {
        float interval = 1f / Mathf.Max(1f, animFPS);
        int   i        = 0;
        while (true)
        {
            if (_sr != null && attackFrames[i] != null)
                _sr.sprite = attackFrames[i];
            i = (i + 1) % attackFrames.Length;
            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator HitShake(JudgementType j)
    {
        float intensity = j == JudgementType.Perfect ? 0.18f : 0.10f;
        float duration  = 0.30f, elapsed = 0f;
        Color startCol  = _sr != null ? _sr.color : Color.white;

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

    IEnumerator AttackFlash()
    {
        float duration = 0.20f, elapsed = 0f;
        Vector3 lunge  = new Vector3(-0.40f, 0f, 0f);  // lunge left (toward player)

        if (_sr != null) _sr.color = ColAttack;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = _basePos + lunge * Mathf.Sin(t * Mathf.PI);
            if (_sr != null) _sr.color = Color.Lerp(ColAttack, Color.white, t);
            yield return null;
        }
        transform.position = _basePos;
        if (_sr != null) _sr.color = Color.white;
    }

    // ── Fallback sprite ───────────────────────────────────────────────

    static Sprite BuildFallbackSprite()
    {
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[S * S];
        var center = new Vector2((S - 1) / 2f, (S - 1) / 2f);
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center) / (S / 2f);
            pixels[y * S + x] = d < 0.9f ? new Color(0.6f, 0.05f, 0.75f, 1f - d * 0.5f) : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
