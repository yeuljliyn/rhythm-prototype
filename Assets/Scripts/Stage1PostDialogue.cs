using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 판 후 대사 (docs/design.md > 구조 > 서사 전달). Stage1_NScene 안에서 결과창(S/A) 뒤, 그 판의
/// 배경 위에 오버레이로 뜬다. Stage1DialogueSceneManager를 복제해 오버레이용으로 다듬은 것이고
/// 원본은 그대로 둔다(프로젝트 컨벤션). 씬 전환은 여기서 안 하고, 끝나면 onFinished만 부른다.
///
/// 1회 게이팅은 호출부(Stage1ResultScreen)가 HasSeen()으로 판단한다 — 여기서는 완주 시 플래그만
/// 세팅(대사씬들과 같은 규칙: 대상 안에서 게이팅하면 직접 테스트할 때마다 튕긴다).
///
/// 프리팹 Assets/Prefabs/Stage1PostDialoguePanel — Canvas 루트(정렬 100, 결과창 99 위)에 Dim +
/// SafeFrame(CharA/CharB/DialogueBox). 각 Stage1_NScene에 비활성으로 하나씩 놓여 있다. 노드는
/// stageIndex(0-based)로 고르므로 프리팹 인스턴스에 판별 설정은 없다.
/// </summary>
public class Stage1PostDialogue : MonoBehaviour
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
        public int[]     NextByChoice; // Choice 전용: 선택 i → NextByChoice[i]. null이면 Next로 합류
        public string    EmotionKey;
    }

    [Serializable]
    public class EmotionEntry
    {
        public string key;
        public Sprite sprite;
    }

    static readonly string NameA = "잭두";
    static readonly string NameB = "물귀신";

    // ── 판별 노드 (docs/design.md 초안 그대로, 선택지는 냉소/호기심 → 잭두 반응만 갈림) ─────
    static readonly Node[] Stage11 =
    {
        // 남 따라 탄 걸 인정
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_슬픔",
            Text="...그때 다들 손 들길래.", Next=1 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="타셨죠. 다들 탈 때.", Next=2 },
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_분노",
            Text="그래도 안 팔아.", Next=3 },
        new Node { Type=NodeType.End },
    };

    static readonly Node[] Stage12 =
    {
        // 개미를 알아봄
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_일반",
            Text="...쟤들 왜 여기 있어.", Next=1 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="저 양반 따라 들어왔다가 같이 잠긴 것들입니다.\n저 양반 없이는 못 나갑니다.", Next=2 },
        new Node { Type=NodeType.Choice, Next=3, NextByChoice=new[]{ 3, 4 },
            Choices=new[]{ "지 탓이네.", "같이 보낼 수 있어?" } },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="...그렇게 되죠.", Next=5 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="저 양반이 가면 따라갑니다.", Next=5 },
        new Node { Type=NodeType.End },
    };

    static readonly Node[] Stage13 =
    {
        // 존버가 약해짐, 잭두와 겹침
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_슬픔",
            Text="존버는... 승리...", Next=1 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="남 일 같지 않습니다. 저도 다음 달까지 못 넣으면 저 옆자리입니다.\n사람은 신용불량자가 되지만 저는 고철값입니다.", Next=2 },
        new Node { Type=NodeType.Choice, Next=3, NextByChoice=new[]{ 3, 4 },
            Choices=new[]{ "너도 물렸네.", "그래서 더 잘 보내야겠네." } },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="...물렸죠.", Next=5 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="그래서 더 잘 보내드려야 합니다.", Next=5 },
        new Node { Type=NodeType.End },
    };

    static readonly Node[] Stage14 =
    {
        // 분장이 도깨비였음을 앎, "...팔걸"
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_일반",
            Text="이 분장... 내가 한 거 아닌데.", Next=1 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="도깨비입니다. 재물신이죠.\n부자로 만들기도 하고 저 꼴로 만들기도 합니다.", Next=2 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정2_화남",
            Text="좇던 대박이 도깨비불이었던 겁니다.", Next=3 },
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_슬픔",
            Text="...팔걸.", Next=4 },
        new Node { Type=NodeType.End },
    };

    static readonly Node[] Stage15 =
    {
        // 배. 엔딩 훅
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_웃음",
            Text="...개미들은.\n같이 가자.", Next=1 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_웃음1",
            Text="잘 가셨습니다.", Next=2 },
        new Node { Type=NodeType.Choice, Next=3, NextByChoice=new[]{ 3, 4 },
            Choices=new[]{ "그래서 수금은.", "캔은 면했어?" } },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="...정산은 넘겼습니다. 이번 달은.", Next=5 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_웃음1",
            Text="면했습니다. 이번 달은.", Next=5 },
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="다음 손님이 와 계십니다.", Next=6 },
        new Node { Type=NodeType.End },
    };

    static readonly Node[][] NodesByStage = { Stage11, Stage12, Stage13, Stage14, Stage15 };

    // ── 게이팅 (호출부가 씀) ────────────────────────────────────────────
    public static string SeenKey(int stageIndex) => $"stage1_post_dialogue_seen_{stageIndex}";
    public static bool   HasSeen(int stageIndex) => PlayerPrefs.GetInt(SeenKey(stageIndex), 0) == 1;
    public static bool   HasNodes(int stageIndex) =>
        stageIndex >= 0 && stageIndex < NodesByStage.Length && NodesByStage[stageIndex].Length > 0;

    // ── Character appearance — 색 디밍만, localScale은 안 건드림(대사씬과 동일) ───────
    static readonly Color ColActive   = Color.white;
    static readonly Color ColInactive = new Color(0.55f, 0.55f, 0.60f, 0.75f);

    // ── UI refs (프리팹 안에서 배선) ───────────────────────────────────
    [SerializeField] Image             _charAImg, _charBImg;
    [SerializeField] TextMeshProUGUI   _speakerTxt, _dialogueTxt;
    [SerializeField] GameObject        _promptGO;
    [SerializeField] GameObject        _choicePanel;
    [SerializeField] Button[]          _choiceBtns;
    [SerializeField] TextMeshProUGUI[] _choiceTxts;
    [SerializeField] AudioSource       _voice;

    [SerializeField] EmotionEntry[] _emotions;
    Dictionary<string, Sprite>      _emotionDict = new Dictionary<string, Sprite>();

    // 결과창을 넘기려고 누른 Space/클릭이 그대로 첫 대사를 스킵하지 않도록 짧은 유예(실시간).
    const float InputArmDelay = 0.35f;

    // ── State ─────────────────────────────────────────────────────────
    Node[]      _nodes;
    int         _stageIndex  = -1;
    int         _nodeIdx     = 0;
    bool        _playing     = false;
    bool        _typing      = false;
    bool        _skipRequest = false;
    bool        _waitChoice  = false;
    float       _armedAt;
    Action      _onFinished;
    Coroutine   _typeCr;
    Coroutine   _animCr;
    AudioClip   _blipClip;

    /// <summary>판 후 대사 시작. 이 오브젝트를 켜고 노드 0부터 재생. 끝나면 스스로 꺼지고 onFinished 호출.</summary>
    public void Play(int stageIndex, Action onFinished)
    {
        if (!HasNodes(stageIndex)) { onFinished?.Invoke(); return; }

        _stageIndex = stageIndex;
        _nodes      = NodesByStage[stageIndex];
        _onFinished = onFinished;
        _playing    = true;
        _armedAt    = Time.realtimeSinceStartup + InputArmDelay;

        if (_blipClip == null) _blipClip = GenerateBlip();
        BuildEmotionDict();

        gameObject.SetActive(true);
        if (_charAImg != null) { _charAImg.preserveAspect = true; _charAImg.color = ColInactive; }
        if (_charBImg != null) { _charBImg.preserveAspect = true; _charBImg.color = ColInactive; }
        _choicePanel.SetActive(false);
        _promptGO.SetActive(false);
        _dialogueTxt.text = "";

        PlayNode(0);
    }

    void Update()
    {
        if (!_playing) return;
        if (Time.realtimeSinceStartup < _armedAt) return;

        var kb = Keyboard.current;
        if (_waitChoice)
        {
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                    SelectChoice(0);
                else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                    SelectChoice(1);
            }
            return;
        }

        bool advance = false;
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
            var node = _nodes[_nodeIdx];
            if (node.Type == NodeType.Line) PlayNode(node.Next);
        }
    }

    // ── Node logic ────────────────────────────────────────────────────
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
                ShowChoices(node);
                break;

            case NodeType.End:
                StartCoroutine(FinishDialogue());
                break;
        }
    }

    void ShowChoices(Node node)
    {
        string[] choices = node.Choices;
        _waitChoice = true;
        _choicePanel.SetActive(true);
        string[] prefixes = { "① ", "② " };
        for (int i = 0; i < _choiceBtns.Length && i < choices.Length; i++)
        {
            _choiceTxts[i].text = prefixes[i] + choices[i];
            _choiceBtns[i].gameObject.SetActive(i < choices.Length);
            int n = (node.NextByChoice != null && i < node.NextByChoice.Length) ? node.NextByChoice[i] : node.Next;
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

    IEnumerator FinishDialogue()
    {
        _playing = false;
        PlayerPrefs.SetInt(SeenKey(_stageIndex), 1);
        PlayerPrefs.Save();

        yield return new WaitForSecondsRealtime(0.4f);
        gameObject.SetActive(false);
        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
    }

    // ── Emotion ───────────────────────────────────────────────────────
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

    // ── Animate chars (unscaled — 결과창이 timeScale 0으로 둔 채 뜸) ────────────────
    IEnumerator AnimateChars(bool aActive)
    {
        const float duration = 0.20f;
        float elapsed = 0f;
        Color aColFrom = _charAImg.color;
        Color bColFrom = _charBImg.color;
        Color aColTo = aActive ? ColActive   : ColInactive;
        Color bColTo = aActive ? ColInactive : ColActive;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _charAImg.color = Color.Lerp(aColFrom, aColTo, t);
            _charBImg.color = Color.Lerp(bColFrom, bColTo, t);
            yield return null;
        }
        _charAImg.color = aColTo;
        _charBImg.color = bColTo;
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

    // ── Voice (Stage1DialogueSceneManager와 동일) ───────────────────────
    void PlayBlip(char c, bool isA)
    {
        if (_voice == null) return;
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

#if UNITY_EDITOR
    // 판 후 대사에서 쓰는 표정 전부. 물귀신 '일반/웃음'은 대사씬1_광대_* 아트를 그대로 씀.
    static readonly Dictionary<string, string> EmotionKeyToPath = new Dictionary<string, string>
    {
        { "잭두_무표정",       "Assets/대사씬시작/대사씬0_잭두_무표정.png" },
        { "잭두_무표정2_화남",  "Assets/대사씬시작/대사씬0_잭두_무표정2화남.png" },
        { "잭두_놀람_당황",     "Assets/대사씬시작/대사씬0_잭두_놀람당황.png" },
        { "잭두_슬픔2",        "Assets/대사씬시작/대사씬0_잭두_슬픔2.png" },
        { "잭두_웃음1",        "Assets/대사씬시작/대사씬0_잭두_웃음1.png" },
        { "물귀신_슬픔",       "Assets/대사씬시작/대사씬1_광대_슬픔.png" },
        { "물귀신_분노",       "Assets/대사씬시작/대사씬1_광대_화남.png" },
        { "물귀신_일반",       "Assets/대사씬시작/대사씬1_광대_일반.png" },
        { "물귀신_웃음",       "Assets/대사씬시작/대사씬1_광대_웃음.png" },
    };

    [ContextMenu("Auto-Load Emotions from Assets/대사씬시작")]
    public void EditorAutoLoadEmotions()
    {
        var list = new List<EmotionEntry>();
        foreach (var kv in EmotionKeyToPath)
        {
            var spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(kv.Value);
            if (spr == null)
                foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(kv.Value))
                    if (o is Sprite s) { spr = s; break; }
            if (spr != null)
                list.Add(new EmotionEntry { key = kv.Key, sprite = spr });
            else
                Debug.LogWarning($"[Stage1PostDialogue] sprite not found: {kv.Key} → {kv.Value}");
        }
        _emotions = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[Stage1PostDialogue] Auto-loaded {list.Count}/{EmotionKeyToPath.Count} emotion sprites.");
    }

    [ContextMenu("Reset 판 후 대사 seen flags (1-1~1-5)")]
    void EditorResetSeenFlags()
    {
        for (int i = 0; i < NodesByStage.Length; i++) PlayerPrefs.DeleteKey(SeenKey(i));
        PlayerPrefs.Save();
        Debug.Log("[Stage1PostDialogue] stage1_post_dialogue_seen_* 초기화 완료");
    }
#endif
}
