using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

public class LobbySceneManager : MonoBehaviour
{
    // ── Stage data ────────────────────────────────────────────────────────

    static readonly string[] BossNames =
    {
        "물귀신", "지박령", "총각귀신", "처녀귀신", "달걀귀신"
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
        new MuguItem { Name="Iron Blade",   Description="A sturdy blade.\nIncreases attack power.",   IconColor=new Color(0.65f,0.65f,0.70f) },
        new MuguItem { Name="Crystal Ring", Description="Resonates with\nrhythm beats.",              IconColor=new Color(0.40f,0.80f,1.00f) },
        new MuguItem { Name="Beat Boots",   Description="Speed up with\nthe beat.",                   IconColor=new Color(0.90f,0.70f,0.20f) },
        new MuguItem { Name="Soul Pendant", Description="Recovers HP on\nPerfect hits.",              IconColor=new Color(0.80f,0.30f,0.80f) },
        new MuguItem { Name="Echo Gloves",  Description="Combo bonus\nx1.5 multiplier.",              IconColor=new Color(0.30f,0.90f,0.60f) },
        new MuguItem { Name="Void Cloak",   Description="Reduces damage\nfrom Miss.",                 IconColor=new Color(0.25f,0.25f,0.40f) },
        new MuguItem { Name="Tempo Crown",  Description="Activates hyper\nmode at max gauge.",         IconColor=new Color(1.00f,0.85f,0.20f) },
        new MuguItem { Name="Shatter Helm", Description="Extra damage on\nPerfect chains.",            IconColor=new Color(0.80f,0.30f,0.20f) },
        new MuguItem { Name="Phase Amulet", Description="Widens the Good\njudgement window.",          IconColor=new Color(0.50f,1.00f,0.80f) },
        new MuguItem { Name="Origin Stone", Description="Balanced stats.\nStarter equipment.",         IconColor=new Color(0.70f,0.60f,0.50f) },
    };

    // ── Inspector ─────────────────────────────────────────────────────────

    [SerializeField] Sprite _bgSprite;
    [SerializeField] Sprite _backIconSprite;
    [SerializeField] Sprite _muguIconSprite;
    [SerializeField] Sprite _leftArrowSprite;
    [SerializeField] Sprite _rightArrowSprite;
    [SerializeField] Sprite _notPlayedSprite;
    [SerializeField] Sprite[] _stageIconSprites;   // index 0-4 (stage 1-5)
    [SerializeField] Sprite[] _stageBossSprites;   // index 0-4 (stage 1-5)
    [SerializeField] Sprite[] _stageLockedSprites; // index 0-4 (stage 2-5만 사용, index 1=지박령, 2=총각귀신, 3=처녀귀신, 4=달걀귀신)
    [SerializeField] Sprite _rankS;
    [SerializeField] Sprite _rankA;
    [SerializeField] Sprite _rankB;
    [SerializeField] Sprite _rankC;

    // ── State ─────────────────────────────────────────────────────────────

    int  _selectedIdx  = 0;
    int  _unlockedCount;
    bool _muguOpen     = false;

    // ── Dynamic UI refs ───────────────────────────────────────────────────

    Image           _stageTitleImg;
    TextMeshProUGUI _stageTitleTxt;
    Image           _rankStatusImgFull;   // 1920x1080 오버레이 (플레이하지않음)
    Image           _rankStatusImgSmall;  // 고정 크기 (S/A/B/C 랭크)
    Image           _bossImgFull;         // 풀스크린 오버레이 (모든 스테이지)
    GameObject      _lockedOverlayFull;
    Image           _lockedImg;           // 스테이지별 잠금 이미지
    CanvasGroup     _leftArrowCG;
    CanvasGroup     _rightArrowCG;

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
        if (next < 0 || next >= BossNames.Length) return;
        _selectedIdx = next;
        SFXManager.PlayNavigate();
        RefreshStageView(_selectedIdx);
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

    // ── Stage view refresh ────────────────────────────────────────────────

