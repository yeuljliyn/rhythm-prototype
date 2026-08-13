using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 1-2 (초단타) visual glue. A cup passes hand-to-hand across 5 normal ants standing in a
/// row (each is a Call-role note, in chronological order — same "count OnCallCue mod N" trick as
/// Stage1WaveRideVisuals, just N=5 instead of 4), then the player ant's own Response-role note
/// decides whether the clown actually gets the cup or is left standing with a question mark.
///
/// Backed by real flipbook art, swapped via Image.sprite — same pattern as Stage1WaveRideVisuals/
/// Stage1WaterPourVisuals. Each character (ants/player/clown) is a single Image; the old primitive
/// Body+Arm/Head+Cup rig's leftover child GameObjects are deactivated in the scene, not deleted.
///
/// The traveling cup is a POOL of Image instances (cupPool), not a single object. CallPatternData's
/// calls are explicitly allowed to overlap in time (see the class doc on CallPatternData/CallEntry —
/// that's this engine's whole point, unlike the old phase-separated PatternData): a new group's Call
/// cues can legitimately start firing while the previous group's Response note is still awaiting
/// judgement (lookahead). Each group claims one free pool slot at its own first Call cue
/// (_activeCupSlot, reassigned in HandleCallCue when antIndex==0) and keeps hopping that same slot for
/// the rest of its own 5 calls — calls within one group always fire strictly in order with no other
/// group's calls interleaved between them, so _activeCupSlot alone is enough to track "which slot is
/// this group's calls currently hopping."
///
/// Judgement is a different story: because the NEXT group's calls can already be firing by the time
/// THIS group's Response finally gets judged, _activeCupSlot may have already moved on to the next
/// group by the time HandleJudgement runs — reading it directly there would grab the wrong group's cup
/// (this was an actual bug: it caused a cup to jump ahead mid-relay, then the group it was stolen from
/// would yank it back on its own next call cue). _pendingResponseSlots is a FIFO queue instead: every
/// group's slot is enqueued when that group's calls start, and HandleJudgement always dequeues the
/// front — the oldest still-unjudged group's slot — which is correct as long as Responses resolve in
/// the same order their groups started, true for this chart's one-Response-in-flight-at-a-time shape.
///
/// World-space RectTransform.position is used for each hop's start/end (not anchoredPosition), so it
/// works correctly regardless of how deeply each character is nested (AntsRow/NormalAntN/Body,
/// Clown/ClownBody, etc.) without this script needing to know or care about that hierarchy's local
/// offsets.
///
/// The cup's last two legs (Normal ant #5 -> player ant -> clown-or-stack) both fire only off
/// GameManager.OnJudgement, deliberately NOT off CallDirector.OnResponsePress. OnResponsePress fires
/// on every physical press regardless of whether it lands in a live note's window (see CallDirector —
/// that's by design, so reactive visuals like Stage1WaveRideVisuals' ant pulse always respond to input
/// even during its mash-guard cooldown). The cup doesn't want that: an early/stray press during the
/// Normal-ant Call phase isn't a real response attempt, so hooking the cup to OnResponsePress made it
/// hop to the player ant on presses that were never actually judged. OnJudgement only ever fires for a
/// press CallDirector accepted as a genuine claim on the live Response note (see CallDirector.Resolve),
/// so chaining both hops off that single event guarantees the cup only ever moves in lockstep with
/// real judged input — same guarantee Stage1WaveRideVisuals's judged reactions get.
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
    [SerializeField] Image[] normalAntImages = new Image[5];
    [SerializeField] Sprite antIdleSprite;
    [SerializeField] Sprite antPassSprite;

    [Header("Player ant — Response note")]
    [SerializeField] Image playerAntImage;
    [SerializeField] Sprite playerAntIdleSprite;
    [SerializeField] Sprite playerAntPassSprite;
    [SerializeField] Sprite playerAntFailSprite;

    [Header("Clown")]
    [SerializeField] Image clownImage;
    [SerializeField] Sprite clownNormalSprite;
    [SerializeField] Sprite clownDrinkSprite;
    [SerializeField] Sprite clownFailSprite;

    [Header("Miss 전용 연출 (해당 그림 없음 — 물리적 반응으로 표현)")]
    [SerializeField] GameObject questionMark;

    [Header("떠다니는 컵 풀 — 콜이 겹칠 수 있어서 컵도 동시에 여러 개 날아올 수 있음")]
    [Tooltip("동시에 날아다닐 수 있는 컵 최대 개수. 겹치는 콜이 이보다 많으면 슬롯을 재사용함.")]
    [SerializeField] Image[] cupPool = new Image[4];
    [Tooltip("평소(대기) 상태 스프라이트")]
    [SerializeField] Sprite cupIdleSprite;
    [Tooltip("옆으로 튈 때(비행 중) 스프라이트")]
    [SerializeField] Sprite cupThrownSprite;
    [Tooltip("마지막 플레이어 개미->광대 구간에서 박자를 성공했을 때 쓰는 컵 스프라이트")]
    [SerializeField] Sprite cupSuccessSprite;
    [Tooltip("마지막 플레이어 개미->광대 구간에서 Miss일 때 쓰는 컵 스프라이트")]
    [SerializeField] Sprite cupFailSprite;
    [Tooltip("한 번 튀어서 다음 개미에게 도착하기까지 걸리는 시간")]
    [SerializeField] float cupHopTime   = 0.22f;
    [Tooltip("튈 때 위로 솟구치는 높이(배구공처럼 통통 튀는 느낌)")]
    [SerializeField] float cupHopHeight = 60f;
    [Tooltip("광대(또는 실패 시 컵 무더기)에 도착한 뒤 컵이 사라지기까지 대기 시간")]
    [SerializeField] float cupVanishDelay = 0.3f;
    [Tooltip("컵이 각 캐릭터 위로 얼마나 떠서 이동할지(개미/광대 등 목표 위치에 더해지는 오프셋). " +
             "인스펙터에서 이 값만 바꾸면 되고, 컵 오브젝트 자체를 직접 옮겨도 다음 튕김에서 다시 " +
             "덮어써짐.")]
    [SerializeField] Vector2 cupOffsetFromTarget = new Vector2(0f, 90f);

    [Header("성공할 때마다 광대 옆에 쌓이는 컵 탑")]
    [SerializeField] Image cupStackBaseImage;
    [SerializeField] Image[] cupStackTopImages = new Image[6];

    [Header("Tuning")]
    [Tooltip("개미/플레이어/광대가 pass·drink·fail 스프라이트로 바뀐 채 유지되는 시간")]
    [SerializeField] float spritePulseTime  = 0.18f;
    [SerializeField] float questionMarkTime = 0.6f;

    int _callCueCount;
    int _successStackCount;
    int _activeCupSlot = -1; // which pool slot the currently-firing group's calls are hopping
    readonly Queue<int> _pendingResponseSlots = new Queue<int>(); // FIFO: oldest unjudged group's slot
    Coroutine _questionMarkCr;

    // "Busy" handles — non-null while that part's animation is still playing. A new trigger while
    // busy is dropped (not queued, not restarted) so the current cycle always finishes cleanly.
    Coroutine[] _antCrs;
    Coroutine[] _cupCrs; // parallel to cupPool
    Coroutine   _playerCr;
    Coroutine   _clownCr;

    void Awake()
    {
        _antCrs = new Coroutine[normalAntImages != null ? normalAntImages.Length : 0];
        _cupCrs = new Coroutine[cupPool != null ? cupPool.Length : 0];
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
        if (cupStackBaseImage != null) cupStackBaseImage.gameObject.SetActive(false);
        if (cupStackTopImages != null)
            foreach (var icon in cupStackTopImages)
                if (icon != null) icon.gameObject.SetActive(false);

        if (normalAntImages != null)
            foreach (var img in normalAntImages)
                if (img != null) img.sprite = antIdleSprite;

        if (playerAntImage != null) playerAntImage.sprite = playerAntIdleSprite;
        if (clownImage != null) clownImage.sprite = clownNormalSprite;

        if (cupPool != null)
            foreach (var cup in cupPool)
            {
                if (cup == null) continue;
                cup.sprite = cupIdleSprite;
                cup.gameObject.SetActive(false);
            }
    }

    void HandleCallCue()
    {
        if (normalAntImages == null || normalAntImages.Length == 0) return;
        int antIndex = _callCueCount % normalAntImages.Length;
        _callCueCount++;

        var img = normalAntImages[antIndex];
        if (img != null && _antCrs[antIndex] == null)
        {
            int capturedIndex = antIndex;
            _antCrs[antIndex] = StartCoroutine(
                SpritePulseRoutine(img, antIdleSprite, antPassSprite, spritePulseTime, () => _antCrs[capturedIndex] = null));
        }

        if (img == null || cupPool == null || cupPool.Length == 0) return;

        // First cue of a new group claims a fresh free pool slot — does NOT touch whatever slot a
        // still-finishing previous group's cup owns, so overlapping groups animate independently.
        // Enqueued so HandleJudgement can later recover the right slot even if this group's own
        // Response doesn't get judged until after the *next* group's calls have already started
        // (which would otherwise have moved _activeCupSlot on).
        if (antIndex == 0)
        {
            _activeCupSlot = FindFreeCupSlot();
            _pendingResponseSlots.Enqueue(_activeCupSlot);
        }

        BounceCupToSlot(_activeCupSlot, img.rectTransform, cupThrownSprite);
    }

    void HandleJudgement(JudgementType j)
    {
        bool isHit = j == JudgementType.Hit;

        if (playerAntImage != null && _playerCr == null)
            _playerCr = StartCoroutine(SpritePulseRoutine(
                playerAntImage, playerAntIdleSprite, isHit ? playerAntPassSprite : playerAntFailSprite,
                spritePulseTime, () => _playerCr = null));

        if (clownImage != null && _clownCr == null)
            _clownCr = StartCoroutine(SpritePulseRoutine(
                clownImage, clownNormalSprite, isHit ? clownDrinkSprite : clownFailSprite,
                spritePulseTime, () => _clownCr = null));

        // Cup's last two legs, both gated on this same judged event (see class doc for why not
        // OnResponsePress): first a quick relay hop into the player ant's hands, then on from there —
        // on Hit it arcs into the clown (mouth/body) and gets "drunk"; on Miss it does NOT reach the
        // clown at all, instead thrown wide toward the cup-stack area (cupStackBaseImage's spot) and
        // dropped there, landing near but not adding to the neat paper-cup stack (that only grows on
        // genuine Hits — see RevealNextStackCup). Uses the oldest still-unjudged group's slot (see
        // class doc for why that's _pendingResponseSlots.Dequeue() and not _activeCupSlot directly).
        if (playerAntImage != null)
        {
            int slot = _pendingResponseSlots.Count > 0 ? _pendingResponseSlots.Dequeue() : _activeCupSlot;
            var finalTarget = isHit ? clownImage : cupStackBaseImage;
            BounceCupChainSlot(
                slot, playerAntImage.rectTransform, cupThrownSprite,
                finalTarget != null ? finalTarget.rectTransform : null, isHit ? cupSuccessSprite : cupFailSprite);
        }

        if (isHit) RevealNextStackCup();
        else       ShowQuestionMark();
    }

    IEnumerator SpritePulseRoutine(Image img, Sprite idleSprite, Sprite activeSprite, float duration, Action onComplete)
    {
        img.sprite = activeSprite;
        yield return new WaitForSeconds(duration);
        img.sprite = idleSprite;
        onComplete?.Invoke();
    }

    // First inactive (not mid-flight) slot; if every slot is busy (pool exhausted — more concurrent
    // relays than cupPool has room for) falls back to slot 0 rather than doing nothing, since a
    // slightly-early redirect is a smaller glitch than a group's cup never appearing at all.
    int FindFreeCupSlot()
    {
        if (cupPool == null) return -1;
        for (int i = 0; i < cupPool.Length; i++)
            if (cupPool[i] != null && !cupPool[i].gameObject.activeSelf) return i;
        return cupPool.Length > 0 ? 0 : -1;
    }

    void BounceCupToSlot(int slot, RectTransform target, Sprite duringSprite, bool vanishAfter = false)
    {
        if (slot < 0 || cupPool == null || slot >= cupPool.Length || cupPool[slot] == null || target == null) return;
        if (_cupCrs[slot] != null) StopCoroutine(_cupCrs[slot]); // redirect mid-hop rather than drop the new beat
        _cupCrs[slot] = StartCoroutine(BounceCupRoutine(slot, target, duringSprite, vanishAfter));
    }

    IEnumerator BounceCupRoutine(int slot, RectTransform target, Sprite duringSprite, bool vanishAfter)
    {
        yield return HopOnce(cupPool[slot], target, duringSprite);

        if (vanishAfter)
        {
            yield return new WaitForSeconds(cupVanishDelay);
            cupPool[slot].gameObject.SetActive(false);
        }
        else
        {
            cupPool[slot].sprite = cupIdleSprite;
        }
        _cupCrs[slot] = null;
    }

    // Two hops back-to-back in one coroutine (so the whole relay is a single owned coroutine,
    // redirectable as one unit) — used for the player-ant-then-onward relay, which must only ever
    // start from a single gating event (see HandleJudgement) rather than two independently-triggered
    // hops.
    void BounceCupChainSlot(int slot, RectTransform target1, Sprite sprite1, RectTransform target2, Sprite sprite2)
    {
        if (slot < 0 || cupPool == null || slot >= cupPool.Length || cupPool[slot] == null || target1 == null) return;
        if (_cupCrs[slot] != null) StopCoroutine(_cupCrs[slot]);
        _cupCrs[slot] = StartCoroutine(BounceCupChainRoutine(slot, target1, sprite1, target2, sprite2));
    }

    IEnumerator BounceCupChainRoutine(int slot, RectTransform target1, Sprite sprite1, RectTransform target2, Sprite sprite2)
    {
        yield return HopOnce(cupPool[slot], target1, sprite1);

        if (target2 != null)
        {
            yield return HopOnce(cupPool[slot], target2, sprite2);
            yield return new WaitForSeconds(cupVanishDelay);
            cupPool[slot].gameObject.SetActive(false);
        }
        else
        {
            cupPool[slot].sprite = cupIdleSprite;
        }
        _cupCrs[slot] = null;
    }

    // Volleyball-style single hop: lerps world position start->target while adding a sine-arc vertical
    // bump, using RectTransform.position (world space) so it's correct regardless of how each
    // character is nested/parented — no need to know or convert between local anchoredPosition spaces
    // per target. Leaves cup.sprite set to duringSprite when it returns; callers decide what happens
    // next (idle, another hop, or vanish).
    IEnumerator HopOnce(Image cup, RectTransform target, Sprite duringSprite)
    {
        if (!cup.gameObject.activeSelf) cup.gameObject.SetActive(true);

        var rect = cup.rectTransform;
        Vector3 startPos = rect.position;
        Vector3 endPos   = target.position + (Vector3)cupOffsetFromTarget;
        cup.sprite       = duringSprite;

        float t = 0f;
        while (t < cupHopTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / cupHopTime);
            Vector3 pos = Vector3.Lerp(startPos, endPos, p);
            pos.y += Mathf.Sin(p * Mathf.PI) * cupHopHeight;
            rect.position = pos;
            yield return null;
        }
        rect.position = endPos;
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

    // First success shows the base cup; every success after that reveals one more stacked cup on top.
    void RevealNextStackCup()
    {
        if (_successStackCount == 0 && cupStackBaseImage != null)
            cupStackBaseImage.gameObject.SetActive(true);
        else if (cupStackTopImages != null)
        {
            int topIndex = _successStackCount - 1;
            if (topIndex >= 0 && topIndex < cupStackTopImages.Length && cupStackTopImages[topIndex] != null)
                cupStackTopImages[topIndex].gameObject.SetActive(true);
        }
        _successStackCount++;
    }
}
