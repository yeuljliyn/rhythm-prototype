using System.Collections;
using UnityEngine;

/// <summary>
/// Generalized version of the "Stage1TutorialGate" pattern (see that file — kept untouched, only
/// wired into the older Stage1Scene) — orchestrates N sequential practice pattern segments, then
/// hands off to the main game, all inside one stage scene with no reload.
///
/// Each segment is its own GameObject holding an AudioSource + CallClock + CallDirector +
/// PracticeLoopExtender, driving a one-cycle CallPatternData (loopLengthBeats &gt; 0) that gets
/// demoed once then repeated seamlessly until requiredHits Hits land (see PracticeLoopExtender).
/// This gate just watches GameManager.OnJudgement to count those Hits, drives the speech
/// bubble/skip/counter UI, and swaps which segment (or the main Director) is active — every
/// segment/Director swap follows the same "deactivate old, activate+rebind new" idiom already used
/// project-wide (Stage1TutorialGate, Stage1WaterPourVisuals.BindDirector, etc).
///
/// No PlayerPrefs "already seen" flag — practice always plays from the top on every entry into the
/// scene. The only way through is landing the hits or holding the skip key (Stage1SkipHold).
/// </summary>
public class Stage1PracticeGate : MonoBehaviour
{
    [System.Serializable]
    public class PracticeSegment
    {
        [Tooltip("이 세그먼트의 AudioSource+CallClock+CallDirector+PracticeLoopExtender를 들고 있는 오브젝트.")]
        public GameObject directorRoot;
        [Tooltip("이 세그먼트가 시작될 때(시범 시작 시점) 보여줄 말풍선 대사. 비우면 안 뜸.")]
        public string introCaption;
        [Tooltip("이 세그먼트의 시범이 끝나고 따라하기가 시작되는 순간 보여줄 대사(보통 세그먼트0만 " +
                 "\"해보십시오.\" 같은 걸 채움, 그 뒤 세그먼트는 보통 비워둠).")]
        public string demoStartCaption;
    }

    [SerializeField] PracticeSegment[] segments;
    [SerializeField] GameObject mainDirectorGO;
    [SerializeField] string mainTransitionCaption = "이제 진짜입니다.";
    [SerializeField] int requiredHits = 3;
    [Tooltip("세그먼트 전환/본편 전환 직전, 현재 세그먼트 오디오가 서서히 작아지는 데 걸리는 시간.")]
    [SerializeField] float audioFadeSeconds = 1f;
    [Tooltip("말풍선 한 줄이 자동으로 사라지기까지 걸리는 시간.")]
    [SerializeField] float captionHoldSeconds = 3f;

    [Header("이 스테이지의 Visuals 스크립트(IPracticeRebindable 구현체) — MonoBehaviour로 직렬화 후 캐스팅")]
    [SerializeField] MonoBehaviour visualsRebindTarget;

    [SerializeField] Stage1SpeechBubble speechBubble;
    [SerializeField] Stage1SkipHold     skipHold;
    [SerializeField] Stage1PracticeCounter counter;

    IPracticeRebindable _visuals;
    int  _segmentIndex;
    int  _hits;
    bool _segmentLocked; // true once requiredHits landed (or skipped) — ignore further judgements while fading
    PracticeLoopExtender _currentExtender;

    void Awake()
    {
        _visuals = visualsRebindTarget as IPracticeRebindable;
        if (_visuals == null)
            Debug.LogError($"{name}: visualsRebindTarget doesn't implement IPracticeRebindable.");
    }

    void Start()
    {
        if (mainDirectorGO != null) mainDirectorGO.SetActive(false);
        foreach (var s in segments)
            if (s.directorRoot != null) s.directorRoot.SetActive(false);

        if (skipHold != null) skipHold.OnSkip += HandleSkip;

        if (segments == null || segments.Length == 0)
        {
            SwitchToMain();
            return;
        }

        _segmentIndex = 0;
        EnterSegment(0);
    }

