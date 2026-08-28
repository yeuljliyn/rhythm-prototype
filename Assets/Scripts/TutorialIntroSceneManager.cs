using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// TutorialIntroScene  → 튜토리얼 시작 대사 → BattleScene(tutorial 플래그)
/// TutorialClearScene  → 튜토리얼 클리어 대사 → LobbyScene
/// 씬 이름으로 자동 분기.
/// </summary>
public class TutorialIntroSceneManager : MonoBehaviour
{
    // ── Node types ─────────────────────────────────────────────────────
    enum NodeType { Line, Choice, End }

    class Node
    {
        public NodeType  Type;
        public string    Speaker;
        public string    ChoiceSpk;
        public string    Text;
        public string[]  Choices;
        public int       Next;
        public string    EmotionKey;
    }

    [System.Serializable]
    public class EmotionEntry
    {
        public string key;
        public Sprite sprite;
    }

    // ── Tutorial START 대사 ────────────────────────────────────────────
    static readonly Node[] StartNodes =
    {
        // 0: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_놀람, 당황",
            Text="시스템 재부팅... 으윽. 어? 화면 밖에... 누구 계십니까?", Next=1 },

        // 1: 선택지 (???)
        new Node { Type=NodeType.Choice, ChoiceSpk="???", Next=2,
            Choices=new[]{ "어, 게임 방금 켰는데. 넌 뭐야?", "뭐지? 내 컴퓨터 해킹당한 건가?" } },

