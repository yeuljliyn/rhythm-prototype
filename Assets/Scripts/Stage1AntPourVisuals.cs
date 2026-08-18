using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage1_4 glue: Hand pours as a Call demo, PlayerAnt pours in Response, Clown just watches and
/// reacts to the outcome (Hit/Miss). Static Image sprite-swaps only (no Animator/AnimationClip) —
/// same style as Stage1SurviveVisuals. CallDirector stays completely unaware of
/// Hand/PlayerAnt/Clown, same as every other Stage1 minigame glue script.
/// </summary>
public class Stage1AntPourVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("Hand (Call)")]
    [SerializeField] Image handImage;
    [SerializeField] Sprite handIdleSprite;
    [SerializeField] Sprite handPourSprite;
    [SerializeField] float handPourHoldTime = 0.35f;

    [Header("Player Ant (Response)")]
    [SerializeField] Image playerAntImage;
    [SerializeField] Sprite playerAntIdleSprite;
    [SerializeField] Sprite playerAntPourSprite;
    [SerializeField] Sprite playerAntFailSprite;
    [SerializeField] float playerReactHoldTime = 0.35f;

    [Header("Clown (reacts to outcome)")]
    [SerializeField] Image clownImage;
    [SerializeField] Sprite clownIdleSprite;
    [SerializeField] Sprite clownFailSprite;
    [SerializeField] Sprite clownSuccessSprite;
    [SerializeField] float clownReactHoldTime = 0.45f;

    Coroutine _handCr, _playerCr, _clownCr;

    void OnEnable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup += HandleCallCue;
        callDirector.OnGroupJudged  += HandleGroupJudged;
        callDirector.OnResponsePress += HandlePlayerPress;
    }

    void OnDisable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup -= HandleCallCue;
        callDirector.OnGroupJudged  -= HandleGroupJudged;
        callDirector.OnResponsePress -= HandlePlayerPress;
    }

    void Start()
    {
        if (handImage != null)      handImage.sprite      = handIdleSprite;
        if (playerAntImage != null) playerAntImage.sprite = playerAntIdleSprite;
        if (clownImage != null)     clownImage.sprite     = clownIdleSprite;
    }

    void HandleCallCue(int groupId)
    {
        if (_handCr != null) StopCoroutine(_handCr);
        _handCr = StartCoroutine(HandPulse());
    }

    void HandleGroupJudged(int groupId, JudgementType j)
    {
        bool hit = j == JudgementType.Hit;

        if (_playerCr != null) StopCoroutine(_playerCr);
        _playerCr = StartCoroutine(PlayerReact(hit));

        if (_clownCr != null) StopCoroutine(_clownCr);
        _clownCr = StartCoroutine(ClownReact(hit));
    }

    // Fires on every physical press, judged or not (hit/miss-window 밖의 "그냥 눌러본" 탭 포함) — the
    // player ant should still visibly pour so tapping always feels responsive, even when nothing
    // is being judged. Busy-guard (skip while a pulse is already playing), NOT cancel-and-restart —
    // CallDirector's own mash cooldown only blocks *judgement*, OnResponsePress still fires on every
    // single press regardless, so cancel-and-restart here let spamming keep resetting the pulse back
    // to "pouring" before it ever got to revert, making the ant look permanently stuck pouring
    // instead of toggling normal/pour per tap. A press that DOES land in a judged window still fires
    // this first, then HandleGroupJudged immediately after (same Update tick) — that one still
    // cancels-and-restarts, so a judged Miss always shows correctly even if this just started a
    // generic pour pulse a moment earlier.
    void HandlePlayerPress()
    {
        if (_playerCr != null) return;
        _playerCr = StartCoroutine(PlayerReact(hit: true));
    }

    IEnumerator HandPulse()
    {
        if (handImage == null) yield break;
        handImage.sprite = handPourSprite;
        yield return new WaitForSeconds(handPourHoldTime);
        handImage.sprite = handIdleSprite;
        _handCr = null;
    }

    IEnumerator PlayerReact(bool hit)
    {
        if (playerAntImage == null) yield break;
        playerAntImage.sprite = hit ? playerAntPourSprite : playerAntFailSprite;
        yield return new WaitForSeconds(playerReactHoldTime);
        playerAntImage.sprite = playerAntIdleSprite;
        _playerCr = null;
    }

    IEnumerator ClownReact(bool hit)
    {
        if (clownImage == null) yield break;
        clownImage.sprite = hit ? clownSuccessSprite : clownFailSprite;
        yield return new WaitForSeconds(clownReactHoldTime);
        clownImage.sprite = clownIdleSprite;
        _clownCr = null;
    }
}
