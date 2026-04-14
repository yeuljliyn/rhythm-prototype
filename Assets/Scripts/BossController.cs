using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Boss entity at the top-center of the screen.
/// Takes damage on successful note hits; attacks the player on misses.
/// Builds its own sprite and HP bar at runtime.
/// </summary>
public class BossController : MonoBehaviour
{
    public static BossController Instance { get; private set; }

    [Header("Stats")]
    public float maxHP = 100f;
    public float HP    { get; private set; }

    SpriteRenderer _sr;
    Vector3        _basePos;
    Coroutine      _shakeRoutine;
    Image          _hpFill;

    static readonly Color ColBody    = new Color(0.60f, 0.05f, 0.75f);
    static readonly Color ColBodyDim = new Color(0.28f, 0.00f, 0.40f);
    static readonly Color ColHit     = new Color(1.00f, 0.25f, 0.10f);
    static readonly Color ColAttack  = new Color(1.00f, 0.80f, 0.20f);

    // ── Lifecycle ─────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        HP = maxHP;
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
        float dmg = j == JudgementType.Perfect ? 15f : 8f;
        HP = Mathf.Clamp(HP - dmg, 0f, maxHP);
        RefreshHPBar();

        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(HitShake(j));
    }

    public void AttackPlayer()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(AttackFlash());

        // Notify player to flash red
        var player = FindObjectOfType<PlayerController>();
        player?.FlashDamage();
    }

    // ── Coroutines ────────────────────────────────────────────────────
    IEnumerator HitShake(JudgementType j)
    {
        float intensity = j == JudgementType.Perfect ? 0.18f : 0.10f;
        float duration  = 0.30f;
        float elapsed   = 0f;

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
        // Boss lunges forward slightly
        float duration = 0.20f;
        float elapsed  = 0f;
        Vector3 lunge  = new Vector3(0f, -0.35f, 0f);

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
        bgRect.anchoredPosition = new Vector2(0f, -28f);
        bgRect.sizeDelta        = new Vector2(360f, 8f);
        bgGO.AddComponent<Image>().color = new Color(0.08f, 0.02f, 0.12f, 0.9f);

        // Label
        // (skipped for simplicity — gauge color indicates boss HP)

        // Fill
        var fillGO   = new GameObject("BossHPFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        _hpFill            = fillGO.AddComponent<Image>();
        _hpFill.type       = Image.Type.Filled;
        _hpFill.fillMethod = Image.FillMethod.Horizontal;
        _hpFill.fillOrigin = 0;
        _hpFill.color      = new Color(0.85f, 0.15f, 0.90f);

        RefreshHPBar();
    }

    void RefreshHPBar()
    {
        if (_hpFill == null) return;
        _hpFill.fillAmount = HP / maxHP;

        // Color shifts: purple → red as HP drops
        float t = HP / maxHP;
        _hpFill.color = Color.Lerp(new Color(1f, 0.1f, 0.1f), new Color(0.85f, 0.15f, 0.9f), t);
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

            // Outer diamond silhouette
            bool inDiamond = (adx + ady) <= 0.92f;

            // Two hollow "eye" circles
            bool eye1 = Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.28f, 0.18f)) < 0.20f;
            bool eye2 = Vector2.Distance(new Vector2(dx, dy), new Vector2( 0.28f, 0.18f)) < 0.20f;

            // Fang notch at bottom
            bool fang = ady < 0.15f && adx < 0.10f && dy < -0.40f;

            if (inDiamond && !eye1 && !eye2)
            {
                float edge = Mathf.InverseLerp(0.92f, 0.70f, adx + ady);
                Color body = Color.Lerp(ColBodyDim, ColBody, edge);

                if (fang) body = new Color(1f, 0.9f, 0.85f);

                pixels[y * S + x] = body;
            }
            else if (eye1 || eye2)
            {
                // Glowing eyes
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
