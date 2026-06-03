using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Command bonus gauge — fills in segments when a command is completed.
/// Does not drain. At 100% triggers OnGaugeFull and resets.
/// </summary>
public class GaugeManager : MonoBehaviour
{
    public static GaugeManager Instance { get; private set; }

    [Header("Gauge Settings")]
    [Range(0.05f, 1f)] public float fillPerCommand = 0.10f;   // 10% per success → 10 commands = full

    public float Gauge { get; private set; } = 0f;

    public System.Action OnGaugeFull;

    Image[]    _segments;
    const int  SegmentCount = 10;
    const float SegmentGap  = 5f;

    static readonly Color ColEmpty = new Color(0.10f, 0.10f, 0.18f, 0.92f);
    static readonly Color ColFill  = new Color(0.15f, 0.85f, 1.00f, 1.00f);
    static readonly Color ColFull  = new Color(1.00f, 0.80f, 0.10f, 1.00f);

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start() => BuildSegmentedBar();

    /// <summary>Called by CommandSystem on a successful command.</summary>
    public void FillFromCommand()
    {
        Gauge = Mathf.Clamp01(Gauge + fillPerCommand);
        Refresh();

        if (Gauge >= 1f)
            TriggerFull();
    }

    void TriggerFull()
    {
        OnGaugeFull?.Invoke();
        Debug.Log("[GaugeManager] Gauge FULL — effect TBD by designer");
        Gauge = 0f;
        Refresh();
    }

    void BuildSegmentedBar()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogWarning("GaugeManager: Canvas not found"); return; }

        var uiRoot = canvasGO.transform.Find("UIRoot");
        var parent = uiRoot != null ? uiRoot : canvasGO.transform;

        // Root container (bottom center)
        var rootGO   = new GameObject("GaugeRoot");
        rootGO.transform.SetParent(parent, false);
        var rootRect = rootGO.AddComponent<RectTransform>();
        rootRect.anchorMin        = new Vector2(0.5f, 0f);
        rootRect.anchorMax        = new Vector2(0.5f, 0f);
        rootRect.pivot            = new Vector2(0f, 0f);   // left-bottom pivot (segments fill left→right)
        rootRect.anchoredPosition = new Vector2(-275f, 18f);
        rootRect.sizeDelta        = new Vector2(550f, 30f);

        // Label
        var lblGO   = new GameObject("GaugeLabel");
        lblGO.transform.SetParent(rootGO.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin        = new Vector2(0.5f, 1f);
        lblRect.anchorMax        = new Vector2(0.5f, 1f);
        lblRect.pivot            = new Vector2(0.5f, 0f);
        lblRect.anchoredPosition = new Vector2(0f, 2f);
        lblRect.sizeDelta        = new Vector2(200f, 18f);
        var lbl = lblGO.AddComponent<TMPro.TextMeshProUGUI>();
        lbl.text      = "POWER";
        lbl.fontSize  = 12f;
        lbl.alignment = TMPro.TextAlignmentOptions.Center;
        lbl.color     = new Color(0.7f, 0.85f, 1f, 0.8f);

        // Segments
        _segments = new Image[SegmentCount];
        float totalWidth = rootRect.sizeDelta.x;
        float segW = (totalWidth - SegmentGap * (SegmentCount - 1)) / SegmentCount;

        for (int i = 0; i < SegmentCount; i++)
        {
            var segGO   = new GameObject($"Seg{i}");
            segGO.transform.SetParent(rootGO.transform, false);
            var segRect = segGO.AddComponent<RectTransform>();
            // Anchor to left edge of parent, stack from left to right
            segRect.anchorMin        = new Vector2(0f, 0f);
            segRect.anchorMax        = new Vector2(0f, 1f);
            segRect.pivot            = new Vector2(0f, 0.5f);
            segRect.sizeDelta        = new Vector2(segW, 0f);
            segRect.anchoredPosition = new Vector2(i * (segW + SegmentGap), 0f);

            _segments[i]       = segGO.AddComponent<Image>();
            _segments[i].color = ColEmpty;
        }

        Refresh();
    }

    void Refresh()
    {
        if (_segments == null) return;
        float filled = Gauge * SegmentCount;
        bool  isFull = Gauge >= 1f;

        for (int i = 0; i < SegmentCount; i++)
        {
            if (_segments[i] == null) continue;
            _segments[i].color = i < filled
                ? (isFull ? ColFull : ColFill)
                : ColEmpty;
        }
    }
}
