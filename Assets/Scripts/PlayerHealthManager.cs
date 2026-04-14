using UnityEngine;
using System.Collections;

/// <summary>
/// Player HP shown as 5 hearts in world space, centered below the player.
/// Miss → lose one heart (turn gray and shrink slightly).
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    public static PlayerHealthManager Instance { get; private set; }

    const int   MaxHearts   = 5;
    const float HeartSpacing = 0.80f;

    public int  Hearts { get; private set; } = MaxHearts;

    SpriteRenderer[] _heartSR    = new SpriteRenderer[MaxHearts];
    Coroutine[]      _pulseJobs  = new Coroutine[MaxHearts];

    static readonly Color ColFull  = new Color(1.00f, 0.20f, 0.25f, 1.00f);
    static readonly Color ColEmpty = new Color(0.25f, 0.08f, 0.08f, 0.50f);

    // ── Lifecycle ─────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        BuildHearts();
        GameManager.Instance.OnJudgement += OnJudgement;
    }

    // ── Public API ────────────────────────────────────────────────────
    public void LoseHeart()
    {
        if (Hearts <= 0) return;
        Hearts--;
        int lostIdx = Hearts;   // index of the heart just lost

        if (_pulseJobs[lostIdx] != null) StopCoroutine(_pulseJobs[lostIdx]);
        _pulseJobs[lostIdx] = StartCoroutine(LoseHeartAnim(lostIdx));
    }

    // ── Private ───────────────────────────────────────────────────────
    void OnJudgement(JudgementType type)
    {
        // Note.Resolve calls LoseHeart directly; this is kept for potential future use.
    }

    void BuildHearts()
    {
        var sprite     = BuildHeartSprite();
        float totalW   = (MaxHearts - 1) * HeartSpacing;
        float startX   = -totalW / 2f;

        for (int i = 0; i < MaxHearts; i++)
        {
            var go = new GameObject("Heart_" + i);
            go.transform.SetParent(transform, false);
            go.transform.position    = new Vector3(startX + i * HeartSpacing, -4.2f, 0f);
            go.transform.localScale  = Vector3.one * 0.50f;

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color        = ColFull;
            sr.sortingOrder = 5;

            _heartSR[i] = sr;
        }
    }

    IEnumerator LoseHeartAnim(int idx)
    {
        if (_heartSR[idx] == null) yield break;

        // Quick shake then go dim
        Transform t    = _heartSR[idx].transform;
        Vector3 origin = t.position;
        float duration = 0.25f, elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float f = elapsed / duration;
            t.position    = origin + (Vector3)(Random.insideUnitCircle * 0.06f * (1f - f));
            _heartSR[idx].color = Color.Lerp(Color.white, ColEmpty, f);
            yield return null;
        }
        t.position = origin;
        _heartSR[idx].color          = ColEmpty;
        _heartSR[idx].transform.localScale = Vector3.one * 0.38f;
    }

    // ── Heart sprite (mathematical heart curve) ───────────────────────
    static Sprite BuildHeartSprite()
    {
        const int S = 48;
        var pixels  = new Color[S * S];

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            // Normalise to [-1.2, 1.2]
            float nx = (x - S / 2f + 0.5f) / (S / 2.4f);
            float ny = (y - S / 2f + 0.5f) / (S / 2.4f);
            ny = -ny + 0.15f;   // flip Y and nudge upward for aesthetic centering

            // Heart implicit equation: (x²+y²-1)³ - x²y³ ≤ 0
            float v = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - nx * nx * Mathf.Pow(ny, 3f);
            if (v <= 0f)
                pixels[y * S + x] = Color.white;
        }

        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
    }
}
