using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Boss entity at the top-center of the screen.
/// HP: 200.  Perfect hit = -12 HP.  Good hit = -7 HP.
/// Takes damage on successful note hits; attacks the player on misses.
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
    public float maxHP = 200f;
    public float HP    { get; private set; }

    SpriteRenderer _sr;
    Vector3        _basePos;
    Coroutine      _shakeRoutine;
    Image          _hpFill;
    Image          _hpBg;

    static readonly Color ColBody    = new Color(0.60f, 0.05f, 0.75f);
    static readonly Color ColBodyDim = new Color(0.28f, 0.00f, 0.40f);
    static readonly Color ColHit     = new Color(1.00f, 0.25f, 0.10f);
    static readonly Color ColAttack  = new Color(1.00f, 0.80f, 0.20f);

    // ── Lifecycle ─────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
        maxHP = 400f;
        HP    = maxHP;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        _basePos = transform.position;

        _sr              = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = BuildBossSprite();
        _sr.color        = Color.white;
        _sr.sortingOrder = 3;
        transform.localScale = Vector3.one * 1.8f;

        BuildHPBar();
    }

    // ── Combat ────────────────────────────────────────────────────────
    public void TakeDamage(JudgementType j)
    {
        if (HP <= 0f) return;
        float dmg = j == JudgementType.Perfect ? 20f : 12f;
        HP = Mathf.Clamp(HP - dmg, 0f, maxHP);
        RefreshHPBar();

        if (HP <= 0f)
        {
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(DeathAnim());
            return;
        }

        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(HitShake(j));
    }

    public void AttackPlayer()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(AttackFlash());

        var player = FindObjectOfType<PlayerController>();
        player?.FlashDamage();
    }

    // ── Coroutines ────────────────────────────────────────────────────
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

    IEnumerator AttackFlash()
    {
        float duration = 0.20f, elapsed = 0f;
        Vector3 lunge  = new Vector3(0f, -0.40f, 0f);

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

    IEnumerator DeathAnim()
    {
        float duration = 0.8f, elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (_sr != null) _sr.color = Color.Lerp(Color.white, new Color(0.3f, 0.3f, 0.3f, 0f), t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }
        gameObject.SetActive(false);
        if (_hpBg != null) _hpBg.gameObject.SetActive(false);
    }

    // ── HP Bar ────────────────────────────────────────────────────────
    void BuildHPBar()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) return;

        var uiRoot = canvasGO.transform.Find("UIRoot");
        var parent = uiRoot != null ? uiRoot : canvasGO.transform;

        // Background
        var bgGO   = new GameObject("BossHPBackground");
        bgGO.transform.SetParent(parent, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0.5f, 1f);
        bgRect.anchorMax        = new Vector2(0.5f, 1f);
        bgRect.pivot            = new Vector2(0.5f, 1f);
        bgRect.anchoredPosition = new Vector2(0f, -20f);
        bgRect.sizeDelta        = new Vector2(400f, 16f);
        _hpBg = bgGO.AddComponent<Image>();
        _hpBg.color             = new Color(0.18f, 0.18f, 0.18f, 1f);

        // Fill — anchored to left edge, width shrinks as HP drops
        var fillGO   = new GameObject("BossHPFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin        = new Vector2(0f, 0f);
        fillRect.anchorMax        = new Vector2(0f, 1f);
        fillRect.pivot            = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta        = new Vector2(400f, 0f);   // width = full, height from anchors
        _hpFill                   = fillGO.AddComponent<Image>();
        _hpFill.color             = new Color(0.85f, 0.15f, 0.90f);

        RefreshHPBar();
    }

    void RefreshHPBar()
    {
        if (_hpFill == null) return;
        float t = HP / maxHP;
        // Directly resize the fill bar width — no fillAmount tricks
        var rt = _hpFill.rectTransform;
        rt.sizeDelta = new Vector2(400f * t, rt.sizeDelta.y);
        _hpFill.color = Color.Lerp(new Color(1f, 0.15f, 0.10f), new Color(0.85f, 0.15f, 0.90f), t);
    }

    // ── Boss Sprite ───────────────────────────────────────────────────
    static Sprite BuildBossSprite()
    {
        const int S = 64;
        var pixels  = new Color[S * S];
        var center  = new Vector2((S - 1) / 2f, (S - 1) / 2f);

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx  = (x - center.x) / (S / 2f);
            float dy  = (y - center.y) / (S / 2f);
            float adx = Mathf.Abs(dx);
            float ady = Mathf.Abs(dy);

            bool inDiamond = (adx + ady) <= 0.92f;
            bool eye1      = Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.28f, 0.18f)) < 0.20f;
            bool eye2      = Vector2.Distance(new Vector2(dx, dy), new Vector2( 0.28f, 0.18f)) < 0.20f;

            if (inDiamond && !eye1 && !eye2)
            {
                float edge = Mathf.InverseLerp(0.92f, 0.70f, adx + ady);
                pixels[y * S + x] = Color.Lerp(ColBodyDim, ColBody, edge);
            }
            else if (eye1 || eye2)
            {
                float eyeDist = eye1
                    ? Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.28f, 0.18f)) / 0.20f
                    : Vector2.Distance(new Vector2(dx, dy), new Vector2( 0.28f, 0.18f)) / 0.20f;
                float glow = 1f - eyeDist;
                pixels[y * S + x] = new Color(1f, 0.85f * glow, 0f, glow);
            }
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
