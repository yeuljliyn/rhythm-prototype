using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 1-1 (파도타기) visual glue. Five ants stand in a row; the clown/wave rides across them
/// left to right. Ant1..Ant4 are Call-role notes (each shows the rhythm by raising its arm in turn),
/// Ant5 is the single Response-role note the player has to hit.
///
/// CallDirector's events carry no "which note" payload (see CallDirector.OnCallCue), so which ant
/// just cued is inferred purely from firing order: OnCallCue fires once per Call-role note in
/// chronological order, and this chart is authored (Call Map Editor, hand-placed — no Z echo, since
/// Z would mirror the call count 1:1 into an equal-count response instead of collapsing 4 calls into
/// a single response) with exactly 4 Call notes per group. So a running counter mod 4 recovers the
/// ant index (0..3) without CallDirector needing to know anything about ants.
///
/// Ant5 and the clown's move to Ant5's spot are driven by OnPhaseChanged(true)/OnResponsePress
/// instead — chart-scheduled and input-driven respectively, same split Stage1WaterPourVisuals uses.
/// </summary>
public class Stage1WaveRideVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("Ant1..Ant4 = Call, Ant5 (last) = Response")]
    [SerializeField] Image[] antImages = new Image[5];
    [SerializeField] Sprite callAntIdleSprite;
    [SerializeField] Sprite callAntRaisedSprite;
    [SerializeField] Sprite responseAntIdleSprite;
    [SerializeField] Sprite responseAntRaisedSprite;

    [SerializeField] RectTransform clownRect;

    [SerializeField] float antPulseDuration = 0.25f;
    [SerializeField] float clownMoveDuration = 0.15f;

    int _callCueCount;
    readonly Coroutine[] _antPulses = new Coroutine[5];
    Coroutine _clownMove;

    void OnEnable()  { Bind(callDirector); }
    void OnDisable() { Unbind(callDirector); }

    void Start()
    {
        for (int i = 0; i < antImages.Length; i++)
            if (antImages[i] != null) antImages[i].sprite = IdleSpriteFor(i);

        if (clownRect != null && antImages.Length > 0 && antImages[0] != null)
            SetClownX(antImages[0].rectTransform.anchoredPosition.x);
    }

    /// Switches which CallDirector drives these visuals — same rebind pattern as
    /// Stage1WaterPourVisuals, kept for consistency if 1-1 ever grows a tutorial gate.
    public void BindDirector(CallDirector newDirector)
    {
        Unbind(callDirector);
        callDirector = newDirector;
        Bind(callDirector);
        _callCueCount = 0;
    }

    void Bind(CallDirector d)
    {
        if (d == null) return;
        d.OnCallCue       += HandleCallCue;
        d.OnPhaseChanged  += HandlePhaseChanged;
        d.OnResponsePress += HandleResponsePress;
    }

    void Unbind(CallDirector d)
    {
        if (d == null) return;
        d.OnCallCue       -= HandleCallCue;
        d.OnPhaseChanged  -= HandlePhaseChanged;
        d.OnResponsePress -= HandleResponsePress;
    }

    void HandleCallCue()
    {
        int antIndex = _callCueCount % 4; // relies on exactly 4 Call notes per group — see class doc
        _callCueCount++;
        PulseAnt(antIndex);
        MoveClownToAnt(antIndex);
    }

    void HandlePhaseChanged(bool isResponsePhase)
    {
        if (isResponsePhase) MoveClownToAnt(4);
    }

    void HandleResponsePress()
    {
        PulseAnt(4);
    }

    void PulseAnt(int index)
    {
        if (index < 0 || index >= antImages.Length || antImages[index] == null) return;
        if (_antPulses[index] != null) StopCoroutine(_antPulses[index]);
        _antPulses[index] = StartCoroutine(PulseAntRoutine(index));
    }

    IEnumerator PulseAntRoutine(int index)
    {
        var img = antImages[index];
        img.sprite = RaisedSpriteFor(index);
        yield return new WaitForSeconds(antPulseDuration);
        img.sprite = IdleSpriteFor(index);
        _antPulses[index] = null;
    }

    // Ant1..Ant4 (index 0-3, Call role) share one look; Ant5 (index 4, Response role) is the
    // player's own distinct character.
    Sprite IdleSpriteFor(int index)   => index == 4 ? responseAntIdleSprite   : callAntIdleSprite;
    Sprite RaisedSpriteFor(int index) => index == 4 ? responseAntRaisedSprite : callAntRaisedSprite;

    void MoveClownToAnt(int index)
    {
        if (clownRect == null || index < 0 || index >= antImages.Length || antImages[index] == null) return;
        if (_clownMove != null) StopCoroutine(_clownMove);
        _clownMove = StartCoroutine(MoveClownRoutine(antImages[index].rectTransform.anchoredPosition.x));
    }

    IEnumerator MoveClownRoutine(float targetX)
    {
        float startX = clownRect.anchoredPosition.x;
        float t = 0f;
        while (t < clownMoveDuration)
        {
            t += Time.deltaTime;
            SetClownX(Mathf.Lerp(startX, targetX, Mathf.Clamp01(t / clownMoveDuration)));
            yield return null;
        }
        SetClownX(targetX);
        _clownMove = null;
    }

    void SetClownX(float x)
    {
        var pos = clownRect.anchoredPosition;
        pos.x = x;
        clownRect.anchoredPosition = pos;
    }
}
