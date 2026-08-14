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
/// The traveling cup is a POOL of Image instances (cupPool), not a single object — CallPatternData's
/// calls are explicitly allowed to overlap in time, up to and including several relays running
/// staggered one-ant-apart (a continuous stream of cups, not just "next group starts before this
/// one's Response is judged"). That ruled out the trick Stage1WaveRideVisuals uses (infer "which
/// note" from a firing-order counter, antIndex = runningCount % 5): that only works when one group's
/// calls always finish spawning before the next group's start, which genuinely-interleaved relays
/// violate — a plain counter can't tell which relay a given OnCallCue belongs to once two groups'
/// individual calls start interleaving (this was a real, confirmed bug: cups would visibly skip an
/// ant or drift to the wrong slot once overlap got dense). Fixed by having CallDirector's events
/// carry the entry's actual groupId (OnCallCueGroup/OnGroupJudged, added specifically for this — see
/// CallDirector.cs, additive, doesn't touch the original OnCallCue/OnResponsePress that
/// Stage1WaveRideVisuals/Stage1WaterPourVisuals still rely on). _groupCallCounts tracks each group's
/// own call index independently (a Dictionary<groupId, count>, not one shared counter), and
/// _groupCupSlot maps each groupId to the pool slot claimed the first time that group's calls are
/// seen — both keyed by the real groupId, so interleaving other groups' calls in between can't
/// desync either one.
///
/// World-space RectTransform.position is used for each hop's start/end (not anchoredPosition), so it
/// works correctly regardless of how deeply each character is nested (AntsRow/NormalAntN/Body,
/// Clown/ClownBody, etc.) without this script needing to know or care about that hierarchy's local
/// offsets.
///
/// The cup's last two legs (Normal ant #5 -> player ant -> clown-or-stack) both fire only off
/// CallDirector.OnGroupJudged, deliberately NOT off OnResponsePress. OnResponsePress fires on every
/// physical press regardless of whether it lands in a live note's window (see CallDirector — that's
/// by design, so reactive visuals like Stage1WaveRideVisuals' ant pulse always respond to input even
/// during its mash-guard cooldown). The cup doesn't want that: an early/stray press during the
/// Normal-ant Call phase isn't a real response attempt, so hooking the cup to OnResponsePress made it
/// hop to the player ant on presses that were never actually judged. OnGroupJudged only ever fires for
/// a press CallDirector accepted as a genuine claim on a live Response note (see CallDirector.Resolve),
/// so chaining both hops off that single event guarantees the cup only ever moves in lockstep with
/// real judged input, and its groupId payload means the correct group's slot is used directly — no
/// FIFO-queue-of-pending-groups guesswork needed (a first version tried that; it broke the same way
/// the counter did once groups genuinely interleaved instead of just nesting cleanly).
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
    [SerializeField] Image[] cupPool = new Image[6];
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
    [Tooltip("성공(Hit)해서 컵이 광대에게 도착하는 마지막 홉에서만 쓰는 별도 오프셋. 위 " +
             "cupOffsetFromTarget을 개미 피벗 변경에 맞춰 올렸더니 광대 쪽으로 갈 때는 머리 위로 " +
             "너무 높이 가버려서 분리함 — 이 값만 낮춰서 광대 '입' 위치에 맞추면 됨. 다른 홉(개미 " +
             "릴레이, 플레이어 개미까지)은 전부 위 cupOffsetFromTarget 그대로 씀.")]
    [SerializeField] Vector2 cupOffsetAtClown = new Vector2(0f, 40f);
    [Tooltip("실패(Miss)해서 컵이 오른쪽 아래 컵무더기 쪽으로 도착하는 마지막 홉에서만 쓰는 별도 " +
             "오프셋. cupOffsetAtClown과 마찬가지로 위 cupOffsetFromTarget과 분리되어 있어서, " +
             "컵무더기 위치에 맞게 이 값만 따로 조절 가능함(다른 홉엔 영향 없음). 기본값은 " +
             "cupOffsetFromTarget과 동일하게 맞춰둠 — 조절 전까진 기존과 동일하게 보임.")]
    [SerializeField] Vector2 cupOffsetAtStack = new Vector2(0f, 90f);

    [Header("실패(Miss)할 때마다 오른쪽 아래에 쌓이는 컵 무더기")]
    [SerializeField] Image cupStackBaseImage;
    [SerializeField] Image[] cupStackTopImages = new Image[6];

    [Header("Tuning")]
    [Tooltip("개미/플레이어/광대가 pass·drink·fail 스프라이트로 바뀐 채 유지되는 시간")]
    [SerializeField] float spritePulseTime  = 0.18f;
    [SerializeField] float questionMarkTime = 0.6f;
    [Tooltip("일반 개미가 pass할 때 Body의 Y 스케일이 순간적으로 커지는 목표값(대기 상태 스케일에서 " +
             "여기까지 부풀었다가 다시 대기 스케일로 돌아옴 — 실제로 컵을 건네는 듯한 통통 튀는 " +
             "느낌). 각 개미의 원래 대기 스케일(X/Z 포함)은 Awake 시점에 그대로 읽어서 기준으로 " +
             "쓰므로, 개미마다 씬에 다르게 찍혀있어도 그 값 기준으로 부풀었다가 정확히 그 값으로 " +
             "돌아감.")]
    [SerializeField] float antPassScaleY = 1.15f;
    [Tooltip("PlayerAnt Body가 입력할 때마다(Call 구간 포함, 실제 판정 여부와 무관하게) 순간적으로 " +
             "커지는 Y 스케일 목표값. 판정(Hit/Miss) 자체는 이 반응과 별개로 CallDirector의 " +
             "Hit/Miss 윈도우 안에서만 이뤄짐 — 이건 그냥 '입력을 시도했다'는 물리적 반응이라 " +
             "콜 구간 중에 눌러도(아직 판정 대상이 아니어도) PlayerAnt가 움찔거리게 해줌.")]
    [SerializeField] float playerPressScaleY = 1.15f;

    int _successStackCount;
    // Per-group state, keyed by the real groupId (not a shared running counter — see class doc).
    readonly Dictionary<int, int> _groupCallCounts = new Dictionary<int, int>(); // groupId -> calls seen so far
    readonly Dictionary<int, int> _groupCupSlot     = new Dictionary<int, int>(); // groupId -> pool slot claimed
    Coroutine _questionMarkCr;

    // "Busy" handles — non-null while that part's animation is still playing. A new trigger while
    // busy is dropped (not queued, not restarted) so the current cycle always finishes cleanly.
    Coroutine[] _antCrs;
    Coroutine[] _cupCrs; // parallel to cupPool
    Coroutine   _playerCr;
    Coroutine   _clownCr;
    // Separate busy-handle from _playerCr on purpose — this one only ever touches localScale
    // (never sprite), so it can run fully independently of the pass/fail sprite pulse below and
    // never gets dropped by (or drops) that pulse's own busy guard. See HandlePlayerResponsePress.
    Coroutine   _playerScaleCr;

    // Each ant's authored idle localScale (X/Z preserved, only Y gets tweened up to antPassScaleY
    // and back) — read once so the pulse always returns to exactly what was authored per-ant in the
    // scene, rather than assuming every ant shares one hardcoded baseline.
    Vector3[] _antBaseScale;
    Vector3   _playerBaseScale;

    // The Canvas all these UI Images actually live under — cached because this component itself
    // isn't necessarily a child of it (GetComponentInParent would miss), so it's read off one of the
    // Graphics we already reference instead. See ScaledOffset for why this matters.
    Canvas _canvas;

    void Awake()
    {
        _antCrs = new Coroutine[normalAntImages != null ? normalAntImages.Length : 0];
        _cupCrs = new Coroutine[cupPool != null ? cupPool.Length : 0];

        _antBaseScale = new Vector3[_antCrs.Length];
        for (int i = 0; i < _antBaseScale.Length; i++)
            if (normalAntImages[i] != null)
                _antBaseScale[i] = normalAntImages[i].rectTransform.localScale;

        if (playerAntImage != null) _playerBaseScale = playerAntImage.rectTransform.localScale;

        _canvas = playerAntImage != null ? playerAntImage.canvas
                : normalAntImages != null && normalAntImages.Length > 0 ? normalAntImages[0]?.canvas
                : null;
    }

    void OnEnable()
    {
        if (callDirector != null)
        {
            callDirector.OnCallCueGroup  += HandleCallCueGroup;
            callDirector.OnGroupJudged   += HandleGroupJudged;
            // Fires on every physical press regardless of whether it landed on a live/judged note
            // (see CallDirector.OnResponsePress) — used here purely so PlayerAnt visually reacts to
            // every input attempt, including ones during the Call phase before its own Response note
            // has even spawned. Actual Hit/Miss judgement is untouched by this — that still only
            // happens inside CallDirector's Hit/Miss windows via HandleGroupJudged below.
            callDirector.OnResponsePress += HandlePlayerResponsePress;
        }
    }

    void OnDisable()
    {
        if (callDirector != null)
        {
            callDirector.OnCallCueGroup  -= HandleCallCueGroup;
            callDirector.OnGroupJudged   -= HandleGroupJudged;
            callDirector.OnResponsePress -= HandlePlayerResponsePress;
        }
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

    void HandleCallCueGroup(int groupId)
    {
        if (normalAntImages == null || normalAntImages.Length == 0) return;

        _groupCallCounts.TryGetValue(groupId, out int countSoFar);
        int antIndex = countSoFar % normalAntImages.Length;
        bool isFirstCallOfGroup = countSoFar == 0;
        _groupCallCounts[groupId] = countSoFar + 1;

        var img = normalAntImages[antIndex];
        if (img != null && _antCrs[antIndex] == null)
        {
            int capturedIndex = antIndex;
            _antCrs[antIndex] = StartCoroutine(
                AntPulseRoutine(capturedIndex, img, spritePulseTime, () => _antCrs[capturedIndex] = null));
        }

        if (img == null || cupPool == null || cupPool.Length == 0) return;

        if (isFirstCallOfGroup)
        {
            // Claim a fresh free pool slot for this group specifically — does NOT touch whatever
            // slot some other still-in-flight group's cup owns, so overlapping/interleaved groups
            // animate independently. Snap straight to this ant's spot first (rather than animating
            // in from wherever this pool slot last was left, which could be an unrelated stale
            // position) so the group's first hop reads as a clean "pop in," matching every
            // subsequent hop's landing-then-resting behavior.
            int slot = FindFreeCupSlot();
            _groupCupSlot[groupId] = slot;
            if (slot >= 0 && cupPool[slot] != null)
            {
                cupPool[slot].gameObject.SetActive(true);
                cupPool[slot].rectTransform.position = img.rectTransform.position + ScaledOffset(cupOffsetFromTarget);
                cupPool[slot].sprite = cupIdleSprite;
            }
        }

        if (_groupCupSlot.TryGetValue(groupId, out int cupSlot))
            BounceCupToSlot(cupSlot, img.rectTransform, cupThrownSprite);
    }

    // Fires on every physical press, independent of whether CallDirector actually judged it — see
    // OnResponsePress subscription in OnEnable. Purely a "the player tried something" physical
    // twitch (scale only, no sprite change), so it plays even for a press during the Call phase
    // that's nowhere near a live Response window. Uses its own busy-handle (_playerScaleCr, not
    // _playerCr) so it never competes with or gets starved by the pass/fail sprite pulse in
    // HandleGroupJudged, which fires separately (and only within the Hit/Miss windows).
    void HandlePlayerResponsePress()
    {
        if (playerAntImage == null || _playerScaleCr != null) return;
        _playerScaleCr = StartCoroutine(ScalePulseRoutine(
            playerAntImage.rectTransform, _playerBaseScale, playerPressScaleY, spritePulseTime,
            () => _playerScaleCr = null));
    }

    void HandleGroupJudged(int groupId, JudgementType j)
    {
        bool isHit = j == JudgementType.Hit;

        if (playerAntImage != null && _playerCr == null)
            _playerCr = StartCoroutine(SpritePulseRoutine(
                playerAntImage, playerAntIdleSprite, isHit ? playerAntPassSprite : playerAntFailSprite,
                spritePulseTime, () => _playerCr = null));

        // On Miss the clown's disappointed reaction isn't tied to the cup at all (the cup is flying
        // to the pile corner, nowhere near the clown), so it still plays immediately here. On Hit,
        // playing clownDrinkSprite right now would show the clown drinking before the cup has even
        // left the player ant's hands — that leg alone is fired below, from PlayClownDrinkPulse,
        // only once the cup's second hop actually lands at the clown's mouth (see cupOffsetAtClown).
        if (!isHit && clownImage != null && _clownCr == null)
            _clownCr = StartCoroutine(SpritePulseRoutine(
                clownImage, clownNormalSprite, clownFailSprite,
                spritePulseTime, () => _clownCr = null));

        // Cup's last two legs, both gated on this same judged event (see class doc for why not
        // OnResponsePress): first a quick relay hop into the player ant's hands, then on from there —
        // on Hit it arcs into the clown (mouth/body) and gets "drunk", nothing left behind; on Miss
        // it does NOT reach the clown at all, instead thrown wide toward the bottom-right cup-pile
        // area (cupStackBaseImage's spot) and left there — that pile is a running tally of *failed*
        // cups (grows only on Miss, see RevealNextStackCup below), not a trophy case of successes.
        // groupId comes straight from CallDirector, so this is always exactly this group's own cup
        // slot — no cross-group ambiguity possible even with several relays in flight at once.
        if (playerAntImage != null && _groupCupSlot.TryGetValue(groupId, out int slot))
        {
            var finalTarget = isHit ? clownImage : cupStackBaseImage;
            BounceCupChainSlot(
                slot, playerAntImage.rectTransform, cupThrownSprite,
                finalTarget != null ? finalTarget.rectTransform : null, isHit ? cupSuccessSprite : cupFailSprite,
                isHit ? cupOffsetAtClown : cupOffsetAtStack,
                isHit ? PlayClownDrinkPulse : null);
        }

        if (!isHit)
        {
            RevealNextStackCup();
            ShowQuestionMark();
        }
    }

    // Fired from BounceCupChainRoutine the instant the cup's second hop actually lands at the
    // clown's mouth (see cupOffsetAtClown) — not from HandleGroupJudged directly — so the "drink"
    // sprite only appears once the cup visually arrives, instead of at the moment of judgement while
    // the cup is still mid-flight from the player ant.
    void PlayClownDrinkPulse()
    {
        if (clownImage != null && _clownCr == null)
            _clownCr = StartCoroutine(SpritePulseRoutine(
                clownImage, clownNormalSprite, clownDrinkSprite,
                spritePulseTime, () => _clownCr = null));
    }

    IEnumerator SpritePulseRoutine(Image img, Sprite idleSprite, Sprite activeSprite, float duration, Action onComplete)
    {
        img.sprite = activeSprite;
        yield return new WaitForSeconds(duration);
        img.sprite = idleSprite;
        onComplete?.Invoke();
    }

    // Ant-only pulse: sprite swap (same as SpritePulseRoutine) plus a Y-scale "squash toward peak and
    // back" over the same duration (see ScalePulseRoutine), so the pass reads as a physical hand-off
    // instead of just a frame swap. Kept separate from SpritePulseRoutine (rather than adding a scale
    // param there) since the clown still uses the plain sprite-only pulse.
    IEnumerator AntPulseRoutine(int index, Image img, float duration, Action onComplete)
    {
        img.sprite = antPassSprite;
        yield return ScalePulseRoutine(img.rectTransform, _antBaseScale[index], antPassScaleY, duration, null);
        img.sprite = antIdleSprite;
        onComplete?.Invoke();
    }

    // Pure Y-scale "squash toward peak and back" tween (X/Z left untouched), no sprite involved —
    // shared by AntPulseRoutine (wrapped with a sprite swap) and HandlePlayerResponsePress (used on
    // its own, since that one must never touch sprite — see its own comment). Sine ease (0 -> 1 -> 0
    // across duration) so it eases in and out instead of linearly snapping to peakY and back.
    IEnumerator ScalePulseRoutine(RectTransform rect, Vector3 baseScale, float peakY, float duration, Action onComplete)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ease = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI); // 0 -> 1 -> 0
            rect.localScale = new Vector3(baseScale.x, Mathf.Lerp(baseScale.y, peakY, ease), baseScale.z);
            yield return null;
        }

        rect.localScale = baseScale;
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
        yield return HopOnce(cupPool[slot], target, duringSprite, cupOffsetFromTarget);

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
    // hops. offset2 is the caller's choice for the SECOND hop only (the first, into the player ant's
    // hands, always uses the shared cupOffsetFromTarget) — lets HandleGroupJudged swap in
    // cupOffsetAtClown/cupOffsetAtStack for that leg without affecting anything else. onSecondHopLanded
    // (optional) fires the instant the cup actually arrives at target2 — used for the clown's "drink"
    // reaction, which must wait for the cup to visually get there rather than firing at judgement time.
    void BounceCupChainSlot(int slot, RectTransform target1, Sprite sprite1, RectTransform target2, Sprite sprite2, Vector2 offset2, Action onSecondHopLanded)
    {
        if (slot < 0 || cupPool == null || slot >= cupPool.Length || cupPool[slot] == null || target1 == null) return;
        if (_cupCrs[slot] != null) StopCoroutine(_cupCrs[slot]);
        _cupCrs[slot] = StartCoroutine(BounceCupChainRoutine(slot, target1, sprite1, target2, sprite2, offset2, onSecondHopLanded));
    }

    IEnumerator BounceCupChainRoutine(int slot, RectTransform target1, Sprite sprite1, RectTransform target2, Sprite sprite2, Vector2 offset2, Action onSecondHopLanded)
    {
        yield return HopOnce(cupPool[slot], target1, sprite1, cupOffsetFromTarget);

        if (target2 != null)
        {
            yield return HopOnce(cupPool[slot], target2, sprite2, offset2);
            onSecondHopLanded?.Invoke();
            yield return new WaitForSeconds(cupVanishDelay);
            cupPool[slot].gameObject.SetActive(false);
        }
        else
        {
            cupPool[slot].sprite = cupIdleSprite;
        }
        _cupCrs[slot] = null;
    }

    // this canvas is Screen Space - Overlay with CanvasScaler set to "Scale With Screen Size" (see
    // ScaledOffset) — RectTransform.position for its children already equals real on-screen pixels
    // at whatever the CURRENT Game view resolution happens to be, so a raw offset added to it (e.g.
    // "float up 90") means a fixed 90 *actual pixels* regardless of resolution. Everything else in
    // the canvas (character size, spacing, ...) scales with canvas.scaleFactor as the Game view is
    // resized (windowed vs. maximized are literally different resolutions), so a fixed-pixel offset
    // drifts relative to the characters at any resolution other than the 1920x1080 reference — this
    // was a real reported bug (cup position differing between normal and maximized Game view).
    // Multiplying by scaleFactor here makes these offsets scale in lockstep with everything else.
    Vector3 ScaledOffset(Vector2 rawOffset) => (Vector3)(rawOffset * (_canvas != null ? _canvas.scaleFactor : 1f));

    // Volleyball-style single hop: lerps world position start->target while adding a sine-arc vertical
    // bump, using RectTransform.position (world space) so it's correct regardless of how each
    // character is nested/parented — no need to know or convert between local anchoredPosition spaces
    // per target. Leaves cup.sprite set to duringSprite when it returns; callers decide what happens
    // next (idle, another hop, or vanish).
    IEnumerator HopOnce(Image cup, RectTransform target, Sprite duringSprite, Vector2 offset)
    {
        if (!cup.gameObject.activeSelf) cup.gameObject.SetActive(true);

        var rect = cup.rectTransform;
        Vector3 startPos = rect.position;
        Vector3 endPos   = target.position + ScaledOffset(offset);
        cup.sprite       = duringSprite;
        float hopHeight  = cupHopHeight * (_canvas != null ? _canvas.scaleFactor : 1f);

        float t = 0f;
        while (t < cupHopTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / cupHopTime);
            Vector3 pos = Vector3.Lerp(startPos, endPos, p);
            pos.y += Mathf.Sin(p * Mathf.PI) * hopHeight;
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

    // First Miss shows the base cup; every Miss after that reveals one more stacked cup on top —
    // a growing pile of failures, not a trophy stack.
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
