using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Dialogue scene leading into Stage1Scene (물타기) — §0 오프닝(사이버 신당) + §1 물귀신 등장.
/// Forked from DialogueSceneManager (kept untouched) rather than edited in place, since the
/// original still leads into BattleScene and may be reverted to independently of this one.
/// </summary>
public class Stage1DialogueSceneManager : MonoBehaviour
{
    // ── Node types ─────────────────────────────────────────────────────
    enum NodeType { Line, Choice, End }
    enum Speaker  { A, B } // A = 잭두(좌), B = 물귀신(우)

    class Node
    {
        public NodeType  Type;
        public Speaker   Spk;
        public string    Text;
        public string[]  Choices;
        public int       Next;
        public string    EmotionKey;
        public string    BgKey; // non-null = swap background to this Resources sprite key on this node
    }

    [System.Serializable]
    public class EmotionEntry
    {
        public string key;
        public Sprite sprite;
    }

    static readonly string NameA = "잭두";
    static readonly string NameB = "물귀신";

    static readonly Node[] Nodes = new Node[]
    {
        // ── §0. 오프닝 — 사이버 신당 ──────────────────────────────────
        // 0: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_놀람_당황", BgKey="튜토_사이버신당",
            Text="시스템 재부팅... 으윽. 어? 화면 밖에... 누구 계십니까?", Next=1 },
        // 1: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=2,
            Choices=new[]{ "게임 방금 켰는데. 넌 뭐야?", "뭐지, 내 컴퓨터 해킹당한 건가?" } },
        // 2: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_웃음1",
            Text="아, 해킹 아닙니다! 저는 잭두, 하드웨어 무당입니다. 원인 모를 신병 고쳐보겠다고 전 재산 털어 내림굿을 받았더니— 글쎄, 화면 밖 신령님이 제 시스템에 강림하셨지 뭡니까.", Next=3 },
        // 3: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=4,
            Choices=new[]{ "내가 신이라고? 난 그냥 게임 끌 건데.", "귀찮네. 종료 버튼 누르고 딴 거 할래." } },
        // 4: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="잠시만요 신령님!!! 종료만은 안 됩니다! 지금 나가시면 전 내일 아침 폐차장에서 캔으로 압축됩니다...", Next=5 },
        // 5: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="기계인 저 혼자선 진짜 성불이 안 됩니다. 원혼을 달랠 '알맹이', 그러니까 신령님의 '리듬'이 개입해야만 위령이 됩니다.", Next=6 },
        // 6: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=7,
            Choices=new[]{ "결국 리듬게임 대리 뛰라는 소리네.", "내가 치고 돈은 네가 벌어? 날강도네 완전." } },
        // 7: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_웃음2",
            Text="수익은... 제가 목숨만 건지면 어떻게든 보답하겠습니다! 방식은 백문이 불여일'타'(打), 부딪혀보며 익히는 게 빠릅니다. 마침 밖에 원혼들이 줄을 섰군요. 첫 손님부터 만나시죠. 가시죠, 신령님!", Next=8 },

        // ── §1. 스테이지 1 — 물귀신 등장/인트로 ─────────────────────────
        // 8: 물귀신
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_슬픔", BgKey="스테1_폐허사이버증권거래소",
            Text="저기요... 9만 층에 사람 있어요...! 구조대 언제 오나요...? 네..? 돔황챠? 이미 한강 물이 턱밑인데 어딜 도망쳐어어!!", Next=9 },
        // 9: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정2_화남",
            Text="첫 번째 타겟입니다. 야수의 심장으로 풀매수했다가 상장폐지 빔을 정통으로 맞고 원혼이 되어버린 놈이죠. 오르내리는 세상 이치를 빨간불·파란불로만 자르려다 저 꼴이 난 겁니다.", Next=10 },
        // 10: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=11,
            Choices=new[]{ "남 일 같지가 않네... 나도 뼈 맞고 게임 끄고 싶다.", "저렇게 우는데 굳이 때려야 해?" } },
        // 11: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_분노",
            Text="신령님의 동정심은 제 마이너스 통장 이자를 1원도 안 깎아줍니다! 자비 없는 엇박자로 저 미련한 영혼을 물리적으로 구제해 주십시오!", Next=12 },
        // 12: end
        new Node { Type=NodeType.End },
    };

    // ── Character appearance ──────────────────────────────────────────
    static readonly Color ColActive   = Color.white;
    static readonly Color ColInactive = new Color(0.30f, 0.30f, 0.30f, 0.65f);
    static readonly Vector3 ScaleActive   = new Vector3(1.10f, 1.10f, 1f);
    static readonly Vector3 ScaleInactive = new Vector3(0.93f, 0.93f, 1f);

    // ── UI refs ───────────────────────────────────────────────────────
    RectTransform     _charARt, _charBRt;
    Image             _charAImg, _charBImg;
    Image             _bgImg;
    TextMeshProUGUI   _speakerTxt, _dialogueTxt;
    GameObject        _promptGO;
    GameObject        _choicePanel;
    Button[]          _choiceBtns;
    TextMeshProUGUI[] _choiceTxts;

    // ── Emotion sprites ───────────────────────────────────────────────
    [SerializeField] EmotionEntry[] _emotions;
    Dictionary<string, Sprite>      _emotionDict = new Dictionary<string, Sprite>();

    // ── State ─────────────────────────────────────────────────────────
    int         _nodeIdx     = 0;
    bool        _typing      = false;
    bool        _skipRequest = false;
    bool        _waitChoice  = false;
    Coroutine   _typeCr;
    Coroutine   _animCr;
    AudioSource _voice;
    AudioClip   _blipClip;
    TMP_FontAsset _font;

    // ── Lifecycle ─────────────────────────────────────────────────────
    IEnumerator Start()
    {
        Time.timeScale = 1f;
        _font = Resources.Load<TMP_FontAsset>("Fonts/NeoDunggeunmoProSDF");
        BuildUI();
        BuildVoice();
        BuildEmotionDict();

        var spriteA = Resources.Load<Sprite>("사이보그무당_배경제거");
        if (spriteA != null) { _charAImg.sprite = spriteA; _charAImg.preserveAspect = true; }

        var spriteB = Resources.Load<Sprite>("광대물귀신_배경제거");
        if (spriteB != null) { _charBImg.sprite = spriteB; _charBImg.preserveAspect = true; }

        if (!string.IsNullOrEmpty(Nodes[0].BgKey)) ApplyBg(Nodes[0].BgKey);

        _charARt.localScale = ScaleInactive;
        _charBRt.localScale = ScaleInactive;
        _charAImg.color     = ColInactive;
        _charBImg.color     = ColInactive;

        // TMP 폰트 아틀라스 선제 빌드 (첫 글자 stutter 방지)
        _speakerTxt.text  = NameA;
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

    void BuildEmotionDict()
    {
        _emotionDict = new Dictionary<string, Sprite>();
        if (_emotions == null) return;
        foreach (var e in _emotions)
            if (e != null && !string.IsNullOrEmpty(e.key) && e.sprite != null)
                _emotionDict[e.key] = e.sprite;
    }

    void ApplyEmotion(string key, bool isA)
    {
        if (_emotionDict.TryGetValue(key, out var spr) && spr != null)
        {
            if (isA) { _charAImg.sprite = spr; _charAImg.preserveAspect = true; }
            else     { _charBImg.sprite = spr; _charBImg.preserveAspect = true; }
        }
    }

    void ApplyBg(string resourceKey)
    {
        var spr = Resources.Load<Sprite>(resourceKey);
        if (spr != null) { _bgImg.sprite = spr; _bgImg.color = Color.white; }
    }

#if UNITY_EDITOR
    // 표정 키 → 실제 파일 경로 매핑 (파일명과 키 이름이 다른 경우 처리)
    static readonly Dictionary<string, string> EmotionKeyToPath = new Dictionary<string, string>
    {
        { "잭두_놀람_당황",     "Assets/EmotionImage/잭두_놀람, 당황.png" },
        { "잭두_웃음1",        "Assets/EmotionImage/잭두_웃음1.png" },
        { "잭두_웃음2",        "Assets/EmotionImage/잭두_웃음2.png" },
        { "잭두_슬픔2",        "Assets/EmotionImage/잭두_슬픔2.png" },
        { "잭두_무표정",       "Assets/EmotionImage/잭두_무표정.png" },
        { "잭두_분노",         "Assets/EmotionImage/잭두_분노.png" },
        { "잭두_무표정2_화남",  "Assets/EmotionImage/잭두_무표정2, 화남.png" },
        { "물귀신_슬픔",       "Assets/EmotionImage/광대물귀신_슬픔.png" },
        { "물귀신_분노",       "Assets/EmotionImage/광대물귀신_분노.png" },
    };

    [ContextMenu("Auto-Load Emotions from Assets/EmotionImage")]
    void EditorAutoLoadEmotions()
    {
        var list = new List<EmotionEntry>();
        foreach (var kv in EmotionKeyToPath)
        {
            var spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(kv.Value);
            if (spr != null)
            {
                list.Add(new EmotionEntry { key = kv.Key, sprite = spr });
            }
            else
            {
                Debug.LogWarning($"[Stage1DialogueScene] Emotion sprite not found: {kv.Value}");
            }
        }
        _emotions = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[Stage1DialogueScene] Auto-loaded {list.Count}/{EmotionKeyToPath.Count} emotion sprites.");
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
            _skipRequest = true;
        else
        {
            var node = Nodes[_nodeIdx];
            if (node.Type == NodeType.Line) PlayNode(node.Next);
        }
    }

    // ── Node logic ────────────────────────────────────────────────────
    void PlayNode(int idx)
    {
        _nodeIdx = idx;
        var node = Nodes[idx];
        _choicePanel.SetActive(false);
        _promptGO.SetActive(false);
        _waitChoice = false;

        if (!string.IsNullOrEmpty(node.BgKey)) ApplyBg(node.BgKey);

        switch (node.Type)
        {
            case NodeType.Line:
                bool isA = node.Spk == Speaker.A;
                _speakerTxt.text  = isA ? NameA : NameB;
                _speakerTxt.color = isA
                    ? new Color(0.25f, 0.85f, 0.50f)
                    : new Color(0.80f, 0.40f, 1.00f);

                if (!string.IsNullOrEmpty(node.EmotionKey))
                    ApplyEmotion(node.EmotionKey, isA);

                if (_animCr != null) StopCoroutine(_animCr);
                _animCr = StartCoroutine(AnimateChars(isA));

                if (_typeCr != null) StopCoroutine(_typeCr);
                _skipRequest = false;
                _typeCr = StartCoroutine(TypeText(node.Text, isA));
                break;

            case NodeType.Choice:
                _speakerTxt.text  = "신령님";
                _speakerTxt.color = new Color(0.85f, 0.80f, 0.30f);
                _dialogueTxt.text = "";
                ShowChoices(node.Choices, node.Next);
                break;

            case NodeType.End:
                StartCoroutine(FinishDialogue());
                break;
        }
    }

    // ── Choice ────────────────────────────────────────────────────────
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

    // ── Animate chars ─────────────────────────────────────────────────
    IEnumerator AnimateChars(bool aActive)
    {
        const float duration = 0.20f;
        float elapsed = 0f;
        var aScaleFrom = _charARt.localScale;
        var bScaleFrom = _charBRt.localScale;
        Color aColFrom = _charAImg.color;
        Color bColFrom = _charBImg.color;
        var aScaleTo = aActive ? ScaleActive   : ScaleInactive;
        var bScaleTo = aActive ? ScaleInactive : ScaleActive;
        Color aColTo = aActive ? ColActive   : ColInactive;
        Color bColTo = aActive ? ColInactive : ColActive;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _charARt.localScale = Vector3.Lerp(aScaleFrom, aScaleTo, t);
            _charBRt.localScale = Vector3.Lerp(bScaleFrom, bScaleTo, t);
            _charAImg.color     = Color.Lerp(aColFrom, aColTo, t);
            _charBImg.color     = Color.Lerp(bColFrom, bColTo, t);
            yield return null;
        }
        _charARt.localScale = aScaleTo;
        _charBRt.localScale = bScaleTo;
        _charAImg.color     = aColTo;
        _charBImg.color     = bColTo;
    }

    // ── Type text ─────────────────────────────────────────────────────
    IEnumerator TypeText(string text, bool isA)
    {
        _typing           = true;
        _dialogueTxt.text = "";
        const float delay = 0.032f;
        foreach (char c in text)
        {
            if (_skipRequest) { _dialogueTxt.text = text; break; }
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

    IEnumerator FinishDialogue()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        SceneTransitionManager.Instance.LoadSceneWithFade("Stage1Scene");
    }

    // ── Voice ─────────────────────────────────────────────────────────
    void BuildVoice()
    {
        var go = new GameObject("[DialogueVoice]");
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

    // ── UI Builder ────────────────────────────────────────────────────
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
        var bgGO = Stretch(cGO.transform, "BG");
        _bgImg   = bgGO.AddComponent<Image>();
        _bgImg.color = new Color(0.04f, 0.02f, 0.08f);

        // Separator line
        var lineGO = Anchored(cGO.transform, "Line",
            new Vector2(0f, 0.22f), new Vector2(1f, 0.22f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), Vector2.zero);
        lineGO.AddComponent<Image>().color = new Color(0.40f, 0.30f, 0.60f, 0.60f);

        // CharA (left — 잭두)
        var aGO  = Anchored(cGO.transform, "CharA",
            new Vector2(0.06f, 0.20f), new Vector2(0.38f, 0.98f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        _charARt  = aGO.GetComponent<RectTransform>();
        _charAImg = aGO.AddComponent<Image>();

        var aLblGO = Anchored(aGO.transform, "NameA",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 1f), new Vector2(0f, 36f), new Vector2(0f, -4f));
        var aLbl = MakeTMP(aLblGO, NameA, 18f, TextAlignmentOptions.Center);
        aLbl.color = new Color(0.55f, 1.00f, 0.65f, 0.80f);

        // CharB (right — 물귀신)
        var bGO  = Anchored(cGO.transform, "CharB",
            new Vector2(0.62f, 0.20f), new Vector2(0.94f, 0.98f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        _charBRt  = bGO.GetComponent<RectTransform>();
        _charBImg = bGO.AddComponent<Image>();

        var bLblGO = Anchored(bGO.transform, "NameB",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 1f), new Vector2(0f, 36f), new Vector2(0f, -4f));
        var bLbl = MakeTMP(bLblGO, NameB, 18f, TextAlignmentOptions.Center);
        bLbl.color = new Color(0.90f, 0.50f, 1.00f, 0.80f);

        // Dialogue box
        var dlgBox = Anchored(cGO.transform, "DialogueBox",
            Vector2.zero, new Vector2(1f, 0.22f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        dlgBox.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.10f, 0.97f);

        // Speaker name
        var nameGO = Anchored(dlgBox.transform, "SpeakerName",
            new Vector2(0.04f, 0.62f), new Vector2(0.50f, 0.95f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _speakerTxt           = MakeTMP(nameGO, "", 24f, TextAlignmentOptions.BottomLeft);
        _speakerTxt.fontStyle = FontStyles.Bold;
        _speakerTxt.color     = Color.white;

        // Dialogue text
        var textGO = Anchored(dlgBox.transform, "DialogueText",
            new Vector2(0.04f, 0.06f), new Vector2(0.93f, 0.62f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _dialogueTxt       = MakeTMP(textGO, "", 26f, TextAlignmentOptions.TopLeft);
        _dialogueTxt.color = new Color(0.92f, 0.92f, 1.00f);

        // Prompt ▶
        _promptGO = Anchored(dlgBox.transform, "Prompt",
            new Vector2(0.93f, 0.06f), new Vector2(1.00f, 0.50f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var promptTxt = MakeTMP(_promptGO, "▶", 28f, TextAlignmentOptions.Center);
        promptTxt.color = new Color(0.75f, 0.75f, 1f, 0.9f);
        _promptGO.SetActive(false);

        // Skip hint
        var skipGO  = Anchored(dlgBox.transform, "SkipHint",
            new Vector2(0.04f, 0f), new Vector2(0.5f, 0.10f),
            new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
        var skipTxt = MakeTMP(skipGO, "CLICK  /  SPACE  to continue", 13f, TextAlignmentOptions.BottomLeft);
        skipTxt.color = new Color(0.45f, 0.45f, 0.65f, 0.80f);

        // Choice panel (대사창 안 선택지)
        _choicePanel = Anchored(dlgBox.transform, "ChoicePanel",
            new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.60f),
            new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);

        _choiceBtns = new Button[2];
        _choiceTxts = new TextMeshProUGUI[2];
        for (int i = 0; i < 2; i++)
        {
            float yMin = i == 0 ? 0.52f : 0.02f;
            float yMax = i == 0 ? 0.98f : 0.48f;
            var btnGO  = Anchored(_choicePanel.transform, $"Choice{i}",
                new Vector2(0f, yMin), new Vector2(1f, yMax),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.14f, 0.09f, 0.24f, 0.90f);
            var btn    = btnGO.AddComponent<Button>();
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

        // EventSystem (마우스 클릭 지원)
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

    // ── UI helpers ────────────────────────────────────────────────────
    static GameObject Stretch(Transform parent, string name)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r    = go.AddComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject Anchored(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos)
    {
        var go   = new GameObject(name);
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