    void RefreshStageView(int idx)
    {
        // Title icon
        var iconSpr = (_stageIconSprites != null && idx < _stageIconSprites.Length) ? _stageIconSprites[idx] : null;
        if (iconSpr != null)
        {
            _stageTitleImg.sprite = iconSpr;
            _stageTitleImg.color  = Color.white;
            _stageTitleTxt.gameObject.SetActive(false);
        }
        else
        {
            _stageTitleImg.sprite = null;
            _stageTitleImg.color  = Color.clear;
            _stageTitleTxt.gameObject.SetActive(true);
            _stageTitleTxt.text = $"Stage{idx + 1}. {BossNames[idx]}";
        }

        // Rank icon
        string savedRank = PlayerPrefs.GetString($"stage_{idx}_rank", "--");
        bool hasRank = savedRank == "S" || savedRank == "A" || savedRank == "B" || savedRank == "C";

        _rankStatusImgFull.gameObject.SetActive(!hasRank);
        _rankStatusImgFull.sprite = _notPlayedSprite;
        _rankStatusImgFull.color  = !hasRank ? Color.white : Color.clear;

        _rankStatusImgSmall.gameObject.SetActive(hasRank);
        if (hasRank)
        {
            _rankStatusImgSmall.sprite = GetRankSprite(savedRank);
            _rankStatusImgSmall.color  = Color.white;
        }

        // Boss — 모든 스테이지 풀스크린
        var bossSpr = (_stageBossSprites != null && idx < _stageBossSprites.Length) ? _stageBossSprites[idx] : null;
        if (bossSpr != null)
        {
            _bossImgFull.sprite = bossSpr;
            _bossImgFull.color  = Color.white;
        }
        else
        {
            _bossImgFull.sprite = null;
            _bossImgFull.color  = StageCharColors[idx];
        }

        // Lock
        bool locked = idx >= _unlockedCount;
        _lockedOverlayFull.SetActive(locked);
        if (locked)
        {
            var lockedSpr = (_stageLockedSprites != null && idx < _stageLockedSprites.Length)
                            ? _stageLockedSprites[idx] : null;
            if (lockedSpr != null)
            {
                _lockedImg.sprite = lockedSpr;
                _lockedImg.color  = Color.white;
            }
            else
            {
                _lockedImg.sprite = null;
                _lockedImg.color  = new Color(0f, 0f, 0f, 0.78f);
            }
        }

        // Arrow fade
        if (_leftArrowCG  != null) { _leftArrowCG.alpha  = idx > 0                    ? 1f : 0.25f; _leftArrowCG.interactable  = idx > 0; }
        if (_rightArrowCG != null) { _rightArrowCG.alpha = idx < BossNames.Length - 1 ? 1f : 0.25f; _rightArrowCG.interactable = idx < BossNames.Length - 1; }
    }

    Sprite GetRankSprite(string rank) => rank switch
    {
        "S" => _rankS,
        "A" => _rankA,
        "B" => _rankB,
        "C" => _rankC,
        _   => _notPlayedSprite
    };

    // ── UI Build ──────────────────────────────────────────────────────────

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
        var bgImg = MakeStretch(cGO.transform, "BG").AddComponent<Image>();
        if (_bgSprite != null)
        {
            bgImg.sprite = _bgSprite;
            bgImg.type   = Image.Type.Simple;
            bgImg.color  = Color.white;
        }
        else
        {
            bgImg.color = new Color(0.04f, 0.03f, 0.10f);
        }

        // Stage title — 1920x1080 full-screen overlay image
        var titleGO = MakeStretch(cGO.transform, "StageTitle");
        _stageTitleImg = titleGO.AddComponent<Image>();
        _stageTitleImg.type          = Image.Type.Simple;
        _stageTitleImg.preserveAspect = false;
        _stageTitleImg.raycastTarget  = false;

