using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class DialogueSceneManager : MonoBehaviour
{
    // ── Dialogue data ─────────────────────────────────────────────────────

    enum Speaker { A, B }

    struct Line { public Speaker Speaker; public string Text; }

    // Placeholder dialogue — replace with real script later.
    // Speaker A = protagonist (left), Speaker B = opponent (right).
    static readonly Line[] Script =
    {
        new Line { Speaker=Speaker.B, Text="Another challenger. How... refreshing." },
        new Line { Speaker=Speaker.A, Text="I'm not just any challenger. I'm here to end your reign." },
        new Line { Speaker=Speaker.B, Text="Ha. They all say that. None of them could keep up with my rhythm." },
        new Line { Speaker=Speaker.A, Text="Then I'll be the first. I've been preparing for this moment." },
        new Line { Speaker=Speaker.B, Text="Preparation means nothing against true talent." },
        new Line { Speaker=Speaker.A, Text="We'll see. Every beat I've ever felt has led me here." },
        new Line { Speaker=Speaker.B, Text="Bold words. But words don't win rhythm battles." },
        new Line { Speaker=Speaker.A, Text="No... but this does." },
        new Line { Speaker=Speaker.B, Text="Hmph. Fine. Show me what you've got." },
        new Line { Speaker=Speaker.A, Text="Let's go. Feel so gut!" },
    };

    static readonly string NameA = "JACKDO";       // 플레이어
    static readonly string NameB = "WATER GHOST"; // 보스 (물귀신)

    // ── Character appearance ──────────────────────────────────────────────

    static readonly Color ColA_Active   = Color.white;
    static readonly Color ColA_Inactive = new Color(0.30f, 0.30f, 0.30f, 0.65f);
    static readonly Color ColB_Active   = Color.white;
    static readonly Color ColB_Inactive = new Color(0.30f, 0.30f, 0.30f, 0.65f);

    static readonly Vector3 ScaleActive   = new Vector3(1.10f, 1.10f, 1f);
    static readonly Vector3 ScaleInactive = new Vector3(0.93f, 0.93f, 1f);

    // ── UI refs ───────────────────────────────────────────────────────────

    RectTransform   _charARt, _charBRt;
    Image           _charAImg, _charBImg;
    TextMeshProUGUI _speakerTxt, _dialogueTxt;
    GameObject      _promptGO;

    // ── State ─────────────────────────────────────────────────────────────

    int         _idx         = 0;
    bool        _typing      = false;
    bool        _skipRequest = false;
    Coroutine   _typeCr;
    Coroutine   _animCr;
    AudioSource _voice;
    AudioClip   _blipClip;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    IEnumerator Start()
    {
        Time.timeScale = 1f;
        BuildUI();
        BuildVoice();

        var spriteA = Resources.Load<Sprite>("사이보그무당_배경제거");
        if (spriteA != null) { _charAImg.sprite = spriteA; _charAImg.preserveAspect = true; }

        var spriteB = Resources.Load<Sprite>("광대물귀신_배경제거");
        if (spriteB != null) { _charBImg.sprite = spriteB; _charBImg.preserveAspect = true; }

        _charARt.localScale = ScaleInactive;
        _charBRt.localScale = ScaleInactive;
        _charAImg.color     = ColA_Inactive;
        _charBImg.color     = ColB_Inactive;

        // 페이드 중에 TMP 폰트 아틀라스 선제 빌드 (페이드 완료 후 첫 글자 stutter 방지)
        _speakerTxt.text  = Script[0].Speaker == Speaker.A ? NameA : NameB;
        _dialogueTxt.text = Script[0].Text;
        Canvas.ForceUpdateCanvases();

        var tm = SceneTransitionManager.Instance;
        if (tm != null)
            while (tm.IsTransitioning)
                yield return null;

        _speakerTxt.text  = "";
        _dialogueTxt.text = "";
        PlayLine(0);
    }

    void Update()
    {
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
            _skipRequest = true;
        }
        else
        {
            _idx++;
            if (_idx >= Script.Length)
                StartCoroutine(FinishDialogue());
            else
                PlayLine(_idx);
        }
    }

    // ── Dialogue logic ────────────────────────────────────────────────────

    void PlayLine(int idx)
    {
        var  line = Script[idx];
        bool isA  = line.Speaker == Speaker.A;

        _speakerTxt.text  = isA ? NameA : NameB;
        _speakerTxt.color = isA
            ? new Color(0.25f, 0.85f, 0.50f)   // JACKDO — 초록
            : new Color(0.80f, 0.40f, 1.00f);   // WATER GHOST — 보라

        if (_animCr != null) StopCoroutine(_animCr);
        _animCr = StartCoroutine(AnimateChars(isA));

        if (_typeCr != null) StopCoroutine(_typeCr);
        _skipRequest = false;
        _typeCr = StartCoroutine(TypeText(line.Text));

        _promptGO.SetActive(false);
    }

    IEnumerator AnimateChars(bool aActive)
    {
        const float duration = 0.20f;
        float elapsed = 0f;

        Vector3 aScaleFrom = _charARt.localScale;
        Vector3 bScaleFrom = _charBRt.localScale;
        Color   aColFrom   = _charAImg.color;
        Color   bColFrom   = _charBImg.color;

        Vector3 aScaleTo = aActive ? ScaleActive   : ScaleInactive;
        Vector3 bScaleTo = aActive ? ScaleInactive : ScaleActive;
        Color   aColTo   = aActive ? ColA_Active   : ColA_Inactive;
        Color   bColTo   = aActive ? ColB_Inactive : ColB_Active;

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

    IEnumerator TypeText(string text)
    {
        _typing           = true;
        _dialogueTxt.text = "";
        const float delay = 0.032f;

        bool isA = Script[_idx].Speaker == Speaker.A;

        foreach (char c in text)
        {
            if (_skipRequest)
            {
                _dialogueTxt.text = text;
                break;
            }
            _dialogueTxt.text += c;

            // Play gibberish blip for visible characters (skip space/punctuation)
            if (c != ' ' && c != '.' && c != ',' && c != '!' && c != '?' && c != '\n')
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
        SceneTransitionManager.Instance.LoadSceneWithFade("BattleScene");
    }

    // ── Voice (Animal Crossing-style gibberish) ───────────────────────────

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

    // Map each character to a consistent pitch so the same letter always
    // sounds the same — gives the voice a distinct character-specific timbre.
    static float CharToPitch(char c, bool isA)
    {
        char lc = char.ToLower(c);

        // Vowels get specific pitches (recognisable "vowel" feel)
        float p = lc switch
        {
            'a' => 1.00f, 'e' => 1.14f, 'i' => 1.30f,
            'o' => 0.88f, 'u' => 1.06f,
            _ => 0.85f + ((int)lc % 10) * 0.04f   // consonants: spread across scale
        };

        // Shift the whole voice up/down per character so A and B sound different
        return isA ? p * 1.10f : p * 0.88f;
    }

    // Short sine-wave burst — ~18ms, quick linear decay → "blip" sound.
    static AudioClip GenerateBlip()
    {
        const int   rate     = 44100;
        const float freq     = 520f;
        const float dur      = 0.018f;
        int   n    = (int)(rate * dur);
        var   data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t   = (float)i / rate;
            float env = 1f - t / dur;              // linear decay
            data[i]   = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.9f;
        }
        var clip = AudioClip.Create("Blip", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ── UI Builder ────────────────────────────────────────────────────────

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

        // ── Background ──
        var bgGO     = Stretch(cGO.transform, "BG");
        var bgImg    = bgGO.AddComponent<Image>();
        var bgSprite = Resources.Load<Sprite>("스테1_폐허사이버증권거래소");
        if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }
        else bgImg.color = new Color(0.04f, 0.02f, 0.08f);

        // ── Separator line above dialogue box ──
        var lineGO   = Anchored(cGO.transform, "Line",
                           new Vector2(0f, 0.22f), new Vector2(1f, 0.22f),
                           new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), Vector2.zero);
        lineGO.AddComponent<Image>().color = new Color(0.40f, 0.30f, 0.60f, 0.60f);

        // ── Character A — left ──
        var aGO  = Anchored(cGO.transform, "CharA",
                       new Vector2(0.06f, 0.20f), new Vector2(0.38f, 0.98f),
                       new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        _charARt  = aGO.GetComponent<RectTransform>();
        _charAImg = aGO.AddComponent<Image>();

        // Character A label
        var aLblGO  = Anchored(aGO.transform, "NameA",
                         new Vector2(0f, 0f), new Vector2(1f, 0f),
                         new Vector2(0.5f, 1f), new Vector2(0f, 36f), new Vector2(0f, -4f));
        var aLbl    = aLblGO.AddComponent<TextMeshProUGUI>();
        aLbl.text          = NameA;
        aLbl.fontSize      = 18f;
        aLbl.alignment     = TextAlignmentOptions.Center;
        aLbl.color         = new Color(0.55f, 1.00f, 0.65f, 0.80f);
        aLbl.raycastTarget = false;

        // ── Character B — right ──
        var bGO  = Anchored(cGO.transform, "CharB",
                       new Vector2(0.62f, 0.20f), new Vector2(0.94f, 0.98f),
                       new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        _charBRt  = bGO.GetComponent<RectTransform>();
        _charBImg = bGO.AddComponent<Image>();

        // Character B label
        var bLblGO  = Anchored(bGO.transform, "NameB",
                         new Vector2(0f, 0f), new Vector2(1f, 0f),
                         new Vector2(0.5f, 1f), new Vector2(0f, 36f), new Vector2(0f, -4f));
        var bLbl    = bLblGO.AddComponent<TextMeshProUGUI>();
        bLbl.text          = NameB;
        bLbl.fontSize      = 18f;
        bLbl.alignment     = TextAlignmentOptions.Center;
        bLbl.color         = new Color(0.90f, 0.50f, 1.00f, 0.80f);
        bLbl.raycastTarget = false;

        // ── Dialogue box ──
        var dlgBox = Anchored(cGO.transform, "DialogueBox",
                         Vector2.zero, new Vector2(1f, 0.22f),
                         new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        dlgBox.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.10f, 0.97f);

        // Speaker name
        var nameGO  = Anchored(dlgBox.transform, "SpeakerName",
                          new Vector2(0.04f, 0.62f), new Vector2(0.50f, 0.95f),
                          new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _speakerTxt            = nameGO.AddComponent<TextMeshProUGUI>();
        _speakerTxt.fontSize   = 24f;
        _speakerTxt.fontStyle  = FontStyles.Bold;
        _speakerTxt.alignment  = TextAlignmentOptions.BottomLeft;
        _speakerTxt.color      = Color.white;
        _speakerTxt.raycastTarget = false;

        // Dialogue text
        var textGO  = Anchored(dlgBox.transform, "DialogueText",
                          new Vector2(0.04f, 0.06f), new Vector2(0.93f, 0.62f),
                          new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        _dialogueTxt            = textGO.AddComponent<TextMeshProUGUI>();
        _dialogueTxt.fontSize   = 26f;
        _dialogueTxt.alignment  = TextAlignmentOptions.TopLeft;
        _dialogueTxt.color      = new Color(0.92f, 0.92f, 1.00f);
        _dialogueTxt.raycastTarget = false;

        // ▶ prompt
        _promptGO = Anchored(dlgBox.transform, "Prompt",
                        new Vector2(0.93f, 0.06f), new Vector2(1.00f, 0.50f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var promptTxt          = _promptGO.AddComponent<TextMeshProUGUI>();
        promptTxt.text         = "▶";
        promptTxt.fontSize     = 28f;
        promptTxt.alignment    = TextAlignmentOptions.Center;
        promptTxt.color        = new Color(0.75f, 0.75f, 1f, 0.9f);
        promptTxt.raycastTarget = false;
        _promptGO.SetActive(false);

        // Skip hint
        var skipGO  = Anchored(dlgBox.transform, "SkipHint",
                          new Vector2(0.04f, 0f), new Vector2(0.5f, 0.10f),
                          new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
        var skipTxt = skipGO.AddComponent<TextMeshProUGUI>();
        skipTxt.text          = "CLICK  /  SPACE  to continue";
        skipTxt.fontSize      = 13f;
        skipTxt.alignment     = TextAlignmentOptions.BottomLeft;
        skipTxt.color         = new Color(0.45f, 0.45f, 0.65f, 0.80f);
        skipTxt.raycastTarget = false;
    }

    // ── UI helpers ────────────────────────────────────────────────────────

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
        var r    = go.AddComponent<RectTransform>();
        r.anchorMin        = anchorMin;
        r.anchorMax        = anchorMax;
        r.pivot            = pivot;
        r.sizeDelta        = sizeDelta;
        r.anchoredPosition = anchoredPos;
        return go;
    }
}