    void OnEnable()  { if (GameManager.Instance != null) GameManager.Instance.OnJudgement += HandleJudgement; }
    void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnJudgement -= HandleJudgement;
        if (skipHold != null) skipHold.OnSkip -= HandleSkip;
        UnsubscribeCurrentExtender();
    }

    // Skip must be pressable from the very first frame of a segment (even before the intro line
    // is dismissed — "스킵은 항상 열려 있다... 첫 진입에도 누를 수 있다"), so the counter/skip UI
    // come up immediately here, but the segment's Director doesn't actually start (no
    // CallClock/CallDirector running yet) until the player dismisses the intro caption — see
    // StartSegmentPlay. If there's no intro caption for this segment, play starts immediately.
    void EnterSegment(int index)
    {
        var seg = segments[index];
        _hits = 0;
        _segmentLocked = false;

        if (counter != null) { counter.SetVisible(true); counter.Configure(requiredHits); counter.SetProgress(0); }
        if (skipHold != null) skipHold.SetActive(true);

        if (!string.IsNullOrEmpty(seg.introCaption) && speechBubble != null)
            speechBubble.ShowUntilConfirm(seg.introCaption, () => StartSegmentPlay(index));
        else
            StartSegmentPlay(index);
    }

    void StartSegmentPlay(int index)
    {
        if (_segmentLocked) return; // skipped while the intro line was still up

        var seg = segments[index];
        if (seg.directorRoot != null) seg.directorRoot.SetActive(true);
        var director = seg.directorRoot != null ? seg.directorRoot.GetComponent<CallDirector>() : null;
        _visuals?.BindDirector(director);

        UnsubscribeCurrentExtender();
        _currentExtender = seg.directorRoot != null ? seg.directorRoot.GetComponent<PracticeLoopExtender>() : null;
        if (_currentExtender != null) _currentExtender.OnDemoFinished += HandleDemoFinished;
    }

    void UnsubscribeCurrentExtender()
    {
        if (_currentExtender != null) _currentExtender.OnDemoFinished -= HandleDemoFinished;
        _currentExtender = null;
    }

    void HandleDemoFinished()
    {
        var caption = segments[_segmentIndex].demoStartCaption;
        if (!string.IsNullOrEmpty(caption) && speechBubble != null)
            speechBubble.ShowFor(caption, captionHoldSeconds);
    }

    void HandleJudgement(JudgementType type)
    {
        if (_segmentLocked) return;
        if (type != JudgementType.Hit) return;

        _hits++;
        if (counter != null) counter.SetProgress(_hits);

        if (_hits >= requiredHits)
        {
            _segmentLocked = true;
            StartCoroutine(AdvanceAfterFade(segments[_segmentIndex].directorRoot));
        }
    }

    void HandleSkip()
    {
        if (_segmentLocked) return; // already mid-advance
        _segmentLocked = true;
        speechBubble?.CancelWait(); // in case the intro line's still up waiting for a confirm press
        var current = _segmentIndex < segments.Length ? segments[_segmentIndex].directorRoot : null;
        StartCoroutine(SkipToMainAfterFade(current));
    }

    IEnumerator AdvanceAfterFade(GameObject segmentRoot)
    {
        yield return FadeOutSegmentAudio(segmentRoot);

        if (segmentRoot != null) segmentRoot.SetActive(false);
        UnsubscribeCurrentExtender();

        _segmentIndex++;
        if (_segmentIndex < segments.Length)
        {
            EnterSegment(_segmentIndex);
        }
        else
        {
            if (!string.IsNullOrEmpty(mainTransitionCaption) && speechBubble != null)
                speechBubble.ShowFor(mainTransitionCaption, captionHoldSeconds);
            SwitchToMain();
        }
    }

    IEnumerator SkipToMainAfterFade(GameObject segmentRoot)
    {
        yield return FadeOutSegmentAudio(segmentRoot);

        if (segmentRoot != null) segmentRoot.SetActive(false);
        UnsubscribeCurrentExtender();

        if (!string.IsNullOrEmpty(mainTransitionCaption) && speechBubble != null)
            speechBubble.ShowFor(mainTransitionCaption, captionHoldSeconds);
        SwitchToMain();
    }

    IEnumerator FadeOutSegmentAudio(GameObject segmentRoot)
    {
        var src = segmentRoot != null ? segmentRoot.GetComponent<AudioSource>() : null;
        if (src == null) yield break;

        float startVol = src.volume;
        float t = 0f;
        while (t < audioFadeSeconds)
        {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(startVol, 0f, t / audioFadeSeconds);
            yield return null;
        }
        src.volume = 0f;
    }

    void SwitchToMain()
    {
        if (counter != null) counter.SetVisible(false);
        if (skipHold != null) skipHold.SetActive(false);
        if (speechBubble != null) speechBubble.Hide();

        if (mainDirectorGO != null) mainDirectorGO.SetActive(true);
        var mainDirector = mainDirectorGO != null ? mainDirectorGO.GetComponent<CallDirector>() : null;
        _visuals?.BindDirector(mainDirector);

        GameManager.Instance?.ResetGame(); // wipe practice hits/misses — scoring starts fresh at main
    }
}
