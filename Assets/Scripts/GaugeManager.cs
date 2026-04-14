using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Survival-style gauge: starts full, drains over time, fills on note hit.
/// Builds its own UI bar at runtime — no prefab needed.
/// </summary>
public class GaugeManager : MonoBehaviour
{
    public static GaugeManager Instance { get; private set; }

    [Header("Gauge Settings")]
    public float drainPerSecond = 0.055f;
    public float perfectFill   = 0.13f;
    public float goodFill      = 0.07f;

    public float Gauge { get; private set; } = 1f;

    Image _fill;

    static readonly Color ColHigh = new Color(0.15f, 0.90f, 1.00f);   // cyan
    static readonly Color ColMid  = new Color(1.00f, 0.85f, 0.10f);   // yellow
    static readonly Color ColLow  = new Color(1.00f, 0.20f, 0.15f);   // red

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        BuildBar();
        GameManager.Instance.OnJudgement += OnJudgement;
        Refresh();
    }

    void BuildBar()
    {
        // Parent to UIRoot (which corrects canvas Y-inversion) so anchors work normally
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogWarning("GaugeManager: Canvas not found"); return; }
        var uiRoot = canvasGO.transform.Find("UIRoot");
        var parent = uiRoot != null ? uiRoot : canvasGO.transform;

        // ── Outer container (dark bg) ──────────────────────────────────
        var bgGO   = new GameObject("GaugeBackground");
        bgGO.transform.SetParent(parent, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0.5f, 1f);
        bgRect.anchorMax        = new Vector2(0.5f, 1f);
        bgRect.pivot            = new Vector2(0.5f, 1f);
        bgRect.anchoredPosition = new Vector2(0f, -14f);
        bgRect.sizeDelta        = new Vector2(520f, 10f);
        var bgImg  = bgGO.AddComponent<Image>();
        bgImg.color             = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        // ── Fill bar ───────────────────────────────────────────────────
        var fillGO   = new GameObject("GaugeFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin  = Vector2.zero;
        fillRect.anchorMax  = Vector2.one;
        fillRect.offsetMin  = Vector2.zero;
        fillRect.offsetMax  = Vector2.zero;
        _fill               = fillGO.AddComponent<Image>();
        _fill.type          = Image.Type.Filled;
        _fill.fillMethod    = Image.FillMethod.Horizontal;
        _fill.fillOrigin    = 0;   // left → right
        _fill.color         = ColHigh;
    }

    void Update()
    {
        if (RhythmManager.Instance == null) return;
        if (RhythmManager.Instance.SongTime < 0f) return;   // wait through intro

        Gauge = Mathf.Clamp01(Gauge - drainPerSecond * Time.deltaTime);
        Refresh();
    }

    void OnJudgement(JudgementType type)
    {
        if      (type == JudgementType.Perfect) Gauge = Mathf.Clamp01(Gauge + perfectFill);
        else if (type == JudgementType.Good)    Gauge = Mathf.Clamp01(Gauge + goodFill);
        Refresh();
    }

    void Refresh()
    {
        if (_fill == null) return;
        _fill.fillAmount = Gauge;

        // colour transitions: cyan → yellow → red
        Color c;
        if      (Gauge > 0.55f) c = Color.Lerp(ColMid,  ColHigh, (Gauge - 0.55f) / 0.45f);
        else if (Gauge > 0.25f) c = Color.Lerp(ColMid,  ColHigh, (Gauge - 0.25f) / 0.30f);
        else                    c = Color.Lerp(ColLow,  ColMid,  Gauge / 0.25f);
        _fill.color = c;
    }
}
