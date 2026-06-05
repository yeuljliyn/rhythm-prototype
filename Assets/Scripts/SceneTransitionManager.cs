using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    public float fadeDuration = 0.4f;

    Image _overlay;
    bool  _isTransitioning;

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
        StartCoroutine(FadeIn());
    }

    // ── Public API ─────────────────────────────────────────────────────────
    public void LoadSceneWithFade(string sceneName)
    {
        if (_isTransitioning) return;
        StartCoroutine(FadeOutThenLoad(sceneName));
    }

    // ── Coroutines ─────────────────────────────────────────────────────────
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

    // ── UI Builder ─────────────────────────────────────────────────────────
    void BuildOverlay()
    {
        var canvasGO = new GameObject("FadeCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(canvasGO.transform, false);

        var rt       = imgGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        _overlay               = imgGO.AddComponent<Image>();
        _overlay.color         = Color.black;
        _overlay.raycastTarget = true;
    }
}
