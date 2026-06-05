using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class TutorialIntroSceneManager : MonoBehaviour
{
    // ── Node types ─────────────────────────────────────────────────────
    enum NodeType { Line, Choice, Tutorial, End }

    class Node
    {
        public NodeType Type;
        public string   Speaker;
        public string   Text;
        public string[] Choices;
        public int      Next;
        public int      OnSuccess;
        public int      OnFail;
    }

    [SerializeField] TMP_FontAsset _font;

    // ── Script ─────────────────────────────────────────────────────────
    static readonly Node[] Nodes = new Node[]
    {
        // 0
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="시스템 재부팅... 으윽. 어? 화면 밖에... 누구 계십니까?", Next=1 },
        // 1 - choice
        new Node { Type=NodeType.Choice, Next=2,
            Choices=new[]{ "어, 게임 방금 켰는데. 넌 뭐야?", "뭐지? 내 컴퓨터 해킹당한 건가?" } },
        // 2
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="아, 해킹 아닙니다! 놀라셨다면 죄송합니다. 저는 잭두라고 합니다. 원인 모를 신병을 고치려고 전 재산을 털어 내림굿을 받았는데... 제 시스템에 강림하신 신이 바로 당신이신 것 같군요.", Next=3 },
        // 3 - choice
        new Node { Type=NodeType.Choice, Next=4,
            Choices=new[]{ "내가 신이라고? 난 그냥 게임 끌 건데.", "귀찮네. 그냥 종료 버튼 누르고 딴 거 할래. 수고해." } },
        // 4
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="잠깐만요! 제발 종료 버튼만은 누르지 말아주십시오! 지금 나가시면 전 내일 아침 폐차장에서 캔으로 압축됩니다 한 번만 살려주십시오...", Next=5 },
        // 5
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="다행히 굿을 받고 나서, 억울한 귀신들의 한풀이 판을 깔아주는 하드웨어는 생겼습니다. 그런데 기계인 저 혼자서는 진짜 성불이 안 됩니다. 원혼을 달랠 진짜 '알맹이', 그러니까 화면 밖 당신의 '리듬'이 개입해야만 위령이 가능합니다.", Next=6 },
        // 6 - choice
        new Node { Type=NodeType.Choice, Next=7,
            Choices=new[]{ "결국 나보고 리듬 게임 대리 뛰어달라는 소리네.", "내가 치면 돈은 네가 번다고? 날강도네 완전." } },
        // 7
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="수익은... 제가 목숨만 건지면 어떻게든 보답하겠습니다! 제발 부탁드립니다. 방식은 아주 간단합니다. 튜토리얼인 여기서만 제가 먼저 시범을 보여드릴 텐데요. 본 게임에 들어가면 귀신들이 먼저 자기들 한풀이랍시고 진상을 부리며 박자를 칠 겁니다.", Next=8 },
        // 8
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="그럼 신령님께서 제 몸을 조종해서, 방금 들은 그 박자 그대로 스페이스바를 쳐주시면 됩니다. 놈들이 선창하면, 우리가 똑같이 후창으로 대답해 주는 거죠.", Next=9 },
        // 9
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="자, 제가 먼저 연습용으로 가이드 박자를 쳐보겠습니다. 제가 '딱!' 하고 치면, 신령님께서 똑같이 스페이스바를 쳐주십시오. 부탁드립니다!", Next=10 },
        // 10 - tutorial input
        new Node { Type=NodeType.Tutorial, OnSuccess=13, OnFail=11 },
        // 11 - fail line 1
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="ㅎ... 환상적인 엇박자입니다!! 역시 신령님의 그 깊고 심오한 그루브를 저 같은 고철 덩어리가 어찌 다 이해하겠습니까!", Next=12 },
        // 12 - fail line 2 → retry
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="방금 그 예술적인 엇박자도 너~무 좋았지만, 다, 다음 실전에서는 방금 치신 것보다 아~주 조금만 더 제 가이드 박자에 가깝게 쳐주시면 수금에 아주 큰! 도움이 될 것 같습니다! 하하하!", Next=10 },
        // 13 - success line 1
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="하아... 완벽합니다. 기계는 흉내 낼 수 없는 완벽한 타이밍입니다. 감사합니다. 이 정도면 통조림이 되는 것은 면할 수 있겠습니다.", Next=14 },
        // 14 - success line 2
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="기본적인 설명은 여기까지입니다. 밖에서 의뢰인들... 아니, 원혼들이 줄을 서 있습니다.", Next=15 },
        // 15 - choice
        new Node { Type=NodeType.Choice, Next=16,
            Choices=new[]{ "그래서, 이제 누구부터 털면 되는데?", "빨리 수금하고 끝내자. 어디로 가?" } },
        // 16
        new Node { Type=NodeType.Line, Speaker="잭두",
            Text="신당 로비로 모시겠습니다. 거기서 오늘 수금할 타겟을 직접 골라주시죠. 가시죠, 신령님!", Next=17 },
        // 17 - end
        new Node { Type=NodeType.End },
    };

    // ── Appearance ─────────────────────────────────────────────────────
    static readonly Color ColA_Active   = Color.white;
    static readonly Color ColA_Inactive = new Color(0.30f, 0.30f, 0.30f, 0.65f);

    static readonly Vector3 ScaleActive   = new Vector3(1.10f, 1.10f, 1f);
    static readonly Vector3 ScaleInactive = new Vector3(0.93f, 0.93f, 1f);

    // ── UI refs ────────────────────────────────────────────────────────
    RectTransform     _charARt;
    Image             _charAImg;
    TextMeshProUGUI   _speakerTxt, _dialogueTxt;
    GameObject        _promptGO;
    GameObject        _choicePanel;
    Button[]          _choiceBtns;
    TextMeshProUGUI[] _choiceTxts;
    GameObject        _tutorialPanel;
    TextMeshProUGUI   _ddalText;
    TextMeshProUGUI   _tutHintTxt;

    // ── State ──────────────────────────────────────────────────────────
    int       _nodeIdx    = 0;
    bool      _typing       = false;
    bool      _skipReq      = false;
    bool      _waitChoice   = false;
    bool      _inTutorial   = false;
    bool      _nameRevealed = false;
    Coroutine _typeCr;
    Coroutine _animCr;
    Coroutine _tutCr;
    AudioSource _voice;
    AudioClip   _blipClip;

    // ── Lifecycle ──────────────────────────────────────────────────────
    IEnumerator Start()
    {
        Time.timeScale = 1f;
        BuildUI();
        BuildVoice();

        var spriteA = Resources.Load<Sprite>("사이보그무당_배경제거");
        if (spriteA != null) { _charAImg.sprite = spriteA; _charAImg.preserveAspect = true; _charAImg.color = ColA_Inactive; }

        _charARt.localScale = ScaleInactive;

        // 페이드 중에 TMP 폰트 아틀라스 선제 빌드 (페이드 완료 후 첫 글자 stutter 방지)
        _speakerTxt.text  = Nodes[0].Speaker;
        _dialogueTxt.text = Nodes[0].Text;
        Canvas.ForceUpdateCanvases();

        var tm = SceneTransitionManager.Instance;
        if (tm != null)
            while (tm.IsTransitioning)
                yield return null;

        _speakerTxt.text  = "";
        _dialogueTxt.text = "";
        PlayNode(0);
    }

    void Update()
    {
        if (_inTutorial) return;

        if (_waitChoice)
        {
            var kb2 = Keyboard.current;
            if (kb2 != null)
            {
                if (kb2.digit1Key.wasPressedThisFrame || kb2.numpad1Key.wasPressedThisFrame)
                    SelectChoice(0);
                else if (kb2.digit2Key.wasPressedThisFrame || kb2.numpad2Key.wasPressedThisFrame)
                    SelectChoice(1);
            }
            return;
        }

        bool advance = false;
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            advance = true;
        var ms = Mouse.current;
        if (ms != null && ms.leftButton.wasPressedThisFrame)
            advance = true;

        if (!advance) return;

        if (_typing)
        {
            _skipReq = true;
        }
        else
        {
            var node = Nodes[_nodeIdx];
            if (node.Type == NodeType.Line)
                PlayNode(node.Next);
        }
    }

    // ── Node logic ─────────────────────────────────────────────────────
    void PlayNode(int idx)
    {
        if (idx > 8) _nameRevealed = true;
        _nodeIdx = idx;
        var node = Nodes[idx];

        _choicePanel.SetActive(false);
        _tutorialPanel.SetActive(false);
        _promptGO.SetActive(false);
        _waitChoice = false;
        _inTutorial = false;

        switch (node.Type)
        {
            case NodeType.Line:
                bool isJackdoo = node.Speaker == "잭두";
                _speakerTxt.text  = isJackdoo ? "잭두" : "신령님";
                _speakerTxt.color = isJackdoo
                    ? new Color(0.25f, 0.85f, 0.50f)
                    : new Color(0.85f, 0.80f, 0.30f);

                if (_animCr != null) StopCoroutine(_animCr);
                _animCr = StartCoroutine(AnimateChars(isJackdoo));

                if (_typeCr != null) StopCoroutine(_typeCr);
                _skipReq = false;
                _typeCr  = StartCoroutine(TypeText(node.Text, isJackdoo));
                break;

            case NodeType.Choice:
                _speakerTxt.text  = _nameRevealed ? "신령님" : "???";
                _speakerTxt.color = new Color(0.85f, 0.80f, 0.30f);
                _dialogueTxt.text = "";
                if (_animCr != null) StopCoroutine(_animCr);
                _animCr = StartCoroutine(AnimateChars(false));
                ShowChoices(node.Choices, node.Next);
                break;

            case NodeType.Tutorial:
                if (_tutCr != null) StopCoroutine(_tutCr);
                _tutCr = StartCoroutine(RunTutorial(node.OnSuccess, node.OnFail));
                break;

            case NodeType.End:
                StartCoroutine(LoadLobby());
                break;
        }
    }

    // ── Choice ─────────────────────────────────────────────────────────
    void ShowChoices(string[] choices, int nextNode)
    {
        _waitChoice = true;
        _promptGO.SetActive(false);
        _choicePanel.SetActive(true);
        string[] prefixes = { "① ", "② " };
        for (int i = 0; i < _choiceBtns.Length && i < choices.Length; i++)
        {
            _choiceTxts[i].text = prefixes[i] + choices[i];
            _choiceBtns[i].gameObject.SetActive(i < choices.Length);
            int n = nextNode;
            _choiceBtns[i].onClick.RemoveAllListeners();
            _choiceBtns[i].onClick.AddListener(() =>
            {
                _choicePanel.SetActive(false);
                _waitChoice = false;
                PlayNode(n);
            });
        }
    }

    void SelectChoice(int idx)
    {
        if (!_waitChoice) return;
        if (idx < _choiceBtns.Length && _choiceBtns[idx].gameObject.activeSelf)
            _choiceBtns[idx].onClick.Invoke();
    }

    // ── Tutorial ───────────────────────────────────────────────────────
    IEnumerator RunTutorial(int onSuccess, int onFail)
    {
        _inTutorial = true;
        _speakerTxt.text  = "잭두";
        _speakerTxt.color = new Color(0.25f, 0.85f, 0.50f);
        _dialogueTxt.text = "";
        if (_animCr != null) StopCoroutine(_animCr);
        _animCr = StartCoroutine(AnimateChars(true));

        _tutorialPanel.SetActive(true);
        _ddalText.text   = "";
        _tutHintTxt.text = "";

        yield return new WaitForSecondsRealtime(0.8f);

        _ddalText.text   = "딱!";
        _tutHintTxt.text = "Space를 누르세요!";

        float window  = 1.5f;
        float elapsed = 0f;
        bool  hit     = false;

        while (elapsed < window)
        {
            elapsed += Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                hit = true;
                break;
            }
            yield return null;
        }

        _tutorialPanel.SetActive(false);
        _inTutorial = false;

        yield return new WaitForSecondsRealtime(0.15f);
        PlayNode(hit ? onSuccess : onFail);
    }

    // ── Type text ──────────────────────────────────────────────────────
    IEnumerator TypeText(string text, bool isA)
    {
        _typing           = true;
        _dialogueTxt.text = "";
        const float delay = 0.032f;

        foreach (char c in text)
        {
            if (_skipReq) { _dialogueTxt.text = text; break; }
            _dialogueTxt.text += c;
            if (c != ' ' && c != '.' && c != ',' && c != '!' && c != '?' && c != '\n' && c != '\'')
                PlayBlip(c, isA);
            yield return new WaitForSecondsRealtime(delay);
        }

        _typing = false;
        _promptGO.SetActive(true);
        StartCoroutine(BlinkPrompt());
    }

    IEnumerator AnimateChars(bool aActive)
    {
        const float dur = 0.20f;
        float elapsed = 0f;
        var aScaleFrom = _charARt.localScale;
        var aColFrom   = _charAImg.color;

        var aScaleTo = aActive ? ScaleActive   : ScaleInactive;
        var aColTo   = aActive ? ColA_Active   : ColA_Inactive;

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            _charARt.localScale = Vector3.Lerp(aScaleFrom, aScaleTo, t);
            _charAImg.color     = Color.Lerp(aColFrom, aColTo, t);
            yield return null;
        }

        _charARt.localScale = aScaleTo;
        _charAImg.color     = aColTo;
    }

    IEnumerator BlinkPrompt()
    {
        var txt = _promptGO.GetComponent<TextMeshProUGUI>();
        while (_promptGO.activeSelf)
        {
            float a = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.5f));
            if (txt != null) txt.color = new Color(0.75f, 0.75f, 1f, a);
            yield return null;
        }
    }

    IEnumerator LoadLobby()
    {
        PlayerPrefs.SetInt("tutorial_complete", 1);
        PlayerPrefs.Save();
        yield return new WaitForSecondsRealtime(0.6f);
        SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
    }

    // ── Voice ──────────────────────────────────────────────────────────
    void BuildVoice()
    {
        var go = new GameObject("[TutVoice]");
        go.transform.SetParent(transform);
        _voice             = go.AddComponent<AudioSource>();
        _voice.playOnAwake = false;
        _voice.volume      = 0.45f;
        _blipClip          = GenerateBlip();
    }

    void PlayBlip(char c, bool isA)
    {
        _voice.pitch = CharToPitch(c, isA);
        _voice.PlayOneShot(_blipClip, 0.45f);
    }

    static float CharToPitch(char c, bool isA)
    {
        char lc = char.ToLower(c);
        float p = lc switch
        {
            'a' => 1.00f, 'e' => 1.14f, 'i' => 1.30f,
            'o' => 0.88f, 'u' => 1.06f,
            _   => 0.85f + ((int)lc % 10) * 0.04f
        };
        return isA ? p * 1.10f : p * 0.88f;
    }

    static AudioClip GenerateBlip()
    {
        const int   rate = 44100;
        const float freq = 520f;
        const float dur  = 0.018f;
        int   n    = (int)(rate * dur);
        var   data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t   = (float)i / rate;
            float env = 1f - t / dur;
            data[i]   = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.9f;
        }
        var clip = AudioClip.Create("TutBlip", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ── UI Builder ─────────────────────────────────────────────────────
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

        Stretch(cGO.transform, "BG").AddComponent<Image>().color = new Color(0.04f, 0.02f, 0.08f);

        var lineGO = Anchored(cGO.transform, "Line",
            new Vector2(0f, 0.22f), new Vector2(1f, 0.22f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), Vector2.zero);
        lineGO.AddComponent<Image>().color = new Color(0.40f, 0.30f, 0.60f, 0.60f);

        // CharA — JACKDOO (center)
        var aGO = Anchored(cGO.transform, "CharA",
            new Vector2(0.31f, 0.20f), new Vector2(0.69f, 0.98f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        _charARt  = aGO.GetComponent<RectTransform>();
        _charAImg = aGO.AddComponent<Image>();
        _charAImg.color = Color.clear;

        var aLblGO = Anchored(aGO.transform, "NameA",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 1f), new Vector2(0f, 36f), new Vector2(0f, -4f));
        var aLbl = MakeTMP(aLblGO, "잭두", 18f, TextAlignmentOptions.Center);
        aLbl.color = new Color(0.55f, 1.00f, 0.65f, 0.80f);

        // Dialogue box
        var dlgBox = Anchored(cGO.transform, "DialogueBox",
            Vector2.zero, new Vector2(1f, 0.22f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        dlgBox.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.10f, 0.97f);

        var nameGO = Anchored(dlgBox.transform, "SpeakerName",
            new Vector2(0.04f, 0.62f), new Vector2(0.50f, 0.95f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _speakerTxt = MakeTMP(nameGO, "", 24f, TextAlignmentOptions.BottomLeft);
        _speakerTxt.fontStyle = FontStyles.Bold;
        _speakerTxt.color     = Color.white;

        var textGO = Anchored(dlgBox.transform, "DialogueText",
            new Vector2(0.04f, 0.06f), new Vector2(0.93f, 0.62f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _dialogueTxt = MakeTMP(textGO, "", 26f, TextAlignmentOptions.TopLeft);
        _dialogueTxt.color = new Color(0.92f, 0.92f, 1.00f);

        _promptGO = Anchored(dlgBox.transform, "Prompt",
            new Vector2(0.93f, 0.06f), new Vector2(1.00f, 0.50f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var promptTxt = MakeTMP(_promptGO, "▶", 28f, TextAlignmentOptions.Center);
        promptTxt.color = new Color(0.75f, 0.75f, 1f, 0.9f);
        _promptGO.SetActive(false);

        var skipGO = Anchored(dlgBox.transform, "SkipHint",
            new Vector2(0.04f, 0f), new Vector2(0.5f, 0.10f),
            new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
        var skipTxt = MakeTMP(skipGO, "CLICK  /  SPACE  to continue", 13f, TextAlignmentOptions.BottomLeft);
        skipTxt.color = new Color(0.45f, 0.45f, 0.65f, 0.80f);

        // Choice panel — inside dialogue box, replaces the text area
        _choicePanel = Anchored(dlgBox.transform, "ChoicePanel",
            new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.60f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);

        _choiceBtns = new Button[2];
        _choiceTxts = new TextMeshProUGUI[2];
        for (int i = 0; i < 2; i++)
        {
            float yMin = i == 0 ? 0.52f : 0.02f;
            float yMax = i == 0 ? 0.98f : 0.48f;
            var btnGO = Anchored(_choicePanel.transform, $"Choice{i}",
                new Vector2(0.00f, yMin), new Vector2(1.00f, yMax),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.14f, 0.09f, 0.24f, 0.90f);
            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = new Color(0.14f, 0.09f, 0.24f, 0.90f);
            colors.highlightedColor = new Color(0.32f, 0.22f, 0.52f, 1.00f);
            colors.pressedColor     = new Color(0.52f, 0.32f, 0.82f, 1.00f);
            btn.colors        = colors;
            btn.targetGraphic = btnImg;

            var txtGO = Stretch(btnGO.transform, "BtnText");
            var txt   = MakeTMP(txtGO, "", 19f, TextAlignmentOptions.MidlineLeft);
            txt.margin = new Vector4(10f, 0f, 4f, 0f);
            txt.color  = new Color(0.92f, 0.92f, 1.00f);

            _choiceBtns[i] = btn;
            _choiceTxts[i] = txt;
        }
        _choicePanel.SetActive(false);

        // EventSystem for mouse clicks on choice buttons
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Tutorial panel
        _tutorialPanel = Anchored(cGO.transform, "TutorialPanel",
            new Vector2(0.30f, 0.35f), new Vector2(0.70f, 0.63f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _tutorialPanel.AddComponent<Image>().color = new Color(0.05f, 0.02f, 0.10f, 0.96f);

        var ddalGO = Anchored(_tutorialPanel.transform, "DdakText",
            new Vector2(0f, 0.45f), new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _ddalText = MakeTMP(ddalGO, "딱!", 72f, TextAlignmentOptions.Center);
        _ddalText.fontStyle = FontStyles.Bold;
        _ddalText.color     = new Color(1f, 0.85f, 0.20f);

        var hintGO = Anchored(_tutorialPanel.transform, "TutHint",
            new Vector2(0f, 0f), new Vector2(1f, 0.45f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _tutHintTxt = MakeTMP(hintGO, "", 22f, TextAlignmentOptions.Center);
        _tutHintTxt.color = new Color(0.80f, 0.80f, 1.00f);

        _tutorialPanel.SetActive(false);
    }

    TextMeshProUGUI MakeTMP(GameObject go, string text, float size, TextAlignmentOptions align)
    {
        var t           = go.AddComponent<TextMeshProUGUI>();
        t.text          = text;
        t.fontSize      = size;
        t.alignment     = align;
        t.raycastTarget = false;
        if (_font != null) t.font = _font;
        return t;
    }

    // ── UI helpers ─────────────────────────────────────────────────────
    static GameObject Stretch(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r       = go.AddComponent<RectTransform>();
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
        var r             = go.AddComponent<RectTransform>();
        r.anchorMin        = anchorMin;
        r.anchorMax        = anchorMax;
        r.pivot            = pivot;
        r.sizeDelta        = sizeDelta;
        r.anchoredPosition = anchoredPos;
        return go;
    }
}
