using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Calibration page UI. Attach to the calibration page container inside SettingsManager.
/// Creates and drives CalibrationSession + CalibrationVisualMetronome.
/// </summary>
public class CalibrationUI : MonoBehaviour
{
    CalibrationSession         _session;
    CalibrationVisualMetronome _visual;

    TextMeshProUGUI _offsetLabel;
    TextMeshProUGUI _sessionResultLabel;
    TextMeshProUGUI _earlyLateLabel;
    TextMeshProUGUI _progressLabel;
    TextMeshProUGUI _statusLabel;
    Button          _startBtn;
    Button          _retestBtn;

    void Awake()
    {
        var sessionGO = new GameObject("[CalibrationSession]");
        DontDestroyOnLoad(sessionGO);
        _session = sessionGO.AddComponent<CalibrationSession>();
        _session.OnSessionComplete += OnSessionComplete;

        var visualGO = new GameObject("[CalibrationVisual]");
        visualGO.transform.SetParent(transform, false);
        _visual = visualGO.AddComponent<CalibrationVisualMetronome>();
        _visual.Initialize(_session, transform);

        // Build UI in Awake so labels/buttons exist before Show() is called.
        // SettingsManager calls Show() in the same frame as SetActive(true),
        // before Start() would normally run.
        BuildUI();
        RefreshOffsetLabel();
    }

    void OnDestroy()
    {
        if (_session != null)
        {
            _session.Stop();
            Destroy(_session.gameObject);
        }
    }

    public void Show()
    {
        _session.Stop();
        RefreshOffsetLabel();
        _startBtn.gameObject.SetActive(true);
        _retestBtn.gameObject.SetActive(false);
        _sessionResultLabel.text = "";
        _earlyLateLabel.text     = "";
        _statusLabel.text        = "";
        _progressLabel.text      = "Press START to begin";
    }

    public void Hide()
    {
        _session.Stop();
    }

    void Update()
    {
        if (_session == null || !_session.IsActive) return;

        _progressLabel.text = _session.IsWarmup
            ? $"Warmup  ({_session.CurrentBeatIndex} / {_session.WarmupBeats})"
            : $"Measuring:  {_session.ValidSampleCount} / {_session.TargetSamples}";

        _statusLabel.text = _session.IsWarmup
            ? "Press SPACE in rhythm"
            : "Press SPACE on each beat";
    }

    void BuildUI()
    {
        MakeCenteredLabel(
            "Press SPACE in time with each click",
            15f, new Color(0.65f, 0.65f, 0.85f), new Vector2(0f, -75f));

        _offsetLabel = MakeCenteredLabel(
            "Judgement offset: 0 ms", 22f, Color.white, new Vector2(0f, -105f));

        _sessionResultLabel = MakeCenteredLabel(
            "", 17f, new Color(0.6f, 1f, 0.65f), new Vector2(0f, -135f));

        _earlyLateLabel = MakeCenteredLabel(
            "", 15f, new Color(0.65f, 0.65f, 0.90f), new Vector2(0f, -160f));

        _progressLabel = MakeCenteredLabel(
            "Press START to begin", 16f, new Color(0.55f, 0.80f, 1f), new Vector2(0f, -185f));

        _statusLabel = MakeCenteredLabel(
            "", 17f, new Color(1f, 0.85f, 0.30f), new Vector2(0f, -210f));

        // Offset buttons: centered side by side
        MakeButton("Offset -5ms", -100f, -300f, 175f, 48f,
            new Color(0.50f, 0.20f, 0.20f), () => Adjust(-5f));
        MakeButton("Offset +5ms",  100f, -300f, 175f, 48f,
            new Color(0.20f, 0.20f, 0.55f), () => Adjust(+5f));

        // Action buttons: START/RETRY on left, RESET on right
        _startBtn = MakeButton("START", -100f, -360f, 175f, 52f,
            new Color(0.10f, 0.55f, 0.30f), StartSession);

        _retestBtn = MakeButton("RETRY", -100f, -360f, 175f, 52f,
            new Color(0.15f, 0.42f, 0.65f), StartSession);
        _retestBtn.gameObject.SetActive(false);

        MakeButton("RESET", 100f, -360f, 175f, 52f,
            new Color(0.42f, 0.10f, 0.10f), Reset);
    }

    void StartSession()
    {
        _session.Begin();
        _startBtn.gameObject.SetActive(false);
        _retestBtn.gameObject.SetActive(false);
        _sessionResultLabel.text = "";
        _earlyLateLabel.text     = "";
        _statusLabel.text        = "Starting...";
    }

    void OnSessionComplete()
    {
        RefreshOffsetLabel();
        _retestBtn.gameObject.SetActive(true);
        _statusLabel.text = "Complete!";

        float  avg = _session.LastAverageErrorMs;
        string dir = avg > 0f ? "Late" : "Early";
        _sessionResultLabel.text =
            $"Avg error: {avg:+0.0;-0.0} ms ({dir})   New offset: {_session.FinalOffsetMs:+0.0;-0.0;0} ms";
        _earlyLateLabel.text =
            $"Early {_session.EarlyCount}  /  Late {_session.LateCount}  /  Excluded {_session.ExcludedCount}";
    }

    void Adjust(float deltaMs)
    {
        CalibrationManager.Instance?.AdjustJudgementOffset(deltaMs);
        RefreshOffsetLabel();
    }

    void Reset()
    {
        _session.Stop();
        CalibrationManager.Instance?.ResetCalibration();
        RefreshOffsetLabel();
        _startBtn.gameObject.SetActive(true);
        _retestBtn.gameObject.SetActive(false);
        _sessionResultLabel.text = "";
        _earlyLateLabel.text     = "";
        _statusLabel.text        = "Reset";
        _progressLabel.text      = "Press START to begin";
    }

    void RefreshOffsetLabel()
    {
        float offset = CalibrationManager.Instance?.JudgementOffsetMs ?? 0f;
        if (_offsetLabel != null)
            _offsetLabel.text = $"Judgement offset:  {offset:+0.0;-0.0;0} ms";
    }

    // ── UI helpers ────────────────────────────────────────────────────────

    TextMeshProUGUI MakeCenteredLabel(string text, float size, Color color, Vector2 pos)
    {
        var go   = new GameObject("Lbl");
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(580f, 40f);
        rect.anchoredPosition = pos;
        var tmp           = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    Button MakeButton(string label, float x, float y, float w, float h,
                      Color color, System.Action onClick)
    {
        var go   = new GameObject("Btn_" + label);
        go.transform.SetParent(transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);
        go.AddComponent<Image>().color = color;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        var lblGO   = new GameObject("L");
        lblGO.transform.SetParent(go.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
        var tmp           = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text          = label;
        tmp.fontSize      = 19f;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }
}
