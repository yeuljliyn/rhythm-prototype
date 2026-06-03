using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class LobbySceneManager : MonoBehaviour
{
    // ── Stage data ────────────────────────────────────────────────────────

    static readonly string[] StageTitles = { "Stage 1", "Stage 2", "Stage 3", "Stage 4", "Stage 5" };

    static readonly string[] BossSprites =
    {
        "물귀신_배경제거",
        "지박령_배경제거",
        "총각귀신_배경제거",
        "처녀귀신_배경제거",
        "달걀귀신_배경제거",
    };

    static readonly Color[] StageCharColors =
    {
        new Color(0.25f, 0.75f, 0.40f),
        new Color(0.25f, 0.50f, 0.90f),
        new Color(0.90f, 0.55f, 0.20f),
        new Color(0.80f, 0.25f, 0.65f),
        new Color(0.95f, 0.80f, 0.10f),
    };

    // ── Mugu item data ────────────────────────────────────────────────────

    struct MuguItem { public string Name, Description; public Color IconColor; }

    static readonly MuguItem[] Items =
    {
        new MuguItem { Name="Iron Blade",   Description="A sturdy blade.\nIncreases attack power.",          IconColor=new Color(0.65f,0.65f,0.70f) },
        new MuguItem { Name="Crystal Ring", Description="Resonates with\nrhythm beats.",                     IconColor=new Color(0.40f,0.80f,1.00f) },
        new MuguItem { Name="Beat Boots",   Description="Speed up with\nthe beat.",                          IconColor=new Color(0.90f,0.70f,0.20f) },
        new MuguItem { Name="Soul Pendant", Description="Recovers HP on\nPerfect hits.",                     IconColor=new Color(0.80f,0.30f,0.80f) },
        new MuguItem { Name="Echo Gloves",  Description="Combo bonus\nx1.5 multiplier.",                     IconColor=new Color(0.30f,0.90f,0.60f) },
        new MuguItem { Name="Void Cloak",   Description="Reduces damage\nfrom Miss.",                        IconColor=new Color(0.25f,0.25f,0.40f) },
        new MuguItem { Name="Tempo Crown",  Description="Activates hyper\nmode at max gauge.",                IconColor=new Color(1.00f,0.85f,0.20f) },
        new MuguItem { Name="Shatter Helm", Description="Extra damage on\nPerfect chains.",                  IconColor=new Color(0.80f,0.30f,0.20f) },
        new MuguItem { Name="Phase Amulet", Description="Widens the Good\njudgement window.",                IconColor=new Color(0.50f,1.00f,0.80f) },
        new MuguItem { Name="Origin Stone", Description="Balanced stats.\nStarter equipment.",               IconColor=new Color(0.70f,0.60f,0.50f) },
    };

    // ── State ─────────────────────────────────────────────────────────────

    int  _selectedIdx  = 0;
    int  _unlockedCount;
    bool _muguOpen     = false;

    // ── UI refs ───────────────────────────────────────────────────────────

    GameObject[]      _cards;
    Image[]           _cardBgs;
    Image[]           _lockOverlays;
    TextMeshProUGUI[] _rankTexts;

    GameObject      _muguOverlay;
    GameObject      _muguDetailPanel;
    Image           _detailIcon;
    TextMeshProUGUI _detailName;
    TextMeshProUGUI _detailDesc;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        _unlockedCount = PlayerPrefs.GetInt("unlocked_stages", 1);
        BuildUI();
        Highlight(_selectedIdx);
        PlayBGM("Music/LobbyBGM");
    }

    void PlayBGM(string resourcePath)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null) { Debug.LogWarning($"BGM 로드 실패: {resourcePath}"); return; }
        var go  = new GameObject("BGM");
        var src = go.AddComponent<AudioSource>();
        src.clip        = clip;
        src.loop        = true;
        src.playOnAwake = false;
        src.volume      = SettingsManager.Volume;
        src.Play();
    }

    void Update()
    {
        if (_muguOpen) return;
        if (SettingsManager.Instance != null && SettingsManager.Instance.IsPanelOpen) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame)
            Move(-1);
        else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            Move(1);

        if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
            TryStart(_selectedIdx);
    }

    // ── Navigation ────────────────────────────────────────────────────────

    void Move(int dir)
    {
        int next = _selectedIdx + dir;
        if (next < 0 || next >= StageTitles.Length) return;
        _selectedIdx = next;
        SFXManager.PlayNavigate();
        Highlight(_selectedIdx);
    }

    void SelectCard(int idx)
    {
        if (idx == _selectedIdx)
            TryStart(idx);
        else
        {
            SFXManager.PlayClick();
            _selectedIdx = idx;
            Highlight(_selectedIdx);
        }
    }

    void TryStart(int idx)
    {
        if (idx >= _unlockedCount) return;
        SFXManager.PlayConfirm();
        PlayerPrefs.SetInt("current_stage", idx);
        PlayerPrefs.Save();
        SettingsManager.Instance?.Close();
        SceneManager.LoadScene("DialogueScene");
    }

    void Highlight(int idx)
    {
        for (int i = 0; i < _cards.Length; i++)
        {
            bool sel = i == idx;
            _cardBgs[i].color = sel
                ? new Color(0.18f, 0.16f, 0.36f)
                : new Color(0.08f, 0.07f, 0.18f);
        }
    }

    // ── UI Build ─────────────────────────────────────────────────────────

    void BuildUI()
    {
        var cGO    = new GameObject("Canvas");
        var canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
        cGO.AddComponent<GraphicRaycaster>();

        // Background
        MakeStretch(cGO.transform, "BG").AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.10f);

        // Title
        var titleGO  = MakeAnchored(cGO.transform, "Title", new Vector2(0.5f,1f), new Vector2(0.5f,1f),
                                    new Vector2(0.5f,1f), new Vector2(800f, 90f), new Vector2(0f,-28f));
        var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text             = "STAGE SELECT";
        titleTxt.fontSize         = 62f;
        titleTxt.fontStyle        = FontStyles.Bold;
        titleTxt.alignment        = TextAlignmentOptions.Center;
        titleTxt.color            = new Color(0.85f, 0.85f, 1.00f);
        titleTxt.characterSpacing = 6f;
        titleTxt.raycastTarget    = false;

        // Stage cards
        BuildStageCards(cGO.transform);

        // Nav hint
        var hintGO  = MakeAnchored(cGO.transform, "NavHint", new Vector2(0.5f,0f), new Vector2(0.5f,0f),
                                   new Vector2(0.5f,0f), new Vector2(700f,36f), new Vector2(0f,36f));
        var hintTxt = hintGO.AddComponent<TextMeshProUGUI>();
        hintTxt.text          = "← →  /  A D    navigate          CLICK    select          SPACE    start";
        hintTxt.fontSize      = 17f;
        hintTxt.alignment     = TextAlignmentOptions.Center;
        hintTxt.color         = new Color(0.50f, 0.50f, 0.70f, 0.85f);
        hintTxt.raycastTarget = false;

        // Back button (top-left)
        var backGO  = MakeAnchored(cGO.transform, "BackButton", new Vector2(0f,1f), new Vector2(0f,1f),
                                   new Vector2(0f,1f), new Vector2(160f,52f), new Vector2(20f,-20f));
        backGO.AddComponent<Image>().color = new Color(0.12f, 0.10f, 0.25f);
        var backBtn = backGO.AddComponent<Button>();
        backBtn.onClick.AddListener(() => { SFXManager.PlayBack(); SceneManager.LoadScene("StartScene"); });
        AddLabel(backGO.transform, "◀  BACK", 20f, Color.white);

        // Mugu button (bottom-right)
        var muguBtnGO = MakeAnchored(cGO.transform, "MuguButton", new Vector2(1f,0f), new Vector2(1f,0f),
                                     new Vector2(1f,0f), new Vector2(210f,62f), new Vector2(-20f,20f));
        muguBtnGO.AddComponent<Image>().color = new Color(0.15f, 0.10f, 0.35f);
        var muguBtn = muguBtnGO.AddComponent<Button>();
        muguBtn.onClick.AddListener(ToggleMugu);
        AddLabel(muguBtnGO.transform, "MUGU / EQUIP", 20f, Color.white);

        // Mugu overlay
        BuildMuguOverlay(cGO.transform);
    }

    void BuildStageCards(Transform parent)
    {
        int   n      = StageTitles.Length;
        float cardW  = 240f;
        float cardH  = 500f;
        float gap    = 28f;
        float totalW = n * cardW + (n - 1) * gap;
        float startX = -totalW * 0.5f + cardW * 0.5f;

        _cards        = new GameObject[n];
        _cardBgs      = new Image[n];
        _lockOverlays = new Image[n];
        _rankTexts    = new TextMeshProUGUI[n];

        for (int i = 0; i < n; i++)
        {
            int   idx      = i;
            bool  unlocked = i < _unlockedCount;
            float xPos     = startX + i * (cardW + gap);

            // Card
            var card     = MakeAnchored(parent, "Card_" + i,
                               new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                               new Vector2(0.5f,0.5f), new Vector2(cardW, cardH),
                               new Vector2(xPos, 20f));
            var cardBg   = card.AddComponent<Image>();
            cardBg.color = new Color(0.08f, 0.07f, 0.18f);
            _cards[i]    = card;
            _cardBgs[i]  = cardBg;

            var btn = card.AddComponent<Button>();
            var bc  = btn.colors;
            bc.normalColor      = Color.white;
            bc.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            bc.pressedColor     = new Color(0.85f, 0.85f, 0.85f);
            btn.colors          = bc;
            btn.targetGraphic   = cardBg;
            btn.onClick.AddListener(() => SelectCard(idx));

            // ── Stage title (top of card) ──
            var stgGO  = MakeAnchored(card.transform, "StageTitle",
                             new Vector2(0f,1f), new Vector2(1f,1f),
                             new Vector2(0.5f,1f), new Vector2(0f,44f),
                             new Vector2(0f,-12f));
            var stgTxt = stgGO.AddComponent<TextMeshProUGUI>();
            stgTxt.text          = StageTitles[i];
            stgTxt.fontSize      = 22f;
            stgTxt.fontStyle     = FontStyles.Bold;
            stgTxt.alignment     = TextAlignmentOptions.Center;
            stgTxt.color         = Color.white;
            stgTxt.raycastTarget = false;

            // ── Rank text ──
            var rankGO  = MakeAnchored(card.transform, "Rank",
                              new Vector2(0f,1f), new Vector2(1f,1f),
                              new Vector2(0.5f,1f), new Vector2(0f,30f),
                              new Vector2(0f,-58f));
            var rankTxt = rankGO.AddComponent<TextMeshProUGUI>();
            string saved = PlayerPrefs.GetString($"stage_{i}_rank", "--");
            rankTxt.text          = $"Best Rank: {saved}";
            rankTxt.fontSize      = 16f;
            rankTxt.alignment     = TextAlignmentOptions.Center;
            rankTxt.color         = new Color(0.90f, 0.85f, 0.45f);
            rankTxt.raycastTarget = false;
            _rankTexts[i]         = rankTxt;

            // ── Boss image (fills lower 70% of card) ──
            var charGO  = MakeAnchored(card.transform, "Character",
                              new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.90f),
                              new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var charImg = charGO.AddComponent<Image>();
            var bossSprite = Resources.Load<Sprite>(BossSprites[i]);
            if (bossSprite != null)
            {
                charImg.sprite         = bossSprite;
                charImg.preserveAspect = true;
                charImg.color          = Color.white;
            }
            else
            {
                charImg.color = StageCharColors[i];
            }

            // ── Lock overlay ──
            var lockGO  = MakeStretch(card.transform, "LockOverlay");
            var lockImg = lockGO.AddComponent<Image>();
            lockImg.raycastTarget = false;
            lockImg.color = unlocked ? new Color(0,0,0,0) : new Color(0,0,0,0.75f);
            _lockOverlays[i] = lockImg;

            if (!unlocked)
            {
                var lockedLblGO  = MakeAnchored(lockGO.transform, "LockedLbl",
                                       new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                                       new Vector2(0.5f,0.5f), new Vector2(180f,50f),
                                       Vector2.zero);
                var lockedLbl    = lockedLblGO.AddComponent<TextMeshProUGUI>();
                lockedLbl.text          = "LOCKED";
                lockedLbl.fontSize      = 26f;
                lockedLbl.fontStyle     = FontStyles.Bold;
                lockedLbl.alignment     = TextAlignmentOptions.Center;
                lockedLbl.color         = new Color(0.85f, 0.85f, 0.85f, 0.90f);
                lockedLbl.raycastTarget = false;
            }
        }
    }

    // ── Mugu overlay ──────────────────────────────────────────────────────

    void BuildMuguOverlay(Transform parent)
    {
        // Full-screen semi-transparent backdrop
        _muguOverlay = MakeStretch(parent, "MuguOverlay");
        _muguOverlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);
        _muguOverlay.AddComponent<Button>().onClick.AddListener(ToggleMugu); // click outside = close

        // Panel
        var panelGO  = MakeAnchored(_muguOverlay.transform, "MuguPanel",
                           new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                           new Vector2(0.5f,0.5f), new Vector2(940f, 580f), Vector2.zero);
        panelGO.AddComponent<Image>().color = new Color(0.06f, 0.05f, 0.14f, 0.97f);
        // Prevent backdrop click-through on panel area
        var panelBlocker = panelGO.AddComponent<Button>();
        panelBlocker.onClick.AddListener(() => { }); // swallow clicks

        // Title
        var pTitleGO  = MakeAnchored(panelGO.transform, "MuguTitle",
                            new Vector2(0f,1f), new Vector2(1f,1f),
                            new Vector2(0.5f,1f), new Vector2(0f,55f), new Vector2(0f,-2f));
        var pTitleTxt = pTitleGO.AddComponent<TextMeshProUGUI>();
        pTitleTxt.text          = "MUGU  /  EQUIP";
        pTitleTxt.fontSize      = 28f;
        pTitleTxt.fontStyle     = FontStyles.Bold;
        pTitleTxt.alignment     = TextAlignmentOptions.Center;
        pTitleTxt.color         = new Color(0.85f, 0.75f, 1.00f);
        pTitleTxt.raycastTarget = false;

        // Close ✕ button
        var closeGO = MakeAnchored(panelGO.transform, "CloseBtn",
                          new Vector2(1f,1f), new Vector2(1f,1f),
                          new Vector2(1f,1f), new Vector2(46f,46f), new Vector2(-8f,-6f));
        closeGO.AddComponent<Image>().color = new Color(0.50f, 0.10f, 0.12f);
        closeGO.AddComponent<Button>().onClick.AddListener(ToggleMugu);
        AddLabel(closeGO.transform, "✕", 22f, Color.white);

        // Item grid
        BuildMuguGrid(panelGO.transform);

        // Detail panel (on top of grid, hidden by default)
        BuildMuguDetail(panelGO.transform);

        _muguOverlay.SetActive(false);
    }

    void BuildMuguGrid(Transform parent)
    {
        const int   cols   = 5;
        const float iconSz = 88f;
        const float nameH  = 40f;
        const float hGap   = 24f;
        const float vGap   = 18f;
        const float cellW  = iconSz + hGap;
        const float cellH  = iconSz + nameH + vGap;
        float totalW = cols * iconSz + (cols - 1) * hGap;
        float startX = -totalW * 0.5f + iconSz * 0.5f;
        float startY = 110f;  // offset from panel center upward

        for (int i = 0; i < Items.Length; i++)
        {
            int   idx = i;
            int   col = i % cols;
            int   row = i / cols;
            float x   = startX + col * cellW;
            float y   = startY - row * cellH;

            // Icon button
            var iconGO  = MakeAnchored(parent, $"MuguIcon_{i}",
                              new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                              new Vector2(0.5f,0.5f), new Vector2(iconSz, iconSz),
                              new Vector2(x, y));
            iconGO.AddComponent<Image>().color = Items[i].IconColor;
            var iconBtn = iconGO.AddComponent<Button>();
            iconBtn.onClick.AddListener(() => ShowMuguDetail(idx));

            // Name label below icon
            var nameGO  = MakeAnchored(parent, $"MuguName_{i}",
                              new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                              new Vector2(0.5f,1f), new Vector2(iconSz + 14f, nameH),
                              new Vector2(x, y - iconSz * 0.5f - 2f));
            var nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
            nameTxt.text          = Items[i].Name;
            nameTxt.fontSize      = 12f;
            nameTxt.alignment     = TextAlignmentOptions.Center;
            nameTxt.color         = new Color(0.78f, 0.78f, 0.90f);
            nameTxt.raycastTarget = false;
        }
    }

    void BuildMuguDetail(Transform parent)
    {
        _muguDetailPanel = MakeStretch(parent, "MuguDetail");
        _muguDetailPanel.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.12f, 0.95f);

        // Large icon
        var iconGO  = MakeAnchored(_muguDetailPanel.transform, "DetailIcon",
                          new Vector2(0.5f,1f), new Vector2(0.5f,1f),
                          new Vector2(0.5f,1f), new Vector2(150f,150f),
                          new Vector2(0f,-80f));
        _detailIcon = iconGO.AddComponent<Image>();

        // Name
        var nameGO  = MakeAnchored(_muguDetailPanel.transform, "DetailName",
                          new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                          new Vector2(0.5f,1f), new Vector2(600f,50f),
                          new Vector2(0f,55f));
        _detailName           = nameGO.AddComponent<TextMeshProUGUI>();
        _detailName.fontSize  = 32f;
        _detailName.fontStyle = FontStyles.Bold;
        _detailName.alignment = TextAlignmentOptions.Center;
        _detailName.color     = Color.white;
        _detailName.raycastTarget = false;

        // Description
        var descGO  = MakeAnchored(_muguDetailPanel.transform, "DetailDesc",
                          new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                          new Vector2(0.5f,1f), new Vector2(600f,80f),
                          new Vector2(0f,-5f));
        _detailDesc           = descGO.AddComponent<TextMeshProUGUI>();
        _detailDesc.fontSize  = 20f;
        _detailDesc.alignment = TextAlignmentOptions.Center;
        _detailDesc.color     = new Color(0.75f, 0.75f, 0.92f);
        _detailDesc.raycastTarget = false;

        // Back button
        var backGO = MakeAnchored(_muguDetailPanel.transform, "DetailBack",
                         new Vector2(0.5f,0f), new Vector2(0.5f,0f),
                         new Vector2(0.5f,0f), new Vector2(180f,52f),
                         new Vector2(0f,50f));
        backGO.AddComponent<Image>().color = new Color(0.20f, 0.15f, 0.35f);
        backGO.AddComponent<Button>().onClick.AddListener(HideMuguDetail);
        AddLabel(backGO.transform, "◀  BACK", 21f, Color.white);

        _muguDetailPanel.SetActive(false);
    }

    // ── Mugu logic ────────────────────────────────────────────────────────

    void ToggleMugu()
    {
        _muguOpen = !_muguOpen;
        _muguOverlay.SetActive(_muguOpen);
        if (_muguOpen) SFXManager.PlayClick(); else SFXManager.PlayBack();
        if (!_muguOpen) HideMuguDetail();
    }

    void ShowMuguDetail(int idx)
    {
        _detailIcon.color = Items[idx].IconColor;
        _detailName.text  = Items[idx].Name;
        _detailDesc.text  = Items[idx].Description;
        _muguDetailPanel.SetActive(true);
    }

    void HideMuguDetail() => _muguDetailPanel?.SetActive(false);

    // ── UI helpers ────────────────────────────────────────────────────────

    static GameObject MakeStretch(Transform parent, string name)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject MakeAnchored(Transform parent, string name,
                                    Vector2 anchorMin, Vector2 anchorMax,
                                    Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.sizeDelta        = sizeDelta;
        rect.anchoredPosition = anchoredPos;
        return go;
    }

    static void AddLabel(Transform parent, string text, float fontSize, Color color)
    {
        var go   = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var tmp           = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = color;
        tmp.raycastTarget = false;
    }
}
