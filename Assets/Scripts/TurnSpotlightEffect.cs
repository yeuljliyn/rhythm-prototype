using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 보스/플레이어 턴 동안 해당 캐릭터 위에 스포트라이트 유지.
/// 턴 전환 시 크로스페이드로 스위치됨.
/// BattleDirector.Start()에서 AddComponent로 생성됨.
/// </summary>
public class TurnSpotlightEffect : MonoBehaviour
{
    class Spot
    {
        public Transform      root;
        public SpriteRenderer cone;
        public SpriteRenderer glow;
        public Light2D        light;
        public Color          col;
        public float          currentT;
        public Coroutine      cr;
    }

    // 플레이모드에서 조정한 warm cream 색상 (#FFEEC2)
    static readonly Color ColSpot = new Color(1.000f, 0.933f, 0.761f);

    const float CrossFade       = 0.40f;
    const float PeakAlpha       = 0.72f;
    const float AmbientDim      = 0.35f;
    const float PeakLightIntens = 1.00f;
    const float ConeH           = 5.5f;
    const float ConeW           = 5.0f;
    const float GlowW           = 4.5f;
    const float GlowH           = 0.7f;
    const float LightInner      = 2.00f;
    const float LightOuter      = 4.93f;
    // 플레이모드에서 조정한 root Y 오프셋
    const float RootYOffset     = -3.1f;

    Spot             _bossSpot;
    Spot             _playerSpot;
    Light2D          _ambientLight;
    PlayerController _playerCtrl;
    Texture2D        _coneTex;
    Texture2D        _glowTex;

    void Awake()
    {
        _coneTex    = BuildConeTex(128, 256);
        _glowTex    = BuildGlowTex(128);
        _bossSpot   = CreateSpot("BossSpot");
        _playerSpot = CreateSpot("PlayerSpot");

        // 전체 씬 어둠 기반 - 없으면 Light2D 있는 곳만 렌더링됨
        var ambGO = new GameObject("SpotlightAmbient");
        ambGO.transform.SetParent(transform);
        _ambientLight           = ambGO.AddComponent<Light2D>();
        _ambientLight.lightType = Light2D.LightType.Global;
        _ambientLight.color     = Color.white;
        _ambientLight.intensity = AmbientDim;

        BattleTurnUI.OnBossTurnBegin   += HandleBoss;
        BattleTurnUI.OnPlayerTurnBegin += HandlePlayer;
    }

    void Start()
    {
        _playerCtrl = FindObjectOfType<PlayerController>();
        PositionSpot(_bossSpot,   BossController.Instance?.transform);
        PositionSpot(_playerSpot, _playerCtrl?.transform);
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
        PositionSpot(_bossSpot, BossController.Instance?.transform);
        FadeTo(_bossSpot,   1f, CrossFade);
        FadeTo(_playerSpot, 0f, CrossFade);
    }

    void HandlePlayer(List<DirectionInputEntry> _)
    {
        PositionSpot(_playerSpot, _playerCtrl?.transform);
        FadeTo(_playerSpot, 1f, CrossFade);
        FadeTo(_bossSpot,   0f, CrossFade);
    }

    void PositionSpot(Spot spot, Transform anchor)
    {
        if (spot == null || anchor == null) return;
        float cx = anchor.position.x, cy = anchor.position.y;

        // root를 캐릭터 위치 + 조정된 오프셋으로 배치
        spot.root.position = new Vector3(cx, cy + RootYOffset, 0f);

        // children은 root 기준 local 좌표
        spot.cone.transform.localPosition  = new Vector3(0f, 0.5f + ConeH * 0.5f, 0f);
        spot.glow.transform.localPosition  = new Vector3(0f, -0.7f, 0f);
        if (spot.light != null)
            spot.light.transform.localPosition = new Vector3(0f, 2.5f, 0f);
    }

    void FadeTo(Spot spot, float target, float duration)
    {
        if (spot.cr != null) StopCoroutine(spot.cr);
        spot.cr = StartCoroutine(FadeRoutine(spot, target, duration));
    }

    IEnumerator FadeRoutine(Spot spot, float target, float duration)
    {
        float start = spot.currentT;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Apply(spot, Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        Apply(spot, target);
        spot.cr = null;
    }

    void Apply(Spot spot, float t)
    {
        spot.currentT = t;
        var c = spot.col;
        spot.cone.color = new Color(c.r, c.g, c.b, t * PeakAlpha);
        spot.glow.color = new Color(c.r, c.g, c.b, t * PeakAlpha * 0.55f);
        if (spot.light != null)
            spot.light.intensity = t * PeakLightIntens;
    }

    // ── Spot 생성 ────────────────────────────────────────────────────────────

    Spot CreateSpot(string name)
    {
        var rootGO = new GameObject(name);
        rootGO.transform.SetParent(transform);

        var spot = new Spot { col = ColSpot, root = rootGO.transform };

        spot.cone = MakeSR(rootGO, "Cone", _coneTex, 1);
        spot.cone.transform.localScale = new Vector3(ConeW, ConeH, 1f);

        spot.glow = MakeSR(rootGO, "Glow", _glowTex, 1);
        spot.glow.transform.localScale = new Vector3(GlowW, GlowH, 1f);

        var lightGO = new GameObject("Light2D");
        lightGO.transform.SetParent(rootGO.transform);
        var l2d = lightGO.AddComponent<Light2D>();
        l2d.lightType             = Light2D.LightType.Point;
        l2d.pointLightInnerRadius = LightInner;
        l2d.pointLightOuterRadius = LightOuter;
        l2d.color                 = Color.white;
        l2d.intensity             = 0f;
        spot.light = l2d;

        Apply(spot, 0f);
        return spot;
    }

    SpriteRenderer MakeSR(GameObject parent, string goName, Texture2D tex, int order)
    {
        var go    = new GameObject(goName);
        go.transform.SetParent(parent.transform);
        var sr    = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                  new Vector2(0.5f, 0.5f), tex.width);
        sr.sortingOrder = order;
        sr.color        = Color.clear;
        return sr;
    }

    // ── 텍스처 빌드 ──────────────────────────────────────────────────────────

    static Texture2D BuildConeTex(int w, int h)
    {
        var tex  = new Texture2D(w, h, TextureFormat.RGBA32, false);
        float iw = 1f / (w - 1), ih = 1f / (h - 1);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            float nx = x * iw;
            float ny = y * ih;  // 0=하단(캐릭터), 1=상단(하늘)

            float halfW = (1f - ny) * 0.47f;
            if (halfW < 0.001f) { tex.SetPixel(x, y, Color.clear); continue; }

            float dx = Mathf.Abs(nx - 0.5f);
            if (dx > halfW) { tex.SetPixel(x, y, Color.clear); continue; }

            float edgeT   = 1f - dx / halfW;
            float heightF = Mathf.Pow(1f - ny, 0.35f);
            float alpha   = Mathf.Pow(edgeT, 1.3f) * heightF;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        return tex;
    }

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
