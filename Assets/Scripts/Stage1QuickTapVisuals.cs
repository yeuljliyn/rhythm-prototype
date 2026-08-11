using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 1-2 (초단타) visual glue. A cup passes hand-to-hand across 5 normal ants standing in a
/// row (each is a Call-role note, in chronological order — same "count OnCallCue mod N" trick as
/// Stage1WaveRideVisuals, just N=5 instead of 4), then the player ant's own Response-role note
/// decides whether the clown actually gets the cup or is left standing with a question mark.
///
/// No art yet, so every "pose" here is a plain primitive rig (Body + Arm, or Head + Cup) tweened
/// directly by this script instead of via Animator/AnimationClip — there's no flipbook art to swap
/// between states yet, so building that infra now would just be thrown away. Swap this for real
/// Animator-driven flipbooks later, same as Stage1WaterPourVisuals/Stage1WaveRideVisuals did once
/// their art arrived.
///
/// Each animated part is guarded against re-trigger while already mid-animation (see the
/// "busy" Coroutine handles below) rather than the stop-and-restart pattern the other two
/// minigames' visuals use. With CallPatternData.minInputIntervalSeconds gating judgement but not
/// CallDirector.OnResponsePress (visuals must still react to every physical press — see
/// Stage1QuickTapVisuals' class history), a stop-and-restart guard means mashing faster than one
/// animation's length perpetually interrupts the "return to idle" half — every restart re-triggers
/// the "swing up" phase before the previous one ever finished swinging back down, so the arm looks
/// permanently raised. Skipping new triggers while already busy instead guarantees every pulse that
/// does play completes its full up-and-down cycle, so the rate is naturally capped by the
/// animation's own length instead of by the input cooldown.
/// </summary>
public class Stage1QuickTapVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("Normal ants — cup passes hand to hand in Call order (exactly 5 Call notes/group)")]
    [SerializeField] RectTransform[] normalAntArms = new RectTransform[5];

    [Header("Player ant — Response note")]
    [SerializeField] RectTransform playerAntArm;
    [SerializeField] Image         playerAntBody;

    [Header("Clown")]
    [SerializeField] RectTransform clownHead;
    [SerializeField] GameObject    clownCup;
    [SerializeField] GameObject    questionMark;

    [Header("Cups pile up beside the player ant on Miss")]
    [SerializeField] Image[] cupStackIcons = new Image[6];

    [Header("Tuning")]
    [SerializeField] float armSwingAngle    = 55f;
    [SerializeField] float armSwingTime     = 0.18f;
    [SerializeField] float headTiltAngle    = -18f;
    [SerializeField] float headTiltTime     = 0.35f;
    [SerializeField] float questionMarkTime = 0.6f;
    [SerializeField] Color missFlashColor   = new Color(1f, 0.35f, 0.35f);

    int   _callCueCount;
    int   _missCount;
    Color _playerAntIdleColor;
    Coroutine _questionMarkCr;

    // "Busy" handles — non-null while that part's animation is still playing. A new trigger while
    // busy is dropped (not queued, not restarted) so the current cycle always finishes cleanly.
    Coroutine[] _antCrs;
    Coroutine   _playerArmCr;
    Coroutine   _clownCr;

    void Awake()
    {
        if (playerAntBody != null) _playerAntIdleColor = playerAntBody.color;
        _antCrs = new Coroutine[normalAntArms != null ? normalAntArms.Length : 0];
    }

    void OnEnable()
    {
        if (callDirector != null) callDirector.OnCallCue += HandleCallCue;
        if (GameManager.Instance != null) GameManager.Instance.OnJudgement += HandleJudgement;
    }

    void OnDisable()
    {
        if (callDirector != null) callDirector.OnCallCue -= HandleCallCue;
        if (GameManager.Instance != null) GameManager.Instance.OnJudgement -= HandleJudgement;
    }

    void Start()
    {
        if (questionMark != null) questionMark.SetActive(false);
        if (clownCup     != null) clownCup.SetActive(false);
        if (cupStackIcons != null)
            foreach (var icon in cupStackIcons)
                if (icon != null) icon.gameObject.SetActive(false);
    }

    void HandleCallCue()
    {
        if (normalAntArms == null || normalAntArms.Length == 0) return;
        int antIndex = _callCueCount % normalAntArms.Length;
        _callCueCount++;

        var arm = normalAntArms[antIndex];
        if (arm == null || _antCrs[antIndex] != null) return; // still mid-swing — let it finish

        int capturedIndex = antIndex;
        _antCrs[antIndex] = StartCoroutine(
            SwingRoutine(arm, armSwingAngle, armSwingTime, () => _antCrs[capturedIndex] = null));
    }

    void HandleJudgement(JudgementType j)
    {
        if (playerAntArm != null && _playerArmCr == null)
        {
            _playerArmCr = j == JudgementType.Hit
                ? StartCoroutine(SwingRoutine(playerAntArm, armSwingAngle, armSwingTime, () => _playerArmCr = null))
                : StartCoroutine(FumbleRoutine(() => _playerArmCr = null));
        }

        if (j == JudgementType.Hit)
        {
            if (clownCup != null && _clownCr == null)
                _clownCr = StartCoroutine(DrinkRoutine(() => _clownCr = null));
        }
        else
        {
            if (clownHead != null && _clownCr == null)
                _clownCr = StartCoroutine(TiltRoutine(clownHead, headTiltAngle, headTiltTime, () => _clownCr = null));

            ShowQuestionMark();
            RevealNextCup();
        }
    }

    IEnumerator SwingRoutine(RectTransform arm, float peakAngle, float duration, Action onComplete)
    {
        float half = duration * 0.5f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            arm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, peakAngle, t / half));
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            arm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(peakAngle, 0f, t / half));
            yield return null;
        }
        arm.localRotation = Quaternion.identity;
        onComplete?.Invoke();
    }

    // Fumble: arm flails at decaying amplitude instead of a clean swing — visually distinct from
    // both Idle and a good Throw without relying on a color flash alone.
    IEnumerator FumbleRoutine(Action onComplete)
    {
        if (playerAntBody != null) playerAntBody.color = missFlashColor;

        float duration = armSwingTime * 1.6f;
        const float freq = 3f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float decay = 1f - t / duration;
            float angle = Mathf.Sin(t * freq * Mathf.PI * 2f) * armSwingAngle * 0.6f * decay;
            playerAntArm.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
        playerAntArm.localRotation = Quaternion.identity;
        if (playerAntBody != null) playerAntBody.color = _playerAntIdleColor;
        onComplete?.Invoke();
    }

    IEnumerator DrinkRoutine(Action onComplete)
    {
        clownCup.SetActive(true);
        yield return TiltRoutine(clownHead, headTiltAngle, headTiltTime, null);
        clownCup.SetActive(false);
        onComplete?.Invoke();
    }

    IEnumerator TiltRoutine(RectTransform head, float peakAngle, float duration, Action onComplete)
    {
        float half = duration * 0.5f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, peakAngle, t / half));
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(peakAngle, 0f, t / half));
            yield return null;
        }
        head.localRotation = Quaternion.identity;
        onComplete?.Invoke();
    }

    void ShowQuestionMark()
    {
        if (questionMark == null) return;
        if (_questionMarkCr != null) StopCoroutine(_questionMarkCr);
        _questionMarkCr = StartCoroutine(QuestionMarkRoutine());
    }

    IEnumerator QuestionMarkRoutine()
    {
        questionMark.SetActive(true);
        yield return new WaitForSeconds(questionMarkTime);
        questionMark.SetActive(false);
        _questionMarkCr = null;
    }

    void RevealNextCup()
    {
        if (cupStackIcons != null && _missCount < cupStackIcons.Length && cupStackIcons[_missCount] != null)
            cupStackIcons[_missCount].gameObject.SetActive(true);
        _missCount++;
    }
}
