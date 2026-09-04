using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

/// <summary>
/// BattleScene-only pause menu.
/// Handles ESC, pause/resume (with 3-2-1 countdown), and the
/// Continue / Settings / Retry / Lobby buttons.
/// SettingsManager defers ESC-open to this when it is active.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    public bool IsPaused { get; private set; }

    // ── UI refs ───────────────────────────────────────────────────────────
    GameObject      _panel;
    TextMeshProUGUI _countdownTxt;
    AudioSource     _gameAudio;

    // ── State ─────────────────────────────────────────────────────────────
    bool      _settingsWasOpen;
    Coroutine _resumeCr;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var sm = SettingsManager.Instance;

        // Detect when settings panel was just closed → restore pause menu
        if (sm != null)
        {
            if (sm.IsPanelOpen)
            {
                _settingsWasOpen = true;
            }
            else if (_settingsWasOpen)
            {
                _settingsWasOpen = false;
                if (IsPaused) _panel.SetActive(true);
            }
        }

        // Don't handle ESC while settings is open
        if (sm != null && sm.IsPanelOpen) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            // IsPaused는 카운트다운이 끝날 때까지 계속 true라서, 카운트다운 도중 ESC를 또 누르면
            // "아직 paused니까" OnContinue()가 다시 불려 코루틴이 처음부터 재시작 — 카운트다운이
            // 3초로 리셋되는 버그(Stage1PauseMenuManager와 동일 계열, 같이 수정). 카운트다운 중인지
            // (_resumeCr != null)를 먼저 봐서, 그때 ESC는 재개 취소 + 일시정지 화면 복귀로 처리.
            if (_resumeCr != null) CancelResumeCountdown();
            else if (IsPaused)     OnContinue();
            else                   DoPause();
        }
    }

    // ── Pause / Resume ────────────────────────────────────────────────────

    void DoPause()
    {
        if (_resumeCr != null) { StopCoroutine(_resumeCr); _resumeCr = null; }

        _gameAudio = FindGameAudio();
        _gameAudio?.Pause();
        Time.timeScale = 0f;

        _panel.SetActive(true);
        IsPaused = true;
    }

    void OnContinue()
    {
        _panel.SetActive(false);
        if (_resumeCr != null) StopCoroutine(_resumeCr);
        _resumeCr = StartCoroutine(ResumeCountdown());
    }

    // 카운트다운 도중 다시 ESC — Time.timeScale/오디오는 DoPause() 이후로 계속 멈춰있는 상태 그대로라
    // 다시 멈출 필요 없이 카운트다운 텍스트만 지우고 패널을 도로 보여주면 됨.
    void CancelResumeCountdown()
    {
        StopCoroutine(_resumeCr);
        _resumeCr = null;

        _countdownTxt.text = "";
        _countdownTxt.gameObject.SetActive(false);

        _panel.SetActive(true);
    }

    IEnumerator ResumeCountdown()
    {
        _countdownTxt.gameObject.SetActive(true);
        string[] steps = { "3", "2", "1", "GO!" };
        foreach (var s in steps)
        {
            _countdownTxt.text = s;
            yield return new WaitForSecondsRealtime(0.85f);
        }
        _countdownTxt.text = "";
        _countdownTxt.gameObject.SetActive(false);

        Time.timeScale = 1f;
        _gameAudio?.UnPause();
        _gameAudio = null;
        IsPaused   = false;
        _resumeCr  = null;
    }

    // ── Button actions ────────────────────────────────────────────────────

    void OnSettings()
    {
        _panel.SetActive(false);
        _settingsWasOpen = false;
        SettingsManager.Instance?.Open();
    }

    void OnRetry()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("BattleScene");
    }

    void OnLobby()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    static AudioSource FindGameAudio()
    {
        var rm = RhythmManager.Instance;
        return rm != null ? rm.GetComponent<AudioSource>() : null;
    }

    // ── UI Builder ────────────────────────────────────────────────────────

    void BuildUI()
    {
        // Own canvas — below SettingsManager (sortingOrder 100) but on top of game
        var cGO    = new GameObject("[PauseCanvas]");
        var canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 1f; // 세로(높이) 기준 고정 — 와이드/울트라와이드에서 위아래 잘림 방지
        cGO.AddComponent<GraphicRaycaster>();

        // Dim overlay (full screen, non-blocking for buttons)
        var dimGO  = Stretch(cGO.transform, "Dim");
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color          = new Color(0f, 0f, 0f, 0.55f);
        dimImg.raycastTarget  = false;

        // Dim은 화면 전체를 덮어야 하니 풀스트레치 그대로 두고, 패널/카운트다운은 SafeFrame(항상
        // 정확히 1920x1080, 캔버스 중앙 고정) 안에 넣음 — Stage1PauseMenuManager와 동일한 이유(패널의
        // 왼쪽-끝 앵커가 울트라와이드에서 캔버스 자체의 넓어진 왼쪽 끝을 기준 삼아 화면 밖으로 밀려나는
        // 버그 방지).
        var safeFrameGO = new GameObject("SafeFrame", typeof(RectTransform));
        safeFrameGO.transform.SetParent(cGO.transform, false);
        var safeFrameRt = (RectTransform)safeFrameGO.transform;
        safeFrameRt.anchorMin = safeFrameRt.anchorMax = new Vector2(0.5f, 0.5f);
        safeFrameRt.pivot = new Vector2(0.5f, 0.5f);
        safeFrameRt.sizeDelta = new Vector2(1920f, 1080f);
        safeFrameRt.anchoredPosition = Vector2.zero;

        // Countdown text — screen center
        var cdGO  = Anchored(safeFrameRt, "Countdown",
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(400f, 300f), Vector2.zero);
        _countdownTxt               = cdGO.AddComponent<TextMeshProUGUI>();
        _countdownTxt.fontSize      = 200f;
        _countdownTxt.fontStyle     = FontStyles.Bold;
        _countdownTxt.alignment     = TextAlignmentOptions.Center;
        _countdownTxt.color         = Color.white;
        _countdownTxt.raycastTarget = false;
        cdGO.SetActive(false);

        // ── Pause panel — left side ──
        _panel = Anchored(safeFrameRt, "PausePanel",
                     new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                     new Vector2(0f, 0.5f), new Vector2(300f, 460f),
                     new Vector2(0f, 0f));
        _panel.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.12f, 0.97f);

        // "PAUSED" title
        var titleGO  = Anchored(_panel.transform, "Title",
                           new Vector2(0f,1f), new Vector2(1f,1f),
                           new Vector2(0.5f,1f), new Vector2(0f, 62f), new Vector2(0f,-8f));
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text             = "PAUSED";
        titleTxt.fontSize         = 36f;
        titleTxt.fontStyle        = FontStyles.Bold;
        titleTxt.alignment        = TextAlignmentOptions.Center;
        titleTxt.color            = new Color(0.85f, 0.85f, 1.00f);
        titleTxt.characterSpacing = 6f;
        titleTxt.raycastTarget    = false;

        // Divider line
        var divGO  = Anchored(_panel.transform, "Divider",
                         new Vector2(0.05f, 1f), new Vector2(0.95f, 1f),
                         new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(0f, -72f));
        divGO.AddComponent<Image>().color = new Color(0.35f, 0.30f, 0.55f, 0.70f);

        // Buttons
        float btnH  = 62f;
        float gap   = 14f;
        float startY = -96f;

        (string label, Color color, System.Action action)[] btns =
        {
            ("CONTINUE",  new Color(0.10f, 0.55f, 0.25f), OnContinue),
            ("SETTINGS",  new Color(0.20f, 0.20f, 0.40f), OnSettings),
            ("RETRY",     new Color(0.15f, 0.35f, 0.60f), OnRetry),
            ("LOBBY",     new Color(0.40f, 0.15f, 0.10f), OnLobby),
        };

        for (int i = 0; i < btns.Length; i++)
        {
            float y = startY - i * (btnH + gap);
            BuildButton(_panel.transform, btns[i].label, btns[i].color, btns[i].action, y, btnH);
        }

        _panel.SetActive(false);
    }

    void BuildButton(Transform parent, string label, Color bg,
                     System.Action onClick, float anchoredY, float height)
    {
        var go   = Anchored(parent, label + "Btn",
                       new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                       new Vector2(0.5f, 1f), new Vector2(240f, height),
                       new Vector2(0f, anchoredY));
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        var bc  = btn.colors;
        bc.highlightedColor = new Color(
            Mathf.Min(bg.r + 0.18f, 1f),
            Mathf.Min(bg.g + 0.18f, 1f),
            Mathf.Min(bg.b + 0.18f, 1f));
        bc.pressedColor = new Color(bg.r * 0.75f, bg.g * 0.75f, bg.b * 0.75f);
        btn.colors = bc;
        btn.onClick.AddListener(() => onClick());

        var lblGO   = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lblRect = lblGO.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero;
        lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
        var lbl           = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text          = label;
        lbl.fontSize      = 26f;
        lbl.fontStyle     = FontStyles.Bold;
        lbl.alignment     = TextAlignmentOptions.Center;
        lbl.color         = Color.white;
        lbl.raycastTarget = false;
    }

    static GameObject Stretch(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject Anchored(Transform parent, string name,
                                Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.sizeDelta = sizeDelta;
        r.anchoredPosition = anchoredPos;
        return go;
    }
}