        // Text fallback (stages 2-5 which have no dedicated icon image)
        var titleTxtGO = MakeAnchored(titleGO.transform, "TitleText",
                             new Vector2(0.5f,1f), new Vector2(0.5f,1f), new Vector2(0.5f,1f),
                             new Vector2(860f, 160f), new Vector2(0f, -130f));
        _stageTitleTxt = titleTxtGO.AddComponent<TextMeshProUGUI>();
        _stageTitleTxt.fontSize      = 64f;
        _stageTitleTxt.fontStyle     = FontStyles.Bold;
        _stageTitleTxt.alignment     = TextAlignmentOptions.Center;
        _stageTitleTxt.color         = new Color(0.55f, 1.00f, 0.90f);
        _stageTitleTxt.raycastTarget = false;

        // 플레이하지않음 — 1920x1080 풀스크린 오버레이
        var rankFullGO = MakeStretch(cGO.transform, "RankStatusFull");
        _rankStatusImgFull = rankFullGO.AddComponent<Image>();
        _rankStatusImgFull.type           = Image.Type.Simple;
        _rankStatusImgFull.preserveAspect = false;
        _rankStatusImgFull.raycastTarget  = false;

        // 랭크 아이콘 (S/A/B/C) — 고정 크기
        var rankSmallGO = MakeAnchored(cGO.transform, "RankStatusSmall",
                              new Vector2(0.5f,1f), new Vector2(0.5f,1f), new Vector2(0.5f,1f),
                              new Vector2(200f, 200f), new Vector2(0f, -286f));
        _rankStatusImgSmall = rankSmallGO.AddComponent<Image>();
        _rankStatusImgSmall.preserveAspect = true;
        _rankStatusImgSmall.raycastTarget  = false;

        // Boss (stage 1) — 1920x1080 풀스크린 오버레이
        var bossFullGO = MakeStretch(cGO.transform, "BossCharacterFull");
        _bossImgFull = bossFullGO.AddComponent<Image>();
        _bossImgFull.type          = Image.Type.Simple;
        _bossImgFull.preserveAspect = false;
        _bossImgFull.raycastTarget  = true;
        _bossImgFull.alphaHitTestMinimumThreshold = 0.1f;
        var bossBtnFull = bossFullGO.AddComponent<Button>();
        var bcf = bossBtnFull.colors;
        bcf.normalColor = Color.white; bcf.highlightedColor = new Color(1f,1f,1f,0.9f); bcf.pressedColor = new Color(0.8f,0.8f,0.8f);
        bossBtnFull.colors = bcf;
        bossBtnFull.onClick.AddListener(() => TryStart(_selectedIdx));

        _lockedOverlayFull = MakeStretch(bossFullGO.transform, "LockedOverlayFull");
        _lockedImg               = _lockedOverlayFull.AddComponent<Image>();
        _lockedImg.type          = Image.Type.Simple;
        _lockedImg.preserveAspect = false;
        _lockedImg.raycastTarget = false;

        // Left arrow
        var leftGO  = MakeAnchored(cGO.transform, "LeftArrow",
                          new Vector2(0f,0.5f), new Vector2(0f,0.5f), new Vector2(0.5f,0.5f),
                          new Vector2(100f, 130f), new Vector2(326f, -169f));
        leftGO.transform.localScale = new Vector3(5f, 5f, 1f);
        _leftArrowCG = leftGO.AddComponent<CanvasGroup>();
        var leftImg  = leftGO.AddComponent<Image>();
        if (_leftArrowSprite != null) { leftImg.sprite = _leftArrowSprite; leftImg.color = Color.white; leftImg.preserveAspect = true; leftImg.alphaHitTestMinimumThreshold = 0.1f; }
        else { leftImg.color = Color.clear; AddLabel(leftGO.transform, "<", 70f, Color.white); }
        var leftBtn  = leftGO.AddComponent<Button>();
        var lc = leftBtn.colors; lc.normalColor = Color.white; lc.highlightedColor = new Color(1f,1f,1f,0.7f); leftBtn.colors = lc;
        leftBtn.onClick.AddListener(() => Move(-1));