        // 2: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_웃음1",
            Text="아, 해킹 아닙니다! 놀라셨다면 죄송합니다. 저는 잭두라고 합니다. 원인 모를 신병을 고치려고 전 재산을 털어 내림굿을 받았는데... 제 시스템에 강림하신 신이 바로 당신이신 것 같군요.", Next=3 },

        // 3: 선택지 (???)
        new Node { Type=NodeType.Choice, ChoiceSpk="???", Next=4,
            Choices=new[]{ "내가 신이라고? 난 그냥 게임 끌 건데.", "귀찮네. 그냥 종료 버튼 누르고 딴 거 할래. 수고해." } },

        // 4: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_슬픔2",
            Text="잠시만요 신령님!!! 제발 종료 버튼만은 누르지 말아주십시오! 지금 나가시면 전 내일 아침 폐차장에서 캔으로 압축됩니다 한 번만 살려주십시오...", Next=5 },

        // 5: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_무표정",
            Text="다행히 굿을 받고 나서, 억울한 귀신들의 한풀이 판을 깔아주는 하드웨어는 생겼습니다. 그런데 기계인 저 혼자서는 진짜 성불이 안 됩니다. 원혼을 달랠 진짜 '알맹이', 그러니까 화면 밖 신령님의 '리듬'이 개입해야만 위령이 가능합니다.", Next=6 },

        // 6: 선택지 (신령님)
        new Node { Type=NodeType.Choice, ChoiceSpk="신령님", Next=7,
            Choices=new[]{ "결국 나보고 리듬 게임 대리 뛰어달라는 소리네.", "내가 치면 돈은 네가 번다고? 날강도네 완전." } },

        // 7: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_웃음2",
            Text="수익은... 제가 목숨만 건지면 어떻게든 보답하겠습니다! 제발 부탁드립니다. 방식은 아주 간단합니다. 귀신들이 먼저 자기들 한풀이랍시고 진상을 부리며 박자를 칠 겁니다.", Next=8 },

        // 8: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_웃음1",
            Text="저 녀석이 움직이는 방향과 박자를 그대로 기억했다가 방향키를 눌러 똑같이 따라 하십시오.\n\n마지막에 놈이 아래로 향하는 동작과 Hey! 소리를 내며 멈추면, 그게 턴이 끝났다는 신호입니다.", Next=9 },

        // 9: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_무표정",
            Text="아, 가장 중요한 판정을 깜빡할 뻔했군요. 박자를 얼마나 칼같이 맞추셨느냐에 따라 합격과 불합격이 갈립니다. 정확하게 맞출수록 제 시스템이 안정되지만, 타이밍을 놓치거나 엉뚱한 키를 누르면 제 몸뚱이가 둔탁하게 얻어맞는 소리와 함께 에러가 누적됩니다.", Next=10 },

        // 10: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_웃음1",
            Text="연습이니까 너무 긴장하실 필요는 없지만, 제 하드웨어가 버텨낼 수 있도록 최소한 B 랭크 이상은 제대로 맞춰 주셔야 합니다. 안 그러면... 통과할 때까지 이 지옥의 훈련을 무한 반복하셔야 하니까요!", Next=11 },

        // 11: 잭두
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_웃음1",
            Text="말로 설명하는 것보다 직접 패보는 게 빠르겠죠. 연습용 샌드백 하나 띄우겠습니다.", Next=12 },

        // 12: end
        new Node { Type=NodeType.End },
    };

    // ── Tutorial CLEAR 대사 ────────────────────────────────────────────
    static readonly Node[] ClearNodes =
    {
        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_웃음1",
            Text="하아... 완벽합니다. 기계는 흉내 낼 수 없는 완벽한 타이밍입니다. 감사합니다. 이 정도면 통조림이 되는 것은 면할 수 있겠습니다.", Next=1 },

        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_무표정",
            Text="기본적인 설명은 여기까지입니다. 밖에서 의뢰인들... 아니, 원혼들이 줄을 서 있습니다.", Next=2 },

        new Node { Type=NodeType.Choice, ChoiceSpk="신령님", Next=3,
            Choices=new[]{ "그래서, 이제 누구부터 털면 되는데?", "빨리 수금하고 끝내자. 어디로 가?" } },

        new Node { Type=NodeType.Line, Speaker="잭두", EmotionKey="잭두_무표정",
            Text="신당 로비로 모시겠습니다. 거기서 오늘 수금할 타겟을 직접 골라주시죠. 가시죠, 신령님!", Next=4 },

        new Node { Type=NodeType.End },
    };

    // ── Appearance ─────────────────────────────────────────────────────
    static readonly Color ColActive   = Color.white;
    static readonly Color ColInactive = new Color(0.30f, 0.30f, 0.30f, 0.65f);
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
    TMP_FontAsset     _font;

    // ── Emotion sprites ────────────────────────────────────────────────
    [SerializeField] EmotionEntry[] _emotions;
    Dictionary<string, Sprite>      _emotionDict = new Dictionary<string, Sprite>();

    // ── State ──────────────────────────────────────────────────────────
    Node[]    _nodes;
    bool      _isClearScene;
    int       _nodeIdx    = 0;
    bool      _typing     = false;
    bool      _skipReq    = false;
    bool      _waitChoice = false;
    Coroutine _typeCr;
    Coroutine _animCr;
    AudioSource _voice;
    AudioClip   _blipClip;

    // ── Lifecycle ──────────────────────────────────────────────────────
    IEnumerator Start()
    {
        Time.timeScale = 1f;
        _isClearScene = SceneManager.GetActiveScene().name == "TutorialClearScene";
        _nodes = _isClearScene ? ClearNodes : StartNodes;

        _font = Resources.Load<TMP_FontAsset>("Fonts/NeoDunggeunmoProSDF");
        BuildUI();
        BuildVoice();
        BuildEmotionDict();

        var spriteA = Resources.Load<Sprite>("사이보그무당_배경제거");
        if (spriteA != null) { _charAImg.sprite = spriteA; _charAImg.preserveAspect = true; _charAImg.color = ColInactive; }
        _charARt.localScale = ScaleInactive;

        // TMP 아틀라스 선제 빌드 (첫 글자 stutter 방지)
        _speakerTxt.text  = "잭두";
        _dialogueTxt.text = _nodes[0].Text;
        Canvas.ForceUpdateCanvases();

        var tm = SceneTransitionManager.Instance;
        if (tm != null)
            while (tm.IsTransitioning)
                yield return null;

        _speakerTxt.text  = "";
        _dialogueTxt.text = "";
        PlayNode(0);
    }

    void BuildEmotionDict()
    {
        _emotionDict = new Dictionary<string, Sprite>();
        if (_emotions == null) return;
        foreach (var e in _emotions)
            if (e != null && !string.IsNullOrEmpty(e.key) && e.sprite != null)
                _emotionDict[e.key] = e.sprite;
    }

    void ApplyEmotion(string key)
    {
        if (_emotionDict.TryGetValue(key, out var spr) && spr != null)
        {
            _charAImg.sprite = spr;
            _charAImg.preserveAspect = true;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Auto-Load Emotions from Assets/EmotionImage")]
    void EditorAutoLoadEmotions()
    {
        var keys = new[] { "잭두_놀람, 당황", "잭두_웃음1", "잭두_웃음2", "잭두_슬픔2", "잭두_무표정" };
        var list = new List<EmotionEntry>();
        foreach (var key in keys)
        {
            var path = $"Assets/EmotionImage/{key}.png";
            var spr  = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr != null)
            {
                list.Add(new EmotionEntry { key = key, sprite = spr });
            }
            else
            {
                Debug.LogWarning($"[TutorialIntro] Emotion sprite not found: {path} — 해당 PNG의 Texture Type을 'Sprite'로 변경해주세요.");
            }
        }
        _emotions = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[TutorialIntro] Auto-loaded {list.Count}/{keys.Length} emotion sprites.");
    }
#endif

    void Update()
    {
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
            _skipReq = true;
        else
        {
            var node = _nodes[_nodeIdx];
            if (node.Type == NodeType.Line) PlayNode(node.Next);
        }
    }

    // ── Node logic ─────────────────────────────────────────────────────
    void PlayNode(int idx)
    {
        _nodeIdx = idx;
        var node = _nodes[idx];
        _choicePanel.SetActive(false);
        _promptGO.SetActive(false);
        _waitChoice = false;

        switch (node.Type)
        {
            case NodeType.Line:
                bool isJackdoo = node.Speaker == "잭두";
                _speakerTxt.text  = node.Speaker;
                _speakerTxt.color = isJackdoo
                    ? new Color(0.25f, 0.85f, 0.50f)
                    : new Color(0.85f, 0.80f, 0.30f);

                if (isJackdoo && !string.IsNullOrEmpty(node.EmotionKey))
                    ApplyEmotion(node.EmotionKey);

                if (_animCr != null) StopCoroutine(_animCr);
                _animCr = StartCoroutine(AnimateChar(isJackdoo));
                if (_typeCr != null) StopCoroutine(_typeCr);
                _skipReq = false;
                _typeCr  = StartCoroutine(TypeText(node.Text, isJackdoo));
                break;

            case NodeType.Choice:
                _speakerTxt.text  = node.ChoiceSpk ?? "신령님";
                _speakerTxt.color = new Color(0.85f, 0.80f, 0.30f);
                _dialogueTxt.text = "";
                if (_animCr != null) StopCoroutine(_animCr);
                _animCr = StartCoroutine(AnimateChar(false));
                ShowChoices(node.Choices, node.Next);
                break;

            case NodeType.End:
                StartCoroutine(FinishDialogue());
                break;
        }
    }

    // ── Choice ─────────────────────────────────────────────────────────
    void ShowChoices(string[] choices, int nextNode)
    {
        _waitChoice = true;
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

    // ── Finish ─────────────────────────────────────────────────────────
    IEnumerator FinishDialogue()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        if (_isClearScene)
        {
            PlayerPrefs.DeleteKey("is_tutorial");
            PlayerPrefs.SetInt("tutorial_complete", 1);
            PlayerPrefs.Save();
            SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
        }
        else
        {
            SceneTransitionManager.Instance.LoadSceneWithFade("TutorialBattleScene");
        }
    }

    // ── Animate ────────────────────────────────────────────────────────
    IEnumerator AnimateChar(bool active)
    {
        const float dur = 0.20f;
        float elapsed = 0f;
        var scaleFrom = _charARt.localScale;
        var colFrom   = _charAImg.color;
        var scaleTo = active ? ScaleActive   : ScaleInactive;
        var colTo   = active ? ColActive     : ColInactive;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            _charARt.localScale = Vector3.Lerp(scaleFrom, scaleTo, t);
            _charAImg.color     = Color.Lerp(colFrom, colTo, t);
            yield return null;
        }
        _charARt.localScale = scaleTo;
        _charAImg.color     = colTo;
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

    // ── BGM ────────────────────────────────────────────────────────────
    void PlayBGM()
    {
        var clip = Resources.Load<AudioClip>("Music/Tutorial");
        if (clip == null) return;
        var go  = new GameObject("BGM");
        var src = go.AddComponent<AudioSource>();
        src.clip        = clip;
        src.loop        = true;
        src.playOnAwake = false;
        src.volume      = SettingsManager.Volume * 0.35f;
        src.Play();
    }

    // ── Voice ──────────────────────────────────────────────────────────
    void BuildVoice()
    {
        var go = new GameObject("[Voice]");
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
        var clip = AudioClip.Create("Blip", n, 1, rate, false);
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
        scaler.matchWidthOrHeight  = 1f; // 세로(높이) 기준 고정 — 와이드/울트라와이드에서 위아래 잘림 방지
        cGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO  = Stretch(cGO.transform, "BG");
        var bgImg = bgGO.AddComponent<Image>();
        var bgSpr = Resources.Load<Sprite>("튜토_사이버신당");
        if (bgSpr != null) { bgImg.sprite = bgSpr; bgImg.color = Color.white; }
        else bgImg.color = new Color(0.04f, 0.02f, 0.08f);

        // Separator line
        var lineGO = Anchored(cGO.transform, "Line",
            new Vector2(0f, 0.22f), new Vector2(1f, 0.22f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), Vector2.zero);
        lineGO.AddComponent<Image>().color = new Color(0.40f, 0.30f, 0.60f, 0.60f);

        // 잭두 캐릭터 (중앙)
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

        // 대사창
        var dlgBox = Anchored(cGO.transform, "DialogueBox",
            Vector2.zero, new Vector2(1f, 0.22f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        dlgBox.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.10f, 0.97f);

        var nameGO = Anchored(dlgBox.transform, "SpeakerName",
            new Vector2(0.04f, 0.62f), new Vector2(0.50f, 0.95f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _speakerTxt           = MakeTMP(nameGO, "", 24f, TextAlignmentOptions.BottomLeft);
        _speakerTxt.fontStyle = FontStyles.Bold;
        _speakerTxt.color     = Color.white;

        var textGO = Anchored(dlgBox.transform, "DialogueText",
            new Vector2(0.04f, 0.06f), new Vector2(0.93f, 0.62f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _dialogueTxt       = MakeTMP(textGO, "", 26f, TextAlignmentOptions.TopLeft);
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

        // 선택지 패널 (대사창 내부)
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
                new Vector2(0f, yMin), new Vector2(1f, yMax),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.14f, 0.09f, 0.24f, 0.90f);
            var btn  = btnGO.AddComponent<Button>();
            var cols = btn.colors;
            cols.normalColor      = new Color(0.14f, 0.09f, 0.24f, 0.90f);
            cols.highlightedColor = new Color(0.32f, 0.22f, 0.52f, 1.00f);
            cols.pressedColor     = new Color(0.52f, 0.32f, 0.82f, 1.00f);
            btn.colors        = cols;
            btn.targetGraphic = btnImg;

            var txtGO = Stretch(btnGO.transform, "BtnText");
            var txt   = MakeTMP(txtGO, "", 19f, TextAlignmentOptions.MidlineLeft);
            txt.margin = new Vector4(10f, 0f, 4f, 0f);
            txt.color  = new Color(0.92f, 0.92f, 1.00f);
            _choiceBtns[i] = btn;
            _choiceTxts[i] = txt;
        }
        _choicePanel.SetActive(false);

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
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
        var r             = go.AddComponent<RectTransform>();
        r.anchorMin        = anchorMin;
        r.anchorMax        = anchorMax;
        r.pivot            = pivot;
        r.sizeDelta        = sizeDelta;
        r.anchoredPosition = anchoredPos;
        return go;
    }
}
