using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public enum ArrowDir { Left, Right, Up, Down }

/// <summary>
/// Optional bonus command system.
/// A random arrow-key sequence is shown top-left.
/// Each input must match the next arrow AND be in rhythm (within commandBeatWindow of a beat).
/// Success → gauge fills; new command generated.
/// Failure → progress resets, same command stays.
/// </summary>
public class CommandSystem : MonoBehaviour
{
    public static CommandSystem Instance { get; private set; }

    [Header("Command Settings")]
    [Range(3, 10)] public int   commandMinLength  = 3;
    [Range(3, 10)] public int   commandMaxLength  = 7;
    [Range(0.05f, 0.3f)] public float commandBeatWindow = 0.15f;

    ArrowDir[]        _command;
    int               _progress;

    TextMeshProUGUI   _commandLabel;
    TextMeshProUGUI   _commandText;
    Image             _panelBg;

    TextMeshProUGUI   _inputHistoryText;

    // Colours for arrow display
    const string ColDone    = "#44FF88";
    const string ColCurrent = "#FFEE22";
    const string ColPending = "#888888";
    const string ColFail    = "#FF4444";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        BuildUI();
        GenerateNewCommand();
    }

    // ── Public API ────────────────────────────────────────────────────

    public void TryInput(ArrowDir dir)
    {
        if (_command == null || _command.Length == 0) return;

        bool inRhythm = IsInRhythm();
        bool correct  = dir == _command[_progress];

        if (!inRhythm || !correct)
        {
            _progress = 0;
            FlashFail();
            UpdateDisplay();
            return;
        }

        _progress++;
        UpdateDisplay();

        if (_progress >= _command.Length)
        {
            GaugeManager.Instance?.FillFromCommand();
            GenerateNewCommand();
        }
    }

    // ── Internals ─────────────────────────────────────────────────────

    bool IsInRhythm()
    {
        float songTime = RhythmManager.Instance.SongTime;
        if (songTime < 0f) return false;

        float beat    = RhythmManager.Instance.BeatInterval;
        float phase   = ((songTime % beat) + beat) % beat;
        float distToNearestBeat = Mathf.Min(phase, beat - phase);
        return distToNearestBeat <= commandBeatWindow;
    }

    void GenerateNewCommand()
    {
        commandMaxLength = Mathf.Max(commandMinLength, commandMaxLength);
        int len = Random.Range(commandMinLength, commandMaxLength + 1);
        _command = new ArrowDir[len];
        for (int i = 0; i < len; i++)
            _command[i] = (ArrowDir)Random.Range(0, 4);
        _progress = 0;
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (_commandText == null) return;

        var sb = new StringBuilder();
        for (int i = 0; i < _command.Length; i++)
        {
            string arrow  = DirToArrow(_command[i]);
            string col    = i < _progress   ? ColDone
                          : i == _progress  ? ColCurrent
                          :                   ColPending;
            bool   bold   = i == _progress;
            sb.Append($"<color={col}>{(bold ? "<b>" : "")}{arrow}{(bold ? "</b>" : "")}</color>");
            if (i < _command.Length - 1) sb.Append(" ");
        }
        _commandText.text = sb.ToString();

        UpdateInputHistory();
    }

    void UpdateInputHistory()
    {
        if (_inputHistoryText == null) return;

        if (_progress == 0)
        {
            _inputHistoryText.text = "<color=#444466>- - -</color>";
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < _progress; i++)
        {
            sb.Append($"<color={ColDone}><b>{DirToArrow(_command[i])}</b></color>");
            if (i < _progress - 1) sb.Append(" ");
        }
        _inputHistoryText.text = sb.ToString();
    }

    Coroutine _failFlash;

    void FlashFail()
    {
        if (_failFlash != null) StopCoroutine(_failFlash);
        _failFlash = StartCoroutine(FailFlashRoutine());
    }

    System.Collections.IEnumerator FailFlashRoutine()
    {
        if (_panelBg != null) _panelBg.color = new Color(0.5f, 0.05f, 0.05f, 0.85f);
        yield return new WaitForSeconds(0.12f);
        if (_panelBg != null) _panelBg.color = new Color(0.06f, 0.06f, 0.12f, 0.80f);
    }

    static string DirToArrow(ArrowDir dir)
    {
        switch (dir)
        {
            case ArrowDir.Left:  return "←";
            case ArrowDir.Right: return "→";
            case ArrowDir.Up:    return "↑";
            case ArrowDir.Down:  return "↓";
            default:             return "?";
        }
    }

    // ── UI Builder ────────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogWarning("CommandSystem: Canvas not found"); return; }

        var uiRoot = canvasGO.transform.Find("UIRoot");
        var parent = uiRoot != null ? uiRoot : canvasGO.transform;

        // Panel background — top-left fixed
        var panelGO   = new GameObject("CommandPanel");
        panelGO.transform.SetParent(parent, false);
        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0f, 1f);
        panelRect.anchorMax        = new Vector2(0f, 1f);
        panelRect.pivot            = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(12f, -12f);
        panelRect.sizeDelta        = new Vector2(380f, 90f);
        _panelBg               = panelGO.AddComponent<Image>();
        _panelBg.color         = new Color(0.06f, 0.06f, 0.12f, 0.80f);

        // "BONUS COMMAND" label
        var labelGO   = new GameObject("CmdLabel");
        labelGO.transform.SetParent(panelGO.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin        = Vector2.zero;
        labelRect.anchorMax        = new Vector2(1f, 1f);
        labelRect.offsetMin        = new Vector2(10f, 50f);
        labelRect.offsetMax        = new Vector2(-10f, -8f);
        _commandLabel              = labelGO.AddComponent<TextMeshProUGUI>();
        _commandLabel.text         = "BONUS COMMAND";
        _commandLabel.fontSize     = 14f;
        _commandLabel.color        = new Color(0.7f, 0.7f, 0.9f, 0.9f);
        _commandLabel.alignment    = TextAlignmentOptions.TopLeft;

        // Arrow sequence text
        var arrowGO   = new GameObject("CmdArrows");
        arrowGO.transform.SetParent(panelGO.transform, false);
        var arrowRect = arrowGO.AddComponent<RectTransform>();
        arrowRect.anchorMin        = Vector2.zero;
        arrowRect.anchorMax        = new Vector2(1f, 1f);
        arrowRect.offsetMin        = new Vector2(10f, 8f);
        arrowRect.offsetMax        = new Vector2(-10f, -32f);
        _commandText               = arrowGO.AddComponent<TextMeshProUGUI>();
        _commandText.fontSize      = 28f;
        _commandText.alignment     = TextAlignmentOptions.MidlineLeft;
        _commandText.richText      = true;

        BuildInputHistoryUI(parent);
    }

    void BuildInputHistoryUI(Transform parent)
    {
        // Hearts container bottom edge ≈ y = -14 - 36 = -50; place panel 6px below
        var panelGO   = new GameObject("InputHistoryPanel");
        panelGO.transform.SetParent(parent, false);
        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 1f);
        panelRect.anchorMax        = new Vector2(0.5f, 1f);
        panelRect.pivot            = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -90f);
        panelRect.sizeDelta        = new Vector2(420f, 80f);

        var bg  = panelGO.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.04f, 0.10f, 0.78f);

        // "CURRENT INPUT" label
        var lblGO   = new GameObject("InputLabel");
        lblGO.transform.SetParent(panelGO.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin        = Vector2.zero;
        lblRect.anchorMax        = new Vector2(1f, 1f);
        lblRect.offsetMin        = new Vector2(8f, 52f);
        lblRect.offsetMax        = new Vector2(-8f, -6f);
        var lbl          = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text         = "CURRENT INPUT";
        lbl.fontSize     = 14f;
        lbl.color        = new Color(0.6f, 0.6f, 0.85f, 0.85f);
        lbl.alignment    = TextAlignmentOptions.Center;

        // Input arrow display
        var inputGO   = new GameObject("InputArrows");
        inputGO.transform.SetParent(panelGO.transform, false);
        var inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchorMin        = Vector2.zero;
        inputRect.anchorMax        = new Vector2(1f, 1f);
        inputRect.offsetMin        = new Vector2(8f, 8f);
        inputRect.offsetMax        = new Vector2(-8f, -28f);
        _inputHistoryText          = inputGO.AddComponent<TextMeshProUGUI>();
        _inputHistoryText.fontSize = 36f;
        _inputHistoryText.alignment = TextAlignmentOptions.Center;
        _inputHistoryText.richText  = true;
        _inputHistoryText.text      = "<color=#444466>- - -</color>";
    }
}
