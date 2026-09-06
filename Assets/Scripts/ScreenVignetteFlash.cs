using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Hit/Miss 판정 시 화면 테두리를 색으로 플래시하는 비네트 이펙트.
/// UIManager.Start()에서 AddComponent로 생성됨.
/// </summary>
public class ScreenVignetteFlash : MonoBehaviour
{
    public static ScreenVignetteFlash Instance { get; private set; }

    [SerializeField] float _peakAlpha = 0.40f;
    [SerializeField] float _fadeIn    = 0.05f;
    [SerializeField] float _fadeOut   = 0.18f;

    static readonly Color ColHit  = new Color(0.10f, 0.60f, 1.00f);
    static readonly Color ColMiss = new Color(1.00f, 0.08f, 0.08f);

    Image     _img;
    Coroutine _cr;
    Texture2D _tex;
    Sprite    _sprite;

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnJudgement += Flash;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null)
            GameManager.Instance.OnJudgement -= Flash;
        if (_tex    != null) Destroy(_tex);
        if (_sprite != null) Destroy(_sprite);
    }

    // ── UI 빌드 ──────────────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("ScreenVignette");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var rt = go.AddComponent<RectTransform>();
        // 화면 전체 스트레치가 아니라 기준 프레임(1920x1080, 중앙 고정)에 맞춤 — 21:9 같은
        // 울트라와이드에서 AspectRatioLetterbox가 x=±960 바깥을 검은 필러박스 바(sortingOrder 999)로
        // 덮어버려서, 풀스트레치 비네트는 좌우 가장자리가 그 바 밑에 깔려 안 보이고 위아래만 보였음
        // (신고받음). 기준 프레임 크기로 맞추면 좌우 글로우가 바 안쪽 경계에 딱 붙어서 다 보임.
        // CanvasScaler(matchWidthOrHeight=1)가 이 좌표를 필러박스 프레임과 정확히 일치시켜 줌.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1920f, 1080f);
        rt.anchoredPosition = Vector2.zero;

        const int S = 256;
        _tex    = BuildVignetteTex(S);
        _sprite = Sprite.Create(_tex, new Rect(0, 0, S, S), Vector2.one * 0.5f, S);

        _img               = go.AddComponent<Image>();
        _img.sprite        = _sprite;
        _img.type          = Image.Type.Simple;
        _img.color         = Color.clear;
        _img.raycastTarget = false;
    }

    // ── 플래시 ───────────────────────────────────────────────────────────────

    void Flash(JudgementType j)
    {
        if (_img == null) return;
        if (_cr != null) StopCoroutine(_cr);
        _cr = StartCoroutine(DoFlash(j == JudgementType.Hit ? ColHit : ColMiss));
    }

    IEnumerator DoFlash(Color col)
    {
        for (float t = 0f; t < _fadeIn; t += Time.deltaTime)
        {
            _img.color = new Color(col.r, col.g, col.b, Mathf.Lerp(0f, _peakAlpha, t / _fadeIn));
            yield return null;
        }
        _img.color = new Color(col.r, col.g, col.b, _peakAlpha);

        for (float t = 0f; t < _fadeOut; t += Time.deltaTime)
        {
            _img.color = new Color(col.r, col.g, col.b, Mathf.Lerp(_peakAlpha, 0f, t / _fadeOut));
            yield return null;
        }
        _img.color = Color.clear;
        _cr = null;
    }

    // ── 비네트 텍스처 생성 ───────────────────────────────────────────────────
    // 가장자리=불투명, 중앙=투명인 직사각형 그라디언트

    static Texture2D BuildVignetteTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float inv = 1f / (size - 1);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float nx       = x * inv;
                float ny       = y * inv;
                float edgeDist = Mathf.Min(Mathf.Min(nx, 1f - nx), Mathf.Min(ny, 1f - ny));
                float alpha    = Mathf.Exp(-edgeDist / 0.045f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }
}
