using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 씬 전환. 기본은 검정 페이드(LoadSceneWithFade). 판에 "들어갈 때"는 연기 와이프(LoadSceneWithSmoke):
/// 연기 뭉치들이 화면을 덮었다가 새 씬 위에서 걷힌다 — docs/design.md "인트로 등장"(판에 들어간다 =
/// 개미로 변해 들어간다). 연기 퍼프는 Resources/Smoke/puff_*.png(절차 생성), 없으면 런타임 원형으로 대체.
/// 둘 다 unscaled 시간 기준이라 timeScale 0에서도 돈다.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    public float fadeDuration = 0.4f;

    [Header("Smoke wipe")]
    [Tooltip("연기가 화면을 다 덮을 때까지")]
    public float smokeCoverDuration = 0.55f;
    [Tooltip("새 씬 위에서 연기가 걷히는 시간")]
    public float smokeClearDuration = 0.9f;
    public int   smokePuffCount = 40;
    [Tooltip("퍼프 기본 지름(px, 1920x1080 기준). 개별 퍼프는 1.0~1.6배 랜덤")]
    public float smokePuffSize = 480f;
    public Color smokeColor = new Color(0.86f, 0.86f, 0.93f, 1f);

    Image _overlay;
    bool  _isTransitioning;

    RectTransform         _smokeLayer;
    Image                 _smokeBacking;
    readonly List<Image>  _puffs = new List<Image>();
    Sprite[]              _puffSprites;
    bool                  _smokePending;   // 연기로 덮은 채 씬을 로드했고, 로드되면 걷어야 함

    public bool IsTransitioning => _isTransitioning;

    // ── Bootstrap ──────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[SceneTransitionManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<SceneTransitionManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        StartCoroutine(FadeIn());
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_smokePending)
        {
            _smokePending = false;
            StartCoroutine(SmokeClear());
        }
        else
        {
            StartCoroutine(FadeIn());
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────
    public void LoadSceneWithFade(string sceneName)
    {
        if (_isTransitioning) return;
        Time.timeScale = 1f;
        StartCoroutine(FadeOutThenLoad(sceneName));
    }

    /// <summary>연기가 화면을 덮은 뒤 씬을 로드하고, 새 씬 위에서 연기가 걷힌다.</summary>
    public void LoadSceneWithSmoke(string sceneName)
    {
        if (_isTransitioning) return;
        Time.timeScale = 1f;
        StartCoroutine(SmokeCoverThenLoad(sceneName));
    }

    /// <summary>씬 전환 없이 연기로 덮기만(§1 변신처럼 연기 속에서 무언가를 바꾸고 싶을 때). 끝나면 onCovered.</summary>
    public void SmokeCoverOnly(System.Action onCovered)
    {
        if (_isTransitioning) return;
        StartCoroutine(SmokeCoverOnlyRoutine(onCovered));
    }

    /// <summary>SmokeCoverOnly 뒤에 연기를 걷는다.</summary>
    public void SmokeClearNow()
    {
        StartCoroutine(SmokeClear());
    }

    // ── Black fade ─────────────────────────────────────────────────────────
    IEnumerator FadeOutThenLoad(string sceneName)
    {
        _isTransitioning = true;
        _overlay.raycastTarget = true;
        yield return StartCoroutine(Fade(0f, 1f));
        SceneManager.LoadScene(sceneName);
        // FadeIn will be triggered by OnSceneLoaded
    }

    IEnumerator FadeIn()
    {
        SetAlpha(1f);
        _overlay.raycastTarget = true;
        yield return StartCoroutine(Fade(1f, 0f));
        _overlay.raycastTarget = false;
        _isTransitioning = false;
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        SetAlpha(from);
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration)));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float a)
    {
        if (_overlay != null)
            _overlay.color = new Color(0f, 0f, 0f, a);
    }

    // ── Smoke wipe ─────────────────────────────────────────────────────────
    IEnumerator SmokeCoverThenLoad(string sceneName)
    {
        _isTransitioning = true;
        _overlay.raycastTarget = true;
        SetAlpha(0f);
        yield return StartCoroutine(SmokeCover());
        _smokePending = true;
        SceneManager.LoadScene(sceneName);
        // SmokeClear는 OnSceneLoaded에서
    }

    IEnumerator SmokeCoverOnlyRoutine(System.Action onCovered)
    {
        _isTransitioning = true;
        _overlay.raycastTarget = true;
        SetAlpha(0f);
        yield return StartCoroutine(SmokeCover());
        onCovered?.Invoke();
    }

    // 퍼프들이 흩어진 자리에서 커지며 화면을 덮는다. 뒤에 연기색 배킹을 같이 올려 틈이 없게.
    IEnumerator SmokeCover()
    {
        EnsurePuffs();
        _smokeLayer.gameObject.SetActive(true);

        var rect = _smokeLayer.rect;
        float w = rect.width, h = rect.height;
        int cols = Mathf.CeilToInt(Mathf.Sqrt(_puffs.Count * (w / h)));
        int rows = Mathf.CeilToInt((float)_puffs.Count / cols);
        float cellW = w / cols, cellH = h / rows;

        var starts  = new float[_puffs.Count];
        var targets = new float[_puffs.Count];
        for (int i = 0; i < _puffs.Count; i++)
        {
            var img = _puffs[i];
            var rt  = img.rectTransform;
            int c = i % cols, r = i / cols;
            float x = -w / 2 + (c + 0.5f) * cellW + Random.Range(-cellW * 0.35f, cellW * 0.35f);
            float y = -h / 2 + (r + 0.5f) * cellH + Random.Range(-cellH * 0.35f, cellH * 0.35f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.localRotation    = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            float size = smokePuffSize * Random.Range(1.0f, 1.6f);
            rt.sizeDelta        = new Vector2(size, size);
            rt.localScale       = Vector3.one * 0.15f;
            img.sprite          = _puffSprites[i % _puffSprites.Length];
            img.color           = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0f);
            starts[i]  = Random.Range(0f, 0.4f);   // 0~40% 구간에 흩어져 시작
            targets[i] = 1f;
        }
        _smokeBacking.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < smokeCoverDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / smokeCoverDuration);
            for (int i = 0; i < _puffs.Count; i++)
            {
                float u = Mathf.Clamp01((t - starts[i]) / (1f - starts[i]));
                float e = 1f - (1f - u) * (1f - u); // ease-out
                var rt = _puffs[i].rectTransform;
                rt.localScale = Vector3.one * Mathf.Lerp(0.15f, targets[i], e);
                rt.localRotation = Quaternion.Euler(0, 0, rt.localRotation.eulerAngles.z + 25f * Time.unscaledDeltaTime);
                var col = _puffs[i].color; col.a = Mathf.Clamp01(u * 1.6f); _puffs[i].color = col;
            }
            // 배킹은 뒤늦게 차오름 — 퍼프가 먼저 보이고, 마지막에 틈을 메운다
            float b = Mathf.Clamp01((t - 0.45f) / 0.55f);
            _smokeBacking.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, b * b);
            yield return null;
        }
        _smokeBacking.color = smokeColor;
    }

    // 새 씬 위에서: 배킹이 먼저 빠지고, 퍼프는 바깥으로 흩어지며 커지다 사라진다.
    IEnumerator SmokeClear()
    {
        if (_smokeLayer == null || !_smokeLayer.gameObject.activeSelf)
        {
            _overlay.raycastTarget = false;
            _isTransitioning = false;
            yield break;
        }
        SetAlpha(0f);
        _overlay.raycastTarget = true;

        var basePos   = new Vector2[_puffs.Count];
        var baseScale = new float[_puffs.Count];
        for (int i = 0; i < _puffs.Count; i++)
        {
            basePos[i]   = _puffs[i].rectTransform.anchoredPosition;
            baseScale[i] = _puffs[i].rectTransform.localScale.x;
        }

        float elapsed = 0f;
        while (elapsed < smokeClearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / smokeClearDuration);
            float bk = 1f - Mathf.Clamp01(t / 0.45f);          // 배킹은 앞 45%에 빠짐
            _smokeBacking.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, bk * bk);
            for (int i = 0; i < _puffs.Count; i++)
            {
                float u = Mathf.Clamp01((t - 0.15f) / 0.85f);
                float e = u * u;                                 // ease-in: 잠깐 머물다 흩어짐
                var rt = _puffs[i].rectTransform;
                rt.anchoredPosition = basePos[i] * (1f + 0.55f * e) + new Vector2(0f, 60f * e);
                rt.localScale       = Vector3.one * (baseScale[i] * (1f + 0.5f * e));
                rt.localRotation    = Quaternion.Euler(0, 0, rt.localRotation.eulerAngles.z + 20f * Time.unscaledDeltaTime);
                var col = _puffs[i].color; col.a = 1f - u; _puffs[i].color = col;
            }
            yield return null;
        }
        _smokeLayer.gameObject.SetActive(false);
        _overlay.raycastTarget = false;
        _isTransitioning = false;
    }

    void EnsurePuffs()
    {
        if (_puffSprites == null || _puffSprites.Length == 0)
        {
            var texs = Resources.LoadAll<Texture2D>("Smoke");
            var list = new List<Sprite>();
            foreach (var tex in texs)
                list.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f));
            if (list.Count == 0) list.Add(MakeFallbackPuff());
            _puffSprites = list.ToArray();
        }
        while (_puffs.Count < smokePuffCount)
        {
            var go = new GameObject("Puff", typeof(RectTransform));
            go.transform.SetParent(_smokeLayer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            _puffs.Add(img);
        }
        _smokeBacking.transform.SetAsFirstSibling();
    }

    // Resources/Smoke가 비어 있을 때의 대체: 부드러운 원
    static Sprite MakeFallbackPuff()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px  = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
            float a = Mathf.Clamp01(1f - r); a = a * a;
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ── UI Builder ─────────────────────────────────────────────────────────
    void BuildOverlay()
    {
        var canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 연기 레이어 — 검정 페이드 이미지 아래(먼저 그려짐). 배킹 + 퍼프들.
        var smokeGO = new GameObject("SmokeLayer", typeof(RectTransform));
        smokeGO.transform.SetParent(canvasGO.transform, false);
        _smokeLayer = (RectTransform)smokeGO.transform;
        FullStretch(_smokeLayer);

        var backingGO = new GameObject("SmokeBacking", typeof(RectTransform));
        backingGO.transform.SetParent(_smokeLayer, false);
        FullStretch((RectTransform)backingGO.transform);
        _smokeBacking = backingGO.AddComponent<Image>();
        _smokeBacking.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, 0f);
        _smokeBacking.raycastTarget = false;
        smokeGO.SetActive(false);

        var imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(canvasGO.transform, false);
        var rt = imgGO.AddComponent<RectTransform>();
        FullStretch(rt);

        _overlay               = imgGO.AddComponent<Image>();
        _overlay.color         = Color.black;
        _overlay.raycastTarget = true;
    }

    static void FullStretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