        // Right arrow
        var rightGO  = MakeAnchored(cGO.transform, "RightArrow",
                           new Vector2(1f,0.5f), new Vector2(1f,0.5f), new Vector2(0.5f,0.5f),
                           new Vector2(100f, 130f), new Vector2(-333f, -169f));
        rightGO.transform.localScale = new Vector3(5f, 5f, 1f);
        _rightArrowCG = rightGO.AddComponent<CanvasGroup>();
        var rightImg  = rightGO.AddComponent<Image>();
        if (_rightArrowSprite != null) { rightImg.sprite = _rightArrowSprite; rightImg.color = Color.white; rightImg.preserveAspect = true; rightImg.alphaHitTestMinimumThreshold = 0.1f; }
        else { rightImg.color = Color.clear; AddLabel(rightGO.transform, ">", 70f, Color.white); }
        var rightBtn  = rightGO.AddComponent<Button>();
        var rc = rightBtn.colors; rc.normalColor = Color.white; rc.highlightedColor = new Color(1f,1f,1f,0.7f); rightBtn.colors = rc;
        rightBtn.onClick.AddListener(() => Move(1));

        // Start hint (bottom-center)
        var hintGO  = MakeAnchored(cGO.transform, "NavHint",
                          new Vector2(0.5f,0f), new Vector2(0.5f,0f), new Vector2(0.5f,0f),
                          new Vector2(600f, 36f), new Vector2(0f, 36f));
        var hintTxt = hintGO.AddComponent<TextMeshProUGUI>();
        hintTxt.text          = "← →  /  A D    navigate          SPACE  /  CLICK    start";
        hintTxt.fontSize      = 17f;
        hintTxt.alignment     = TextAlignmentOptions.Center;
        hintTxt.color         = new Color(0.50f, 0.50f, 0.70f, 0.85f);
        hintTxt.raycastTarget = false;

        // Back button (top-left) — 풀스크린 보스 이후 생성해야 클릭 차단 안 받음
        var backGO  = MakeAnchored(cGO.transform, "BackButton",
                          new Vector2(0f,1f), new Vector2(0f,1f), new Vector2(0.5f,0.5f),
                          new Vector2(110f, 110f), new Vector2(113f, -97f));
        var backImg = backGO.AddComponent<Image>();
        if (_backIconSprite != null)
        {
            backImg.sprite = _backIconSprite;
            backImg.color  = Color.white;
            backImg.preserveAspect = true;
            backImg.alphaHitTestMinimumThreshold = 0.1f;
        }
        else
        {
            backImg.color = Color.clear;
            AddLabel(backGO.transform, "◀", 40f, Color.white);
        }
        backGO.transform.localScale = new Vector3(3f, 3f, 1f);
        var backBtn  = backGO.AddComponent<Button>();
        var backCols = backBtn.colors;
        backCols.normalColor      = Color.white;
        backCols.highlightedColor = new Color(1f, 1f, 1f, 0.6f);
        backCols.pressedColor     = new Color(0.7f, 0.7f, 0.7f);
        backBtn.colors = backCols;
        backBtn.onClick.AddListener(() => { SFXManager.PlayBack(); SceneManager.LoadScene("StartScene"); });

        // Mugu button (bottom-right)
        var muguGO  = MakeAnchored(cGO.transform, "MuguButton",
                          new Vector2(1f,0f), new Vector2(1f,0f), new Vector2(1f,0f),
                          new Vector2(200f, 200f), new Vector2(86f, -210f));
        muguGO.transform.localScale = new Vector3(3f, 3f, 1f);
        var muguImg = muguGO.AddComponent<Image>();
        if (_muguIconSprite != null) { muguImg.sprite = _muguIconSprite; muguImg.color = Color.white; muguImg.preserveAspect = true; muguImg.alphaHitTestMinimumThreshold = 0.1f; }
        else { muguImg.color = new Color(0.15f, 0.10f, 0.35f); AddLabel(muguGO.transform, "MUGU / EQUIP", 20f, Color.white); }
        muguGO.AddComponent<Button>().onClick.AddListener(ToggleMugu);

