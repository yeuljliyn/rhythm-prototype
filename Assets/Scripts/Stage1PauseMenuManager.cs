using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

/// <summary>
/// Stage1_x scene pause menu — same behavior as PauseMenuManager (ESC toggle, 3-2-1 resume
/// countdown, CONTINUE/SETTINGS/RETRY/LOBBY), rebuilt as its own class because Stage1 scenes' game
/// audio lives on CallClock's AudioSource, not RhythmManager. Retry reloads whichever Stage1_x scene
/// is currently active (SceneManager.GetActiveScene().name), so this one instance works across all
/// of them (Stage1Scene, Stage1_1Scene, ...) without per-scene subclassing.
/// SettingsManager defers ESC-open to this when it is active (same as it does for PauseMenuManager).
/// </summary>
public class Stage1PauseMenuManager : MonoBehaviour
{
    public static Stage1PauseMenuManager Instance { get; private set; }

    public bool IsPaused { get; private set; }

    // ── UI refs ───────────────────────────────────────────────────────────
    GameObject      _panel;
    GameObject      _dim;
    TextMeshProUGUI _countdownTxt;
    CallClock       _clock;

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

        if (sm != null && sm.IsPanelOpen) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (IsPaused) OnContinue();
            else          DoPause();
        }
    }

    // ── Pause / Resume ────────────────────────────────────────────────────

    void DoPause()
    {
        if (_resumeCr != null) { StopCoroutine(_resumeCr); _resumeCr = null; }

        _clock = FindObjectOfType<CallClock>();
        _clock?.Pause();
        Time.timeScale = 0f;

        _panel.SetActive(true);
        _dim.SetActive(true);
        IsPaused = true;
    }

    void OnContinue()
    {
        _panel.SetActive(false);
        _dim.SetActive(false);
        if (_resumeCr != null) StopCoroutine(_resumeCr);
        _resumeCr = StartCoroutine(ResumeCountdown());
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
        _clock?.Resume();
        _clock     = null;
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
        SceneTransitionManager.Instance.LoadSceneWithFade(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void OnLobby()
    {
        Time.timeScale = 1f;
        SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
    }

    // ── UI Builder ────────────────────────────────────────────────────────

    void BuildUI()
    {
        var cGO    = new GameObject("[Stage1PauseCanvas]");
        var canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
        cGO.AddComponent<GraphicRaycaster>();

        _dim = Stretch(cGO.transform, "Dim");
        var dimImg = _dim.AddComponent<Image>();
        dimImg.color          = new Color(0f, 0f, 0f, 0.55f);
        dimImg.raycastTarget  = false;
        _dim.SetActive(false);

        var cdGO  = Anchored(cGO.transform, "Countdown",
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), new Vector2(400f, 300f), Vector2.zero);
        _countdownTxt               = cdGO.AddComponent<TextMeshProUGUI>();
        _countdownTxt.fontSize      = 200f;
        _countdownTxt.fontStyle     = FontStyles.Bold;
        _countdownTxt.alignment     = TextAlignmentOptions.Center;
        _countdownTxt.color         = Color.white;
        _countdownTxt.raycastTarget = false;
        cdGO.SetActive(false);

        _panel = Anchored(cGO.transform, "PausePanel",
                     new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                     new Vector2(0f, 0.5f), new Vector2(300f, 460f),
                     new Vector2(0f, 0f));
        _panel.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.12f, 0.97f);

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

        var divGO  = Anchored(_panel.transform, "Divider",
                         new Vector2(0.05f, 1f), new Vector2(0.95f, 1f),
                         new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(0f, -72f));
        divGO.AddComponent<Image>().color = new Color(0.35f, 0.30f, 0.55f, 0.70f);

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
