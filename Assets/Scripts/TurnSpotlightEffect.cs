using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 보스/플레이어 턴 전환 시 해당 캐릭터 위에 스포트라이트 빛줄기 연출.
/// BattleDirector.Start()에서 AddComponent로 생성됨.
/// </summary>
public class TurnSpotlightEffect : MonoBehaviour
{
    static readonly Color ColBoss   = new Color(1.00f, 0.80f, 0.30f);
    static readonly Color ColPlayer = new Color(0.30f, 0.80f, 1.00f);

    const float FadeIn    = 0.30f;
    const float Hold      = 1.50f;
    const float FadeOut   = 0.80f;
    const float PeakAlpha = 0.70f;

    SpriteRenderer _cone;
    SpriteRenderer _glow;
    Coroutine      _cr;
    Texture2D      _coneTex;
    Texture2D      _glowTex;

    PlayerController _playerCtrl;

    void Awake()
    {
        BuildSprites();
        BattleTurnUI.OnBossTurnBegin   += HandleBoss;
        BattleTurnUI.OnPlayerTurnBegin += HandlePlayer;
    }

    void Start()
    {
        _playerCtrl = FindObjectOfType<PlayerController>();
    }

    void OnDestroy()
    {
        BattleTurnUI.OnBossTurnBegin   -= HandleBoss;
        BattleTurnUI.OnPlayerTurnBegin -= HandlePlayer;
        if (_coneTex != null) Destroy(_coneTex);
        if (_glowTex != null) Destroy(_glowTex);
    }

    void HandleBoss()
    {
        var t = BossController.Instance?.transform;
        if (t != null) Show(t.position, ColBoss);
    }

    void HandlePlayer(List<DirectionInputEntry> _)
    {
        if (_playerCtrl != null) Show(_playerCtrl.transform.position, ColPlayer);
    }

    void Show(Vector3 worldPos, Color col)
    {
        if (_cr != null) StopCoroutine(_cr);
        _cr = StartCoroutine(SpotlightRoutine(worldPos, col));
    }

    IEnumerator SpotlightRoutine(Vector3 pos, Color col)
    {
        const float ConeH = 5.0f, ConeW = 3.0f;
        const float GlowW = 3.5f, GlowH = 0.6f;

        _cone.transform.position   = new Vector3(pos.x, pos.y + 0.5f + ConeH * 0.5f, 0f);
        _cone.transform.localScale = new Vector3(ConeW, ConeH, 1f);
        _glow.transform.position   = new Vector3(pos.x, pos.y - 0.7f, 0f);
        _glow.transform.localScale = new Vector3(GlowW, GlowH, 1f);

        for (float t = 0f; t < FadeIn; t += Time.deltaTime)
        {
            SetAlpha(col, t / FadeIn);
            yield return null;
        }
        SetAlpha(col, 1f);

        yield return new WaitForSeconds(Hold);

        for (float t = 0f; t < FadeOut; t += Time.deltaTime)
        {
            SetAlpha(col, 1f - t / FadeOut);
            yield return null;
        }
        SetAlpha(col, 0f);
        _cr = null;
    }

    void SetAlpha(Color col, float t)
    {
        float a = t * PeakAlpha;
        _cone.color = new Color(col.r, col.g, col.b, a);
        _glow.color = new Color(col.r, col.g, col.b, a * 0.55f);
    }

    // ── 스프라이트 빌드 ──────────────────────────────────────────────────────

    void BuildSprites()
    {
        _coneTex = BuildConeTex(128, 256);
        _glowTex = BuildGlowTex(128);
        _cone    = MakeSR("SpotlightCone", _coneTex, 1);
        _glow    = MakeSR("SpotlightGlow", _glowTex, 1);
        SetAlpha(Color.white, 0f);
    }

    SpriteRenderer MakeSR(string goName, Texture2D tex, int order)
    {
        var go    = new GameObject(goName);
        go.transform.SetParent(transform);
        var sr    = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                  new Vector2(0.5f, 0.5f), tex.width);
        sr.sortingOrder = order;
        sr.color        = Color.clear;
        return sr;
    }

    // 아래가 넓고 위로 좁아지는 빛줄기 콘 텍스처
    static Texture2D BuildConeTex(int w, int h)
    {
        var tex  = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float iw = 1f / (w - 1), ih = 1f / (h - 1);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            float nx = x * iw;
            float ny = y * ih;  // 0=하단(캐릭터), 1=상단(하늘)

            float halfW = (1f - ny) * 0.44f;
            if (halfW < 0.001f) { tex.SetPixel(x, y, Color.clear); continue; }

            float dx = Mathf.Abs(nx - 0.5f);
            if (dx > halfW) { tex.SetPixel(x, y, Color.clear); continue; }

            float edgeT   = 1f - dx / halfW;
            float heightF = Mathf.Pow(1f - ny, 0.4f);
            float alpha   = Mathf.Pow(edgeT, 1.5f) * heightF;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        return tex;
    }

    // 타원형 발밑 글로우 텍스처
    static Texture2D BuildGlowTex(int size)
    {
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float iv = 1f / (size - 1);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float nx = x * iv, ny = y * iv;
            float dx   = (nx - 0.5f) * 2f;
            float dy   = (ny - 0.5f) * 5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a    = Mathf.Clamp01(1f - dist);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * 0.9f));
        }
        tex.Apply();
        return tex;
    }
}