        // Mugu overlay
        BuildMuguOverlay(cGO.transform);

        // Apply initial state
        RefreshStageView(_selectedIdx);
    }

    // ── Mugu overlay ──────────────────────────────────────────────────────

    void BuildMuguOverlay(Transform parent)
    {
        _muguOverlay = MakeStretch(parent, "MuguOverlay");
        _muguOverlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);
        _muguOverlay.AddComponent<Button>().onClick.AddListener(ToggleMugu);

        var panelGO = MakeAnchored(_muguOverlay.transform, "MuguPanel",
                          new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                          new Vector2(0.5f,0.5f), new Vector2(940f, 580f), Vector2.zero);
        panelGO.AddComponent<Image>().color = new Color(0.06f, 0.05f, 0.14f, 0.97f);
        var panelBlocker = panelGO.AddComponent<Button>();
        panelBlocker.onClick.AddListener(() => { });

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

        var closeGO = MakeAnchored(panelGO.transform, "CloseBtn",
                          new Vector2(1f,1f), new Vector2(1f,1f),
                          new Vector2(1f,1f), new Vector2(46f,46f), new Vector2(-8f,-6f));
        closeGO.AddComponent<Image>().color = new Color(0.50f, 0.10f, 0.12f);
        closeGO.AddComponent<Button>().onClick.AddListener(ToggleMugu);
        AddLabel(closeGO.transform, "✕", 22f, Color.white);

        BuildMuguGrid(panelGO.transform);
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
        float startY = 110f;

        for (int i = 0; i < Items.Length; i++)
        {
            int   idx = i;
            int   col = i % cols;
            int   row = i / cols;
            float x   = startX + col * cellW;
            float y   = startY - row * cellH;

            var iconGO  = MakeAnchored(parent, $"MuguIcon_{i}",
                              new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                              new Vector2(0.5f,0.5f), new Vector2(iconSz, iconSz),
                              new Vector2(x, y));
            iconGO.AddComponent<Image>().color = Items[i].IconColor;
            iconGO.AddComponent<Button>().onClick.AddListener(() => ShowMuguDetail(idx));

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

        var iconGO  = MakeAnchored(_muguDetailPanel.transform, "DetailIcon",
                          new Vector2(0.5f,1f), new Vector2(0.5f,1f),
                          new Vector2(0.5f,1f), new Vector2(150f,150f), new Vector2(0f,-80f));
        _detailIcon = iconGO.AddComponent<Image>();

        var nameGO  = MakeAnchored(_muguDetailPanel.transform, "DetailName",
                          new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                          new Vector2(0.5f,1f), new Vector2(600f,50f), new Vector2(0f,55f));
        _detailName           = nameGO.AddComponent<TextMeshProUGUI>();
        _detailName.fontSize  = 32f;
        _detailName.fontStyle = FontStyles.Bold;
        _detailName.alignment = TextAlignmentOptions.Center;
        _detailName.color     = Color.white;
        _detailName.raycastTarget = false;

        var descGO  = MakeAnchored(_muguDetailPanel.transform, "DetailDesc",
                          new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
                          new Vector2(0.5f,1f), new Vector2(600f,80f), new Vector2(0f,-5f));
        _detailDesc           = descGO.AddComponent<TextMeshProUGUI>();
        _detailDesc.fontSize  = 20f;
        _detailDesc.alignment = TextAlignmentOptions.Center;
        _detailDesc.color     = new Color(0.75f, 0.75f, 0.92f);
        _detailDesc.raycastTarget = false;

        var backGO = MakeAnchored(_muguDetailPanel.transform, "DetailBack",
                         new Vector2(0.5f,0f), new Vector2(0.5f,0f),
                         new Vector2(0.5f,0f), new Vector2(180f,52f), new Vector2(0f,50f));
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
