using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;
using System.Collections;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    // ── Persisted values ─────────────────────────────────────────────────
    public static float Volume     { get; private set; } = 1f;
    public static float Brightness { get; private set; } = 1f;
    public static bool  Fullscreen { get; private set; } = true;

    public bool IsPanelOpen { get; private set; }

    // ── UI refs ───────────────────────────────────────────────────────────
    GameObject      _panel;
    Image           _brightnessOverlay;
    TextMeshProUGUI _countdownText;
    GameObject      _settingsPage;
    GameObject      _calibrationPage;
    CalibrationUI   _calibUI;
    Slider          _volumeSlider;
    Slider          _brightnessSlider;
    Toggle          _fullscreenToggle;

    // ── Pause state ───────────────────────────────────────────────────────
    AudioSource _gameAudio;
    Coroutine   _resumeRoutine;
    bool        _pausedBySettings;

    // ── BGM ducking (calibration page) ───────────────────────────────────
    AudioSource _bgmSource;
    float       _bgmOriginalVolume;
    bool        _bgmDucked;

    // ── Bootstrap ─────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[SettingsManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<SettingsManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPrefs();
        EnsureEventSystem();
        BuildPersistentUI();
        ApplySettings();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _calibUI?.Hide();
        EnsureEventSystem();
        Time.timeScale    = 1f;
        IsPanelOpen       = false;
        _pausedBySettings = false;
        _panel?.SetActive(false);
        _gameAudio = null;
        _bgmSource = null;
        _bgmOriginalVolume = 0f;
        _bgmDucked = false;

        if (_resumeRoutine != null) { StopCoroutine(_resumeRoutine); _resumeRoutine = null; }
        if (_countdownText != null) { _countdownText.text = ""; _countdownText.gameObject.SetActive(false); }

        ApplySettings();
    }

    // ── EventSystem ───────────────────────────────────────────────────────

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var esGO = new GameObject("[EventSystem]");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
    }

    // ── Input ─────────────────────────────────────────────────────────────

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (IsPanelOpen) Close();
            else if (PauseMenuManager.Instance == null && Stage1PauseMenuManager.Instance == null) Open();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void Open(int page = 0)
    {
        SFXManager.PlayClick();
        _pausedBySettings = Time.timeScale > 0f;
        if (_pausedBySettings)
        {
            _gameAudio = FindGameAudio();
            _gameAudio?.Pause();
            Time.timeScale = 0f;
        }

        if (_resumeRoutine != null) { StopCoroutine(_resumeRoutine); _resumeRoutine = null; }

        SyncSlidersToValues();
        _panel.SetActive(true);
        ShowPage(page);
        IsPanelOpen = true;
    }

    public void Close()
    {
        SFXManager.PlayBack();
        _calibUI?.Hide();
        DuckBGM(false);
        _panel.SetActive(false);
        IsPanelOpen = false;

        if (_pausedBySettings)
        {
            _pausedBySettings = false;
            string sceneName = SceneManager.GetActiveScene().name;
            bool inBattle = sceneName == "BattleScene" || sceneName.StartsWith("Stage1");
            if (inBattle && _gameAudio != null)
                _resumeRoutine = StartCoroutine(CountdownResume());
            else
            {
                Time.timeScale = 1f;
                _gameAudio = null;
            }
        }
    }

    // ── Countdown resume ─────────────────────────────────────────────────

    IEnumerator CountdownResume()
    {
        _countdownText.gameObject.SetActive(true);
        string[] steps = { "3", "2", "1", "GO!" };
        foreach (var s in steps)
        {
            _countdownText.text = s;
            yield return new WaitForSecondsRealtime(0.9f);
        }
        _countdownText.text = "";
        _countdownText.gameObject.SetActive(false);
        Time.timeScale = 1f;
        _gameAudio?.UnPause();
        _gameAudio = null;
        _resumeRoutine = null;
    }

    // ── Persistent UI builder ─────────────────────────────────────────────

    void BuildPersistentUI()
    {
        var cGO = new GameObject("[SettingsCanvas]");
        DontDestroyOnLoad(cGO);
        var canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
        cGO.AddComponent<GraphicRaycaster>();

        // Brightness overlay
        var bGO  = new GameObject("BrightnessOverlay");
        bGO.transform.SetParent(cGO.transform, false);
        var bRect = bGO.AddComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one;
        bRect.offsetMin = bRect.offsetMax = Vector2.zero;
        _brightnessOverlay               = bGO.AddComponent<Image>();
        _brightnessOverlay.color         = new Color(0f, 0f, 0f, 1f - Brightness);
        _brightnessOverlay.raycastTarget = false;

        // Countdown text
        var cdGO = new GameObject("CountdownText");
        cdGO.transform.SetParent(cGO.transform, false);
        var cdRect = cdGO.AddComponent<RectTransform>();
        cdRect.anchorMin        = new Vector2(0.5f, 0.5f);
        cdRect.anchorMax        = new Vector2(0.5f, 0.5f);
        cdRect.pivot            = new Vector2(0.5f, 0.5f);
        cdRect.sizeDelta        = new Vector2(400f, 300f);
        cdRect.anchoredPosition = Vector2.zero;
        _countdownText           = cdGO.AddComponent<TextMeshProUGUI>();
        _countdownText.fontSize  = 200f;
        _countdownText.fontStyle = FontStyles.Bold;
        _countdownText.alignment = TextAlignmentOptions.Center;
        _countdownText.color     = Color.white;
        _countdownText.raycastTarget = false;
        cdGO.SetActive(false);

        BuildSettingsPanel(cGO.transform);
        _panel.SetActive(false);
    }

    void BuildSettingsPanel(Transform root)
    {
        _panel = new GameObject("SettingsPanel");
        _panel.transform.SetParent(root, false);
        var pRect = _panel.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0.25f, 0.1f);
        pRect.anchorMax = new Vector2(0.75f, 0.9f);
        pRect.offsetMin = pRect.offsetMax = Vector2.zero;
        _panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.12f, 0.96f);

        BuildTabBar(_panel.transform);
        _settingsPage    = BuildSettingsPage(_panel.transform);
        _calibrationPage = BuildCalibrationPage(_panel.transform);
    }

    void BuildTabBar(Transform parent)
    {
        var barGO  = new GameObject("TabBar");
        barGO.transform.SetParent(parent, false);
        var barRect = barGO.AddComponent<RectTransform>();
        barRect.anchorMin        = new Vector2(0f, 1f);
        barRect.anchorMax        = new Vector2(1f, 1f);
        barRect.pivot            = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta        = new Vector2(0f, 60f);

        string[] tabs = { "SETTINGS", "CALIBRATION" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int   idx  = i;
            float xMin = i == 0 ? 0f : 0.5f;
            var   tGO  = new GameObject("Tab_" + tabs[i]);
            tGO.transform.SetParent(barGO.transform, false);
            var tRect = tGO.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(xMin, 0f);
            tRect.anchorMax = new Vector2(xMin + 0.5f, 1f);
            tRect.offsetMin = tRect.offsetMax = Vector2.zero;
            tGO.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.22f, 1f);
            var btn = tGO.AddComponent<Button>();
            btn.onClick.AddListener(() => ShowPage(idx));

            var lblGO   = new GameObject("Label");
            lblGO.transform.SetParent(tGO.transform, false);
            var lblRect = lblGO.AddComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = lblRect.offsetMax = Vector2.zero;
            var lbl           = lblGO.AddComponent<TextMeshProUGUI>();
            lbl.text          = tabs[i];
            lbl.fontSize      = 20f;
            lbl.alignment     = TextAlignmentOptions.Center;
            lbl.color         = Color.white;
            lbl.raycastTarget = false;
        }
    }

    void ShowPage(int idx)
    {
        SFXManager.PlayNavigate();
        _settingsPage?.SetActive(idx == 0);
        _calibrationPage?.SetActive(idx == 1);
        if (idx == 1) { _calibUI?.Show(); DuckBGM(true); }
        else          { _calibUI?.Hide(); DuckBGM(false); }
    }

    void DuckBGM(bool duck)
    {
        if (_bgmSource == null)
        {
            var bgmGO = GameObject.Find("BGM");
            if (bgmGO != null) _bgmSource = bgmGO.GetComponent<AudioSource>();
        }
        if (_bgmSource == null) return;

        if (duck && !_bgmDucked)
        {
            _bgmOriginalVolume = _bgmSource.volume;
            _bgmSource.volume  = _bgmOriginalVolume * 0.5f;
            _bgmDucked = true;
        }
        else if (!duck && _bgmDucked)
        {
            _bgmSource.volume = _bgmOriginalVolume;
            _bgmDucked = false;
        }
    }

    // ── Settings page ─────────────────────────────────────────────────────

    GameObject BuildSettingsPage(Transform parent)
    {
        var page = MakePage(parent, "SettingsPage");

        float y = -100f;

        AddRowLabel(page.transform, "VOLUME", y);
        _volumeSlider = MakeSlider(page.transform, new Vector2(0f, y - 40f));
        _volumeSlider.onValueChanged.AddListener(v => Volume = v);
        y -= 120f;

        AddRowLabel(page.transform, "BRIGHTNESS", y);
        _brightnessSlider = MakeSlider(page.transform, new Vector2(0f, y - 40f));
        _brightnessSlider.onValueChanged.AddListener(v =>
        {
            Brightness = v;
            if (_brightnessOverlay != null)
                _brightnessOverlay.color = new Color(0f, 0f, 0f, 1f - v);
        });
        y -= 120f;

        AddRowLabel(page.transform, "FULLSCREEN", y);
        _fullscreenToggle = MakeToggle(page.transform, new Vector2(0f, y - 40f));
        _fullscreenToggle.onValueChanged.AddListener(v => Fullscreen = v);

        var applyGO   = new GameObject("ApplyButton");
        applyGO.transform.SetParent(page.transform, false);
        var applyRect = applyGO.AddComponent<RectTransform>();
        applyRect.anchorMin        = new Vector2(0.5f, 0f);
        applyRect.anchorMax        = new Vector2(0.5f, 0f);
        applyRect.pivot            = new Vector2(0.5f, 0f);
        applyRect.sizeDelta        = new Vector2(240f, 60f);
        applyRect.anchoredPosition = new Vector2(0f, 40f);
        applyGO.AddComponent<Image>().color = new Color(0.10f, 0.60f, 0.30f);
        applyGO.AddComponent<Button>().onClick.AddListener(ApplyAndSave);

        var applyLblGO   = new GameObject("Label");
        applyLblGO.transform.SetParent(applyGO.transform, false);
        var applyLblRect = applyLblGO.AddComponent<RectTransform>();
        applyLblRect.anchorMin = Vector2.zero; applyLblRect.anchorMax = Vector2.one;
        applyLblRect.offsetMin = applyLblRect.offsetMax = Vector2.zero;
        var applyLbl           = applyLblGO.AddComponent<TextMeshProUGUI>();
        applyLbl.text          = "APPLY";
        applyLbl.fontSize      = 24f;
        applyLbl.alignment     = TextAlignmentOptions.Center;
        applyLbl.color         = Color.white;
        applyLbl.raycastTarget = false;

        return page;
    }

    void ApplyAndSave()
    {
        ApplySettings();
        SavePrefs();
    }

    void ApplySettings()
    {
        AudioListener.volume = Volume;
        Screen.fullScreen    = Fullscreen;
        if (_brightnessOverlay != null)
            _brightnessOverlay.color = new Color(0f, 0f, 0f, 1f - Brightness);
    }

    void SyncSlidersToValues()
    {
        if (_volumeSlider     != null) _volumeSlider.value     = Volume;
        if (_brightnessSlider != null) _brightnessSlider.value = Brightness;
        if (_fullscreenToggle != null) _fullscreenToggle.isOn  = Fullscreen;
    }

    // ── Calibration page — delegates to CalibrationUI ────────────────────

    GameObject BuildCalibrationPage(Transform parent)
    {
        var page = MakePage(parent, "CalibrationPage");

        // Header label
        var hdrGO   = new GameObject("CalibHeader");
        hdrGO.transform.SetParent(page.transform, false);
        var hdrRect = hdrGO.AddComponent<RectTransform>();
        hdrRect.anchorMin        = new Vector2(0.5f, 1f);
        hdrRect.anchorMax        = new Vector2(0.5f, 1f);
        hdrRect.pivot            = new Vector2(0.5f, 1f);
        hdrRect.sizeDelta        = new Vector2(600f, 34f);
        hdrRect.anchoredPosition = new Vector2(0f, -70f);
        var hdrTxt           = hdrGO.AddComponent<TextMeshProUGUI>();
        hdrTxt.text          = "INPUT OFFSET CALIBRATION";
        hdrTxt.fontSize      = 18f;
        hdrTxt.alignment     = TextAlignmentOptions.Center;
        hdrTxt.color         = new Color(0.75f, 0.75f, 0.95f);
        hdrTxt.raycastTarget = false;

        // Attach CalibrationUI component — it builds its own child UI
        _calibUI = page.AddComponent<CalibrationUI>();

        page.SetActive(false);
        return page;
    }

    // ── UI helpers ────────────────────────────────────────────────────────

    static AudioSource FindGameAudio()
    {
        var rm = RhythmManager.Instance;
        if (rm != null) return rm.GetComponent<AudioSource>();

        var cc = FindObjectOfType<CallClock>();
        return cc != null ? cc.GetComponent<AudioSource>() : null;
    }

    GameObject MakePage(Transform parent, string name)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(0f, -60f);
        return go;
    }

    void AddRowLabel(Transform parent, string text, float anchoredY)
    {
        var go   = new GameObject("Lbl_" + text);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(600f, 34f);
        rect.anchoredPosition = new Vector2(0f, anchoredY);
        var tmp               = go.AddComponent<TextMeshProUGUI>();
        tmp.text              = text;
        tmp.fontSize          = 18f;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.color             = new Color(0.75f, 0.75f, 0.95f);
        tmp.raycastTarget     = false;
    }

    Slider MakeSlider(Transform parent, Vector2 anchoredPos)
    {
        var go   = new GameObject("Slider");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(500f, 28f);
        rect.anchoredPosition = anchoredPos;

        var bgGO   = new GameObject("Bg");
        bgGO.transform.SetParent(go.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        bgGO.AddComponent<Image>().color = new Color(0.20f, 0.20f, 0.30f);

        var faGO   = new GameObject("FillArea");
        faGO.transform.SetParent(go.transform, false);
        var faRect = faGO.AddComponent<RectTransform>();
        faRect.anchorMin = new Vector2(0f, 0.25f); faRect.anchorMax = new Vector2(1f, 0.75f);
        faRect.offsetMin = new Vector2(5f, 0f);    faRect.offsetMax = new Vector2(-5f, 0f);

        var fillGO   = new GameObject("Fill");
        fillGO.transform.SetParent(faGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        fillGO.AddComponent<Image>().color = new Color(0.15f, 0.70f, 0.40f);

        var haGO   = new GameObject("HandleArea");
        haGO.transform.SetParent(go.transform, false);
        var haRect = haGO.AddComponent<RectTransform>();
        haRect.anchorMin = Vector2.zero; haRect.anchorMax = Vector2.one;
        haRect.offsetMin = haRect.offsetMax = Vector2.zero;

        var hGO   = new GameObject("Handle");
        hGO.transform.SetParent(haGO.transform, false);
        var hRect = hGO.AddComponent<RectTransform>();
        hRect.sizeDelta = new Vector2(22f, 22f);
        var hImg = hGO.AddComponent<Image>();
        hImg.color = Color.white;

        var slider           = go.AddComponent<Slider>();
        slider.fillRect      = fillRect;
        slider.handleRect    = hRect;
        slider.targetGraphic = hImg;
        slider.minValue      = 0f;
        slider.maxValue      = 1f;
        slider.value         = 1f;
        return slider;
    }

    Toggle MakeToggle(Transform parent, Vector2 anchoredPos)
    {
        var go   = new GameObject("Toggle");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0.5f, 1f);
        rect.sizeDelta        = new Vector2(60f, 34f);
        rect.anchoredPosition = anchoredPos;

        var bgGO  = new GameObject("Bg");
        bgGO.transform.SetParent(go.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.20f, 0.20f, 0.30f);

        var ckGO   = new GameObject("Checkmark");
        ckGO.transform.SetParent(bgGO.transform, false);
        var ckRect = ckGO.AddComponent<RectTransform>();
        ckRect.anchorMin = new Vector2(0.1f, 0.1f); ckRect.anchorMax = new Vector2(0.9f, 0.9f);
        ckRect.offsetMin = ckRect.offsetMax = Vector2.zero;
        var ckImg = ckGO.AddComponent<Image>();
        ckImg.color = new Color(0.15f, 0.70f, 0.40f);

        var toggle           = go.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic       = ckImg;
        toggle.isOn          = Screen.fullScreen;
        return toggle;
    }

    // ── PlayerPrefs ───────────────────────────────────────────────────────

    void LoadPrefs()
    {
        Volume     = PlayerPrefs.GetFloat("cfg_volume",     1f);
        Brightness = PlayerPrefs.GetFloat("cfg_brightness", 1f);
        Fullscreen = PlayerPrefs.GetInt  ("cfg_fullscreen", 1) == 1;
        // Calibration offset is loaded by CalibrationManager (BeforeSceneLoad)
    }

    static void SavePrefs()
    {
        PlayerPrefs.SetFloat("cfg_volume",     Volume);
        PlayerPrefs.SetFloat("cfg_brightness", Brightness);
        PlayerPrefs.SetInt  ("cfg_fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
        // Calibration offset is saved by CalibrationManager
    }
}
