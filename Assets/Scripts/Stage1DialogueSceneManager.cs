using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// §1. 스테이지1 인트로 — 물귀신 등장. 지상 로비에서 스테이지1을 선택해 지하로 내려간 뒤 재생되는
/// 대사(현재는 씬 전환의 페이드아웃/인만 있고, 별도의 "내려가는 연출"은 아직 없음 — 나중에 붙는
/// 대로 이 씬 앞이나 SurfaceLobbyScene 쪽에 추가될 예정). 대사가 끝나면 UndergroundLobbyScene
/// (1-1~1-5 스테이지 선택 로비, 아직 플레이스홀더)으로 전환됨 — Stage1Scene(1-1 실제 플레이)으로
/// 바로 가는 게 아니라 그 사이에 지하 로비가 있음. 예전엔 §0 오프닝(사이버 신당)도 이 씬 앞부분에
/// 같이 있었는데, 오프닝은 게임 전체에서 딱 한 번만 나오는 것으로 분리되어 이제 DialogueScene
/// (원본 DialogueSceneManager를 이 용도로 개조함)에서 담당 — 이 씬은 §1만 남음.
///
/// Forked from DialogueSceneManager (kept untouched) rather than edited in place, since the
/// original still leads into BattleScene and may be reverted to independently of this one.
/// (그 DialogueScene 원본은 이후 오프닝 대사 전용으로 개조됐음 — 위 참고.)
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
        // ── §1. 스테이지 1 — 물귀신 등장/인트로 ─────────────────────────
        // (무대: 물이 목까지 찬 곳, 개미들이 우글거림 — 배경/캐릭터로 이미 표현되어 별도 대사 노드
        // 없음)
        // 0: 물귀신
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_슬픔", BgKey="스테1_폐허사이버증권거래소",
            Text="9만 층이요... 사람 있어요...\n구조대... 아직인가요...", Next=1 },
        // 1: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=2,
            Choices=new[]{ "9만 층?", "무슨 소리야 저게." } },
        // 2: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정2_화남",
            Text="주식입니다. 9만 원에 사서 못 팔았다는 뜻이죠.\n물렸다고 합니다. 그러다 진짜 물에 빠지셨고요.", Next=3 },
        // 3: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=4,
            Choices=new[]{ "밑에 우글거리는 건 뭐야.", "저 벌레들은." } },
        // 4: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="개미입니다. 저 양반 따라 들어갔다가 같이 잠긴 것들이죠.\n이제 저 양반 없이는 못 나갑니다.", Next=5 },
        // 5: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=6,
            Choices=new[]{ "분장한 건 뭔데.", "광대 같은데 저거." } },
        // 6: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_놀람_당황",
            Text="...광대 분장이군요.\n<b>영감입니다.</b> 도깨비요.", Next=7 },
        // 7: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=8,
            Choices=new[]{ "도깨비가 왜 붙어.", "귀신 아니고?" } },
        // 8: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="도깨비는 재물신입니다. 부자로 만들어주기도 하고, 저 꼴로 만들기도 하죠.\n저 양반이 좇던 대박이 저겁니다. <b>도깨비불이었던 거예요.</b>", Next=9 },
        // 9: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=10,
            Choices=new[]{ "너랑 비슷하네.", "돈 때문에 저렇게 된 거잖아." } },
        // 10: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_슬픔2",
            Text="...남 일 같지 않습니다.\n저도 다음 달까지 못 넣으면 저 옆자리입니다.", Next=11 },
        // 11: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="그래서 더 잘 보내드려야 합니다.", Next=12 },
        // 12: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=13,
            Choices=new[]{ "어떻게 보내는데.", "때려서 쫓아내?" } },
        // 13: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="<b>영감이면 영감놀이입니다.</b> 놀아드리고 보내는 굿이죠.\n쫓으면 화내고 돌아옵니다. 실컷 놀아드려야 갑니다.", Next=14 },
        // 14: 선택지 (신령님)
        new Node { Type=NodeType.Choice, Next=15,
            Choices=new[]{ "이기는 게 아니라?", "그게 무슨 굿이야." } },
        // 15: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_무표정",
            Text="이기면 안 됩니다. 지는 것도 아니고요.\n끝까지 같이 놀아드리는 게 전부입니다.", Next=16 },
        // 16: 물귀신
        new Node { Type=NodeType.Line, Spk=Speaker.B, EmotionKey="물귀신_분노",
            Text="안 가! 안 간다고!\n<b>존버는 승리한다!!</b> 나는 아직 안 팔았어!!", Next=17 },
        // 17: 잭두
        new Node { Type=NodeType.Line, Spk=Speaker.A, EmotionKey="잭두_웃음1",
            Text="...기운은 좋으시군요.\n저 정도면 잘 노시겠습니다. 시작하시죠.", Next=18 },
        // 18: end
        new Node { Type=NodeType.End },
    };

    // ── Character appearance ──────────────────────────────────────────
    // 말하는 쪽 강조는 '색 디밍'만 — 크기(localScale)는 안 건드림. CharA/CharB의 크기·위치·
    // 좌우반전(localRotation)은 전부 에디터에서 직접 잡음(대사씬시작 아트 구도, DialogueScene과
    // 동일). 예전엔 여기서 ScaleActive/ScaleInactive를 lerp로 씌웠는데 그게 에디터 스케일을
    // 매번 덮어써서 Play하면 초상화가 커지던 버그가 있었음(DialogueScene에서 먼저 고침).
    static readonly Color ColActive   = Color.white;
    static readonly Color ColInactive = new Color(0.55f, 0.55f, 0.60f, 0.75f);

    // ── UI refs (assigned in Editor — see Assets/Scenes/Stage1DialogueScene.unity) ──
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

    // §1 배경. 예전 Resources.Load("스테1_폐허사이버증권거래소")는 그 파일이 Resources 하위폴더에
    // 있어 bare-key 로드가 null을 반환 → 배경이 아예 안 떴음. 지금은 임시로 §0 옥탑방 배경을
    // 에디터에서 직접 배정(§1 전용 배경 아트 나오면 교체). 노드 BgKey는 그대로 두되(Resources가
    // 잡히면 그때부터 노드별 전환 동작), null이면 이 스프라이트를 안 덮어씀(ApplyBg의 null 가드).
    [SerializeField] Sprite _bgSprite;

    // ── State ─────────────────────────────────────────────────────────
    int         _nodeIdx     = 0;
    bool        _typing      = false;
    bool        _skipRequest = false;
    bool        _waitChoice  = false;
    Coroutine   _typeCr;
    Coroutine   _animCr;
    AudioClip   _blipClip;

    // ── Lifecycle ─────────────────────────────────────────────────────
    IEnumerator Start()
    {
        Time.timeScale = 1f;
        _blipClip = GenerateBlip();
        BuildEmotionDict();

        // CharA(잭두)/CharB(광대물귀신) 베이스 스프라이트는 에디터에서 직접 꽂아둠 — 첫 노드가
        // 곧바로 EmotionKey로 덮어씀. 크기·위치·좌우반전도 에디터 값 그대로(여기서 안 건드림).
        if (_charAImg != null) _charAImg.preserveAspect = true;
        if (_charBImg != null) _charBImg.preserveAspect = true;

        if (_bgImg != null && _bgSprite != null) { _bgImg.sprite = _bgSprite; _bgImg.color = Color.white; }
        if (!string.IsNullOrEmpty(Nodes[0].BgKey)) ApplyBg(Nodes[0].BgKey); // Resources가 잡히면 노드 배경으로 덮음

        _charAImg.color = ColInactive;
        _charBImg.color = ColInactive;

        // TMP 폰트 아틀라스 선제 빌드 (첫 글자 stutter 방지) — Nodes[0]은 물귀신(B)의 대사
        _speakerTxt.text  = NameB;
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
    // 표정 키 → 실제 파일 경로 매핑 (파일명과 키 이름이 다른 경우 처리) — §1에서 실제 쓰는 것만
    // (§0 오프닝 전용 표정들은 DialogueScene 쪽으로 옮겨감 — 그쪽 EmotionKeyToPath 참고)
    static readonly Dictionary<string, string> EmotionKeyToPath = new Dictionary<string, string>
    {
        // 잭두는 §0(DialogueScene)와 같은 새 아트(Assets/대사씬시작/)로 통일.
        { "잭두_무표정2_화남",  "Assets/대사씬시작/대사씬0_잭두_무표정2화남.png" },
        { "잭두_무표정",       "Assets/대사씬시작/대사씬0_잭두_무표정.png" },
        { "잭두_놀람_당황",     "Assets/대사씬시작/대사씬0_잭두_놀람당황.png" },
        { "잭두_슬픔2",        "Assets/대사씬시작/대사씬0_잭두_슬픔2.png" },
        { "잭두_웃음1",        "Assets/대사씬시작/대사씬0_잭두_웃음1.png" },
        // 광대(물귀신)는 새 아트가 아직 없어 기존 EmotionImage로 임시 — 나중에 교체.
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

    // 이 씬을 직접 열어 Play로 테스트할 때 바로 초기화할 수 있도록 — SurfaceLobbyManager도 같은 키를
    // 보고 대사를 다시 스킵할지 판단하니, 로비를 거쳐 재진입할 때도 이 초기화가 적용됨.
    [ContextMenu("스테이지1 지하 인트로 초기화(stage1_underground_intro_seen 지우기)")]
    void ResetIntroSeen()
    {
        PlayerPrefs.DeleteKey("stage1_underground_intro_seen");
        PlayerPrefs.Save();
        Debug.Log("[Stage1DialogueScene] stage1_underground_intro_seen 초기화 완료 — 다음부터 §1 대사 다시 재생됨");
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
    // 말하는 쪽만 밝게, 나머지는 디밍 — 색만 lerp. 크기·반전은 에디터 값 유지(위 주석 참고).
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

    IEnumerator FinishDialogue()
    {
        // 스테이지1 지하 첫 진입 대사 — 오프닝(DialogueScene)과 같은 패턴으로 게임 통틀어 한 번만.
        // 게이팅 자체는 여기서 안 함(그러면 이 씬을 에디터에서 직접 열어 테스트할 때마다 튕겨버림 —
        // 오프닝 때 실제로 겪은 문제), SurfaceLobbyManager.TrySelect()가 이 플래그를 보고 애초에
        // 이 씬을 다시 로드할지 말지 결정함. 여기서는 완주했다는 사실만 기록.
        PlayerPrefs.SetInt("stage1_underground_intro_seen", 1);
        PlayerPrefs.Save();

        yield return new WaitForSecondsRealtime(0.6f);
        SceneTransitionManager.Instance.LoadSceneWithFade("UndergroundLobbyScene");
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
