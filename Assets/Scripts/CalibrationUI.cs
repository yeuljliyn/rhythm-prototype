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
    }

    void Start()
    {
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

    // Called by SettingsManager when switching to calibration tab
    public void Show()
    {
        _session.Stop();
        RefreshOffsetLabel();
        _startBtn.gameObject.SetActive(true);
        _retestBtn.gameObject.SetActive(false);
        _sessionResultLabel.text = "";
        _earlyLateLabel.text     = "";
        _statusLabel.text        = "";
        _progressLabel.text      = "측정 시작 버튼을 누르세요";
    }

    // Called by SettingsManager when leaving calibration tab
    public void Hide()
    {
        _session.Stop();
    }

    void Update()
    {
        if (_session == null || !_session.IsActive) return;

        _progressLabel.text = _session.IsWarmup
            ? $"워밍업 ({_session.CurrentBeatIndex} / {_session.WarmupBeats})"
            : $"측정 중:  {_session.ValidSampleCount} / {_session.TargetSamples}";

        _statusLabel.text = _session.IsWarmup
            ? "박자에 맞춰 방향키를 눌러주세요"
            : $"다음 방향:  {DirToStr(_session.CurrentTargetDir)}";
    }

    void BuildUI()
    {
        // Instruction
        MakeCenteredLabel(
            "클릭 소리에 맞춰 표시된 방향키를 누르세요",
            15f, new Color(0.65f, 0.65f, 0.85f), new Vector2(0f, -75f));

        _offsetLabel = MakeCenteredLabel(
            "판정 오프셋: 0 ms", 22f, Color.white, new Vector2(0f, -105f));

        _sessionResultLabel = MakeCenteredLabel(
            "", 17f, new Color(0.6f, 1f, 0.65f), new Vector2(0f, -135f));

        _earlyLateLabel = MakeCenteredLabel(
            "", 15f, new Color(0.65f, 0.65f, 0.90f), new Vector2(0f, -160f));

        _progressLabel = MakeCenteredLabel(
            "측정 시작 버튼을 누르세요", 16f, new Color(0.55f, 0.80f, 1f), new Vector2(0f, -185f));

        _statusLabel = MakeCenteredLabel(
            "", 17f, new Color(1f, 0.85f, 0.30f), new Vector2(0f, -210f));

        // -- Buttons --

        _startBtn = MakeButton("측정 시작", -120f, -370f, 200f, 52f,
            new Color(0.10f, 0.55f, 0.30f), StartSession);

        _retestBtn = MakeButton("다시 측정", -120f, -370f, 200f, 52f,
            new Color(0.15f, 0.42f, 0.65f), StartSession);
        _retestBtn.gameObject.SetActive(false);

        MakeButton("판정 −5ms",  80f, -320f, 175f, 48f,
            new Color(0.50f, 0.15f, 0.15f), () => Adjust(-5f));
        MakeButton("판정 +5ms", 260f, -320f, 175f, 48f,
            new Color(0.15f, 0.15f, 0.50f), () => Adjust(+5f));

        MakeButton("초기화", 80f, -370f, 175f, 48f,
            new Color(0.42f, 0.10f, 0.10f), Reset);
    }

    void StartSession()
    {
        _session.Begin();
        _startBtn.gameObject.SetActive(false);
        _retestBtn.gameObject.SetActive(false);
        _sessionResultLabel.text = "";
        _earlyLateLabel.text     = "";
        _statusLabel.text        = "시작 중...";
    }

    void OnSessionComplete()
    {
        RefreshOffsetLabel();
        _retestBtn.gameObject.SetActive(true);
        _statusLabel.text = "측정 완료!";

        float avg   = _session.LastAverageErrorMs;
        string dir  = avg > 0f ? "늦음 (Late)" : "빠름 (Early)";
        _sessionResultLabel.text =
            $"이번 평균 오차: {avg:+0.0;-0.0} ms ({dir})\n" +
            $"→ 판정 오프셋: {_session.FinalOffsetMs:+0.0;-0.0;0} ms";
        _earlyLateLabel.text =
            $"Early {_session.EarlyCount}회  /  Late {_session.LateCount}회  /  제외 {_session.ExcludedCount}회";
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
        _statusLabel.text        = "초기화됨";
        _progressLabel.text      = "측정 시작 버튼을 누르세요";
    }

    void RefreshOffsetLabel()
    {
        float offset = CalibrationManager.Instance?.JudgementOffsetMs ?? 0f;
        if (_offsetLabel != null)
            _offsetLabel.text = $"판정 오프셋:  {offset:+0.0;-0.0;0} ms";
    }

    static string DirToStr(ArrowDir dir) => dir switch
    {
        ArrowDir.Up    => "↑ 위",
        ArrowDir.Down  => "↓ 아래",
        ArrowDir.Left  => "← 왼쪽",
        ArrowDir.Right => "→ 오른쪽",
        _              => "?"
    };

    // ── UI helpers ───────────────────────────────────────────────────────────

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
