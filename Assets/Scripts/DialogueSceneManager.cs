using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// §0. 오프닝 — 사이버 신당 (잭두-신령님 첫 만남). StartScene 직후, 게임을 통틀어 딱 한 번만
/// 재생됨 — PlayerPrefs "opening_seen" 플래그로 게이팅(StartSceneManager.OnStart()이 이 플래그를
/// 보고 아예 이 씬을 스킵하고 바로 SurfaceLobbyScene으로 감; 여기서는 완주했을 때 그 플래그를
/// 세팅만 함 — Stage1TutorialGate의 "stage1_tutorial_seen"과 동일한 패턴).
///
/// UI는 Stage1DialogueSceneManager와 완전히 동일한 필드 레이아웃(에디터에서 직접 배치 — Play 없이도
/// Hierarchy에서 보임, BuildUI()로 런타임 절차 생성하던 예전 방식 폐기)을 그대로 따름 — 실제로 이
/// 씬(Assets/Scenes/DialogueScene.unity)의 Hierarchy 자체가 Stage1DialogueScene.unity를 복제해서
/// 만든 것이라, 필드 이름을 이 스크립트와 똑같이 맞춰두면 기존 오브젝트 참조가 그대로 유지됨.
///
/// 이 오프닝엔 신령님이 화면 밖 존재로만 등장(초상화 없음, 대사도 전부 선택지로만 표현)해서
/// CharB(물귀신 자리였던 초상화 슬롯)를 아예 숨김 — 이 씬에서는 안 씀.
/// </summary>
public class DialogueSceneManager : MonoBehaviour
{
    // ── Node types ─────────────────────────────────────────────────────
    enum NodeType { Line, Choice, End }
    enum Speaker  { A, B } // A = 잭두(좌). B(물귀신)는 이 씬에서 안 씀 — CharB는 숨김.

    class Node
    {
        public NodeType  Type;
        public Speaker   Spk;
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

    static readonly string NameA = "잭두";

    static readonly Node[] Nodes = new Node[]
    {
        // 0: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_놀람_당황",
            Text="어. 어어.\n화면 밖에... 누구 계십니까?", Next=1 },
        // 1: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=2,
            Choices=new[]{ "누구긴. 방금 게임 켰는데.", "내 컴퓨터가 왜 말을 걸지." } },
        // 2: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_웃음1",
            Text="오셨다. 진짜 오셨네.", Next=3 },
        // 3: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="저는 잭두입니다. 무당이고요.\n보시다시피 기계입니다.", Next=4 },
        // 4: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=5,
            Choices=new[]{ "기계가 무당을 해?", "고장 난 거 아니고?" } },
        // 5: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="원인 모를 신병이 왔습니다. 병원에서는 모른답니다.\n내림굿을 받으면 낫는다길래 받았습니다.", Next=6 },
        // 6: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="전 재산을 털었고, 모자란 건 긁었습니다.\n그랬더니 신은 안 오고 할부만 남았습니다.", Next=7 },
        // 7: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=8,
            Choices=new[]{ "그래서 나한테 뭘 어쩌라고.", "할부는 네 사정이지." } },
        // 8: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="제 사정이 곧 목숨입니다.\n사람은 못 갚으면 신용불량자가 되지만, 저는 <b>몸을 뜯어갑니다.</b>", Next=9 },
        // 9: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정2_화남",
            Text="기계는 고철값으로 계산되거든요.\n다음 달까지 못 넣으면 압류돼서 폐차장으로 갑니다. <b>캔으로 압축되는 겁니다.</b>", Next=10 },
        // 10: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=11,
            Choices=new[]{ "그래서 굿을 한다고?", "그거랑 나랑 무슨 상관인데." } },
        // 11: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_웃음2",
            Text="굿판을 벌일 겁니다. 원혼 달래드리고 값을 받으면 되죠.\n손님은 많습니다. 요즘 세상에 한 맺힌 분이 얼마나 많습니까.", Next=12 },
        // 12: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="그런데 저 혼자서는 굿이 안 됩니다.\n굿은 장단입니다. 신령님이 박자를 넣어주셔야 합니다.", Next=13 },
        // 13: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=14,
            Choices=new[]{ "치는 건 나고 수금은 너야?", "결국 내가 대신 친다는 거네." } },
        // 14: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="정산은... 제가 캔이 안 되면 어떻게든 하겠습니다.\n마침 밖에 첫 손님이 와 계십니다. 가시죠.", Next=15 },
        // 15: end
        new Node { Type=NodeType.End },
    };

    // ── Character appearance ──────────────────────────────────────────
    static readonly Color ColActive   = Color.white;
    static readonly Color ColInactive = new Color(0.30f, 0.30f, 0.30f, 0.65f);
    static readonly Vector3 ScaleActive   = new Vector3(1.10f, 1.10f, 1f);
    static readonly Vector3 ScaleInactive = new Vector3(0.93f, 0.93f, 1f);

    // ── UI refs (assigned in Editor — see Assets/Scenes/DialogueScene.unity; field names match
    // Stage1DialogueSceneManager on purpose, see class doc) ──
    [SerializeField] RectTransform     _charARt, _charBRt;
    [SerializeField] Image             _charAImg, _charBImg;
    [SerializeField] Image             _bgImg;
    [SerializeField] TextMeshProUGUI   _speakerTxt, _dialogueTxt;
    [SerializeField] GameObject        _promptGO;
    [SerializeField] GameObject        _choicePanel;
    [SerializeField] Button[]          _choiceBtns;
    [SerializeField] TextMeshProUGUI[] _choiceTxts;
    [SerializeField] AudioSource       _voice;

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
    AudioClip   _blipClip;

    // ── Lifecycle ─────────────────────────────────────────────────────
    // "최초 1회만" 게이팅은 여기서 하지 않음 — StartSceneManager.OnStart()가 opening_seen을 보고
    // 애초에 이 씬을 로드할지 말지 결정하는 게 유일한 진입 경로라, 여기서도 같은 체크를
    // 반복하면 씬을 직접 열어 Play로 테스트/수정할 때(예: 오브젝트 위치 조정 후 확인) 이미 세팅된
    // 플래그 때문에 매번 대사 없이 바로 지상 로비로 튕겨버리는 문제가 있었음(실제로 겪음). 대사가
    // 끝났을 때 FinishDialogue()에서 플래그를 세팅하는 것만으로 충분함.
    IEnumerator Start()
    {
        Time.timeScale = 1f;
        _blipClip = GenerateBlip();
        BuildEmotionDict();

        var spriteA = Resources.Load<Sprite>("사이보그무당_배경제거");
        if (spriteA != null) { _charAImg.sprite = spriteA; _charAImg.preserveAspect = true; }

        var bgSprite = Resources.Load<Sprite>("튜토_사이버신당");
        if (_bgImg != null && bgSprite != null) { _bgImg.sprite = bgSprite; _bgImg.color = Color.white; }

        if (_charBRt != null) _charBRt.gameObject.SetActive(false); // 신령님은 화면 밖 존재 — 초상화 없음

        _charARt.localScale = ScaleActive; // 이 씬은 잭두 혼자라 처음부터 활성 상태로
        _charAImg.color     = ColActive;

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

#if UNITY_EDITOR
    // 표정 키 → 실제 파일 경로 매핑 (파일명과 키 이름이 다른 경우 처리) — 이 씬(§0)에서 쓰는
    // 잭두 표정만. Stage1DialogueSceneManager와 같은 원본 이미지를 그대로 재사용.
    static readonly Dictionary<string, string> EmotionKeyToPath = new Dictionary<string, string>
    {
        { "잭두_놀람_당황",     "Assets/EmotionImage/잭두_놀람, 당황.png" },
        { "잭두_웃음1",        "Assets/EmotionImage/잭두_웃음1.png" },
        { "잭두_웃음2",        "Assets/EmotionImage/잭두_웃음2.png" },
        { "잭두_슬픔2",        "Assets/EmotionImage/잭두_슬픔2.png" },
        { "잭두_무표정",       "Assets/EmotionImage/잭두_무표정.png" },
        { "잭두_무표정2_화남",  "Assets/EmotionImage/잭두_무표정2, 화남.png" },
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
                Debug.LogWarning($"[DialogueScene] Emotion sprite not found: {kv.Value}");
            }
        }
        _emotions = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[DialogueScene] Auto-loaded {list.Count}/{EmotionKeyToPath.Count} emotion sprites.");
    }
#endif

    // StartSceneManager에도 같은 키를 지우는 컨텍스트 메뉴가 있지만, 그건 StartScene에서만 쓸 수
    // 있음 — 이 씬을 직접 열어 Play로 테스트할 때 바로 초기화할 수 있도록 여기도 추가.
    [ContextMenu("오프닝 대사 초기화(opening_seen 지우기)")]
    void ResetOpeningSeen()
    {
        PlayerPrefs.DeleteKey("opening_seen");
        PlayerPrefs.Save();
        Debug.Log("[DialogueScene] opening_seen 초기화 완료 — 다음 Play부터 오프닝 대사 다시 재생됨");
    }

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

        switch (node.Type)
        {
            case NodeType.Line:
                bool isA = node.Spk == Speaker.A;
                _speakerTxt.text  = NameA;
                _speakerTxt.color = new Color(0.25f, 0.85f, 0.50f);

                if (!string.IsNullOrEmpty(node.EmotionKey))
                    ApplyEmotion(node.EmotionKey, isA);

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
        PlayerPrefs.SetInt("opening_seen", 1);
        PlayerPrefs.Save();
        yield return new WaitForSecondsRealtime(0.6f);
        SceneTransitionManager.Instance.LoadSceneWithFade("SurfaceLobbyScene");
    }

    // ── Voice ─────────────────────────────────────────────────────────
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
}
