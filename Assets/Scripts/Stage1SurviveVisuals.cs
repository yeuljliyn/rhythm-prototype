using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 1-3 (존버) visual glue, v2 (replaces the "shower sweeps over pairs" design — see git
/// history for that first attempt, abandoned after it turned out not to match the intended feel).
///
/// Three characters left-to-right: Clown, NormalAnt, PlayerAnt. Default state is everyone submerged
/// (shower ON) with the player physically holding the button down — "존버" (endure/hold on) is meant
/// literally here. Each 4-beat cycle:
///   beat 1: Clown surfaces to breathe (Call, Tap — just a timing point, not judged)
///   beat 2: NormalAnt surfaces to breathe (Call, Tap)
///   beat 3: PlayerAnt surfaces — this is the player RELEASING the held button (Response, judged)
///   beat 4 (= next cycle's start): everyone dives back down together, player presses again to arm
///           the next cycle's Hold
///
/// Chart shape per cycle (4 CallEntry objects, same groupId) — deliberately NOT using Hold for
/// Clown/NormalAnt: they're never judged, and "how long they stay surfaced" is driven by real events
/// (their own cue, then the cycle's dedicated beat-4 cue below), not a fixed duration, so a Hold's
/// releaseBeat would be redundant data. Only the player's note needs Hold semantics, because that's
/// the one thing actually physically held down by a real press:
///
///   Clown:      Tap,  Call,     cueBeat = beat 1
///   NormalAnt:  Tap,  Call,     cueBeat = beat 2
///   Player:     Hold, Response, cueBeat = this cycle's start (= previous cycle's beat 4), releaseBeat = beat 3
///   "Beat4Cue": Tap,  Call,     cueBeat = beat 4 (= next cycle's start)
///
/// The 4th entry ("beat4Cue") exists SPECIFICALLY so Clown/NormalAnt/shower re-submerge on the chart's
/// own fixed schedule, not on the player's personal input timing — an earlier version instead hung
/// this off CallDirector.OnHoldArmed (the player's own press-arm event), reasoning that it always
/// coincides with beat 4 when the player's timing is correct. That was wrong: it makes Clown/NormalAnt
/// dive at whatever moment the player happens to press, not necessarily beat 4 — confirmed via actual
/// play-testing ("플레이어가 박자가 틀리면 광대나 개미가 잠수해야 할 때가 아닌데 잠수를 해버림").
/// Everything demo-visual now stays driven purely by chart-timed Call-role cues, matching every other
/// Stage1 minigame's "the event decides when, the coroutine only decides how" principle.
///
/// Event wiring:
///   - CallDirector.OnCallCueGroup (fires once per Call-role entry spawn): idx 0 = Clown surfaces,
///     idx 1 = NormalAnt surfaces, idx 2 = the beat4Cue — shower flips back "on" and Clown/NormalAnt
///     dive back down together. Also idx 0 is when the shower sprite flips to "off" (it stays off for
///     the rest of the cycle), and each case ALSO starts the *next* character's head-bubble one beat
///     early — see the bubble paragraph below.
///   - CallDirector.OnResponsePress / OnResponseRelease: Y position is a direct, unconditional,
///     always-immediate mirror of the held button — press always sets the dive Y, release always sets
///     the surface Y, full stop, nothing ever delays or blocks it (explicit request: "위치 움직이는건
///     그냥 플레이어 입력으로만 영향 받았음 좋겠는데"). The sprite normally mirrors position the same
///     way too, EXCEPT while PlayerFailFlash is running. This replaced an even earlier version driven
///     by OnHoldArmed (only fires on a *successful* arm) and OnGroupJudged/OnHoldEarlyRelease (only
///     fire once the note actually resolves): a press or release that didn't land inside that cycle's
///     live/claimable window fired none of those events, leaving the player stuck in whatever pose its
///     last successful event left it in — reported as "타이밍 안 맞게 물 밖으로 나오면 다시 물속으로
///     안 들어가지는."
///   - CallDirector.OnGroupJudged: Hit -> nothing (position was already correct — the same release
///     that earned the Hit already moved the player, and its bubble was already hidden by that same
///     release). Miss -> PlayerFailFlash pins the *sprite* to playerFailSprite for failSpriteHoldTime,
///     but deliberately never touches Y — an
///     earlier version routed both dive/surface and fail through one shared state machine that also
///     locked position for the whole fail duration, so diving/surfacing during that window did nothing
///     visible until the flash ended (reported: "실패 스프라이트 나올때... 위치가 고정돼?"). Now Y
///     always tracks input live underneath the fail art, and the sprite just snaps to match wherever
///     that ended up once the flash's timer runs out.
///     CallDirector.OnResponseWasted (a press/release that claimed/armed nothing live at all) was
///     briefly wired to also trigger a fail flash, so off-beat diving/surfacing — not just missing the
///     one judged response — would read as a mistake too. Reverted: it fires on almost every press/
///     release during mashing (mash timing rarely lands in a real claimable window), so it kept
///     re-triggering the fail flash and blotting out the dive/surface toggle almost permanently —
///     reported as the toggle "disappearing again." Left unsubscribed here; a near-miss press or an
///     early release still get their own Miss via OnGroupJudged regardless.
///
/// Bubble anticipation cue ("올라오기 직전마다 머리 위에 기포가 뽀로록"), v2 — replaces the earlier
/// procedural placeholder (soft round dots generated at runtime) now that real art exists
/// (Assets/1-3/1-3 기포.png). Spec from the planner, verbatim behavior:
///   - Clown/NormalAnt: their bubble appears exactly 1 beat before *their own* surface cue, and
///     disappears the instant they actually surface.
///   - PlayerAnt (Response): its bubble appears 1 beat before the beat they need to surface by — but
///     ONLY if they're still submerged at that moment (they may have already surfaced early on a
///     mistimed release, in which case there's nothing left to warn them about). It disappears the
///     instant they surface too, for the same reason a bubble floating above someone already out of
///     the water would look wrong ("기포가 물밖에 있으면 좀 이상하잖아").
/// Implementation: each of Clown/NormalAnt/Player surfaces exactly 1 beat after the character *before*
/// them in the cycle (Clown@beat1, NormalAnt@beat2, Player releases@beat3), and the beat4Cue itself
/// lands exactly 1 beat before the *next* cycle's Clown@beat1. So instead of computing "beat - 1"
/// against the chart directly, each OnCallCueGroup case in HandleCallCueGroup does double duty: it
/// drives its own character's surfacing AND starts the *next* character's bubble, one beat early "for
/// free," with no new chart entries needed. Clown/NormalAnt's bubble is hidden in DelayedReveal's
/// completion callback (the exact moment they become visually surfaced); PlayerAnt's bubble is hidden
/// unconditionally in HandlePlayerResponsePress/Release (covers every way it can surface: on-time
/// Hit, early-release Miss, everything). Known gap: cycle 0's very first Clown bubble has no earlier
/// beat4Cue to hang off of, so it's simply never shown — a one-time, likely-unnoticed exception at the
/// very start of the song; every cycle after that is correct.
/// </summary>
public class Stage1SurviveVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("광대")]
    [SerializeField] Image clownImage;
    [SerializeField] Sprite clownDiveSprite;
    [SerializeField] Sprite clownSurfaceSprite;

    [Header("일반 개미 1마리")]
    [SerializeField] Image normalAntImage;
    [SerializeField] Sprite antDiveSprite;
    [SerializeField] Sprite antSurfaceSprite;

    [Header("플레이어 개미(잭두) — 잠수/물밖/실패 3컷")]
    [SerializeField] Image playerAntImage;
    [SerializeField] Sprite playerDiveSprite;
    [SerializeField] Sprite playerSurfaceSprite;
    [Tooltip("실패 스프라이트 1장뿐 — 일찍 뗀 경우(물 밖 위치)/제때 못 뗀 경우(물 속 위치) 둘 다 " +
             "같은 그림을 Y 위치로만 구분해서 보여줌. 나중에 그림이 2장 생기면 이 자리에서 바로 " +
             "나눠 쓸 수 있음.")]
    [SerializeField] Sprite playerFailSprite;

    [Header("샤워기 — 물 튼/끈 2컷, 위치 고정(더 이상 좌우로 안 움직임)")]
    [SerializeField] Image showerImage;
    [SerializeField] Sprite showerOnSprite;
    [SerializeField] Sprite showerOffSprite;

    [Header("Tuning — 잠수/물밖")]
    [Tooltip("물 밖(숨쉬러 올라옴)일 때 캐릭터가 위로 올라가는 Y 오프셋(양수 = 위로). 기본 상태가 " +
             "잠수라서, 씬에 배치해둔 위치 자체를 잠수 자세 기준으로 잡음 — 물 밖일 때만 이 값만큼 " +
             "위로 올라감.")]
    [SerializeField] float surfaceYOffset = 80f;
    [Tooltip("판정 실패(Miss)로 인한 실패 스프라이트를 보여준 뒤 다시 원래 포즈로 돌아가기까지 대기 " +
             "시간 — 이 동안에도 Y 위치는 플레이어 입력에 따라 계속 그대로 움직임, 스프라이트만 이 " +
             "시간만큼 고정됨(PlayerFailFlash 참고).")]
    [SerializeField] float failSpriteHoldTime = 0.6f;
    [Tooltip("플레이어가 입력할 때마다(Call 구간 포함) Body Y 스케일이 커지는 목표값 — 판정과 무관한 " +
             "움직임 전용 반응, 다른 Stage1 미니게임과 동일한 패턴")]
    [SerializeField] float playerPressScaleY = 1.15f;
    [SerializeField] float pressScalePulseTime = 0.3f;
    [Tooltip("beat4Cue가 실제로 발화하는 순간(OnCallCueGroup)은 CallDirector 스폰 로직 때문에 진짜 " +
             "박자4보다 hitWindow만큼 먼저 옴 — 그 앞당겨진 만큼 여기서 도로 늦춰서 광대/개미가 " +
             "실제 박자보다 일찍 잠수해버리던 문제를 보정함. CallPatternData의 hitWindow 값과 " +
             "맞춰서 조절할 것(기본값은 hitWindow 기본값 0.12와 동일).")]
    [SerializeField] float syncDelay = 0.12f;
    [Tooltip("Clown/NormalAnt 전용: 물 밖 큐가 뜨고 나서 실제로 물 밖 스프라이트로 바뀌기까지 대기 " +
             "시간 — syncDelay와 같은 이유(OnCallCueGroup이 hitWindow만큼 일찍 발화)의 보정용. " +
             "이름은 예전 기포 연출 시절 그대로지만 지금은 순수 싱크 보정 값.")]
    [SerializeField] float surfaceRevealDelay = 0.25f;

    [Header("머리 위 기포 — 실제 아트 (Assets/1-3/1-3 기포.png). 세 개 다 씬에 미리 배치해둔 " +
            "오브젝트를 끄고 켜는 방식(런타임 생성 안 함) — 에디터에서 위치를 바로 보고 조정 가능.")]
    [SerializeField] Sprite bubbleSprite;
    [SerializeField] Image clownBubbleImage;
    [SerializeField] Image antBubbleImage;
    [SerializeField] Image playerBubbleImage;

    // groupId -> how many of this group's Call-role entries have fired OnCallCueGroup so far.
    // idx 0 = Clown, idx 1 = NormalAnt — see class doc.
    readonly System.Collections.Generic.Dictionary<int, int> _groupCallCounts = new System.Collections.Generic.Dictionary<int, int>();

    Coroutine _clownCr;
    Coroutine _antCr;
    Coroutine _submergeCr;
    Coroutine _playerFailCr;
    Coroutine _playerScaleCr;
    Vector3   _playerBaseScale;

    // Literal mirror of "is the player currently holding the button" — updated unconditionally by
    // every press/release, independent of judgement. See class doc's Event wiring section. Only read
    // by PlayerFailFlash, to know which sprite to snap back to once the fail flash ends.
    bool _playerSubmerged = true;

    float _clownBaseY, _antBaseY, _playerBaseY;

    void Awake()
    {
        if (clownImage != null) _clownBaseY = clownImage.rectTransform.anchoredPosition.y;
        if (normalAntImage != null) _antBaseY = normalAntImage.rectTransform.anchoredPosition.y;
        if (playerAntImage != null)
        {
            _playerBaseY = playerAntImage.rectTransform.anchoredPosition.y;
            _playerBaseScale = playerAntImage.rectTransform.localScale;
        }
    }

    void OnEnable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup    += HandleCallCueGroup;
        callDirector.OnGroupJudged     += HandleGroupJudged;
        callDirector.OnResponsePress   += HandlePlayerResponsePress;
        callDirector.OnResponseRelease += HandlePlayerResponseRelease;
    }

    void OnDisable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup    -= HandleCallCueGroup;
        callDirector.OnGroupJudged     -= HandleGroupJudged;
        callDirector.OnResponsePress   -= HandlePlayerResponsePress;
        callDirector.OnResponseRelease -= HandlePlayerResponseRelease;
    }

    void Start()
    {
        // Everyone starts submerged, shower on, player already "holding" — see class doc. The scene-
        // authored position IS the dive pose (see surfaceYOffset's tooltip), so no offset needed here.
        if (clownImage != null) { clownImage.sprite = clownDiveSprite; SetY(clownImage.rectTransform, _clownBaseY); }
        if (normalAntImage != null) { normalAntImage.sprite = antDiveSprite; SetY(normalAntImage.rectTransform, _antBaseY); }
        if (playerAntImage != null) { playerAntImage.sprite = playerDiveSprite; SetY(playerAntImage.rectTransform, _playerBaseY); }
        if (showerImage != null) showerImage.sprite = showerOnSprite;

        if (clownBubbleImage != null)  clownBubbleImage.sprite  = bubbleSprite;
        if (antBubbleImage != null)    antBubbleImage.sprite    = bubbleSprite;
        if (playerBubbleImage != null) playerBubbleImage.sprite = bubbleSprite;
        SetBubble(clownBubbleImage, false);
        SetBubble(antBubbleImage, false);
        SetBubble(playerBubbleImage, false);
    }

    void HandleCallCueGroup(int groupId)
    {
        _groupCallCounts.TryGetValue(groupId, out int idx);
        _groupCallCounts[groupId] = idx + 1;

        switch (idx)
        {
            case 0: // Clown — beat 1. Also the moment the shower shuts off for this cycle, and
                    // NormalAnt's bubble starts (NormalAnt surfaces 1 beat later, at beat 2).
                if (showerImage != null) showerImage.sprite = showerOffSprite;
                SurfaceClown();
                SetBubble(antBubbleImage, true);
                break;
            case 1: // NormalAnt — beat 2. Also starts PlayerAnt's bubble, but only if they're still
                    // submerged — they may have already surfaced early on a mistimed release, in which
                    // case there's nothing left to warn them about.
                SurfaceAnt();
                if (_playerSubmerged) SetBubble(playerBubbleImage, true);
                break;
            case 2: // beat4Cue — shower back on, Clown/NormalAnt dive together. Chart-timed, does NOT
                    // depend on the player's own press (see class doc). Also starts the *next* cycle's
                    // Clown bubble (Clown surfaces 1 beat after this fires, at next cycle's beat 1).
                if (_submergeCr != null) StopCoroutine(_submergeCr);
                _submergeCr = StartCoroutine(SubmergeCastNPCsDelayed());
                SetBubble(clownBubbleImage, true);
                break;
            // default: more than 3 Call-role fires in one group shouldn't happen for this chart shape.
        }
    }

    // CallDirector.SpawnDueCalls fires OnCallCueGroup as soon as t >= cueTime - hitWindow — i.e. every
    // Call-role cue (Clown/NormalAnt/this one) actually spawns hitWindow seconds BEFORE its nominal
    // beat, uniformly. Surfacing already happens to land close to the true beat because
    // surfaceRevealDelay (0.25s) outweighs that early fire, but submerging had no such compensation and
    // was visibly diving before beat 4 actually arrived (reported via play-testing). syncDelay
    // corrects for exactly that lookahead so this lands on the real beat instead.
    IEnumerator SubmergeCastNPCsDelayed()
    {
        if (syncDelay > 0f) yield return new WaitForSeconds(syncDelay);

        if (showerImage != null) showerImage.sprite = showerOnSprite;

        if (_clownCr != null) { StopCoroutine(_clownCr); _clownCr = null; }
        if (clownImage != null) { clownImage.sprite = clownDiveSprite; SetY(clownImage.rectTransform, _clownBaseY); }

        if (_antCr != null) { StopCoroutine(_antCr); _antCr = null; }
        if (normalAntImage != null) { normalAntImage.sprite = antDiveSprite; SetY(normalAntImage.rectTransform, _antBaseY); }

        _submergeCr = null;
    }

    void SurfaceClown()
    {
        if (clownImage == null || _clownCr != null) return;
        _clownCr = StartCoroutine(DelayedReveal(clownImage, clownSurfaceSprite, _clownBaseY + surfaceYOffset,
            () => { _clownCr = null; SetBubble(clownBubbleImage, false); }));
    }

    void SurfaceAnt()
    {
        if (normalAntImage == null || _antCr != null) return;
        _antCr = StartCoroutine(DelayedReveal(normalAntImage, antSurfaceSprite, _antBaseY + surfaceYOffset,
            () => { _antCr = null; SetBubble(antBubbleImage, false); }));
    }

    IEnumerator DelayedReveal(Image img, Sprite surfaceSprite, float targetY, Action onComplete)
    {
        yield return new WaitForSeconds(surfaceRevealDelay);
        img.sprite = surfaceSprite;
        SetY(img.rectTransform, targetY);
        onComplete?.Invoke();
    }

    // Hit only needs the reward flourish — position is already correct (the release that earned it
    // already moved the player via HandlePlayerResponseRelease). Miss shows the fail sprite — see
    // PlayerFailFlash for why this never touches Y.
    void HandleGroupJudged(int groupId, JudgementType j)
    {
        if (playerAntImage == null) return;

        if (j == JudgementType.Hit) return; // position/bubble already correct via HandlePlayerResponseRelease

        if (_playerFailCr != null) StopCoroutine(_playerFailCr);
        _playerFailCr = StartCoroutine(PlayerFailFlash());
    }

    // Fires on every physical press regardless of judgement (see CallDirector.OnResponsePress). Y
    // position is a direct, unconditional, always-immediate mirror of the held button — nothing, not
    // even an in-progress fail flash, ever delays or blocks it, per explicit request ("위치 움직이는건
    // 그냥 플레이어 입력으로만 영향 받았음 좋겠는데"). The sprite normally mirrors position the same
    // way, EXCEPT while a fail flash is showing (_playerFailCr != null) — that's the one exception, and
    // it only ever pins the *sprite*, never Y, so diving/surfacing during a fail flash still visibly
    // moves the character even though the fail art stays on screen until its own timer ends.
    void HandlePlayerResponsePress()
    {
        if (playerAntImage == null) return;

        if (_playerScaleCr == null)
            _playerScaleCr = StartCoroutine(ScalePulseRoutine(
                playerAntImage.rectTransform, _playerBaseScale, playerPressScaleY, pressScalePulseTime,
                () => _playerScaleCr = null));

        _playerSubmerged = true;
        SetY(playerAntImage.rectTransform, _playerBaseY);
        if (_playerFailCr == null) playerAntImage.sprite = playerDiveSprite;
        SetBubble(playerBubbleImage, false); // fresh dive — any bubble left over from before is stale
    }

    // Release counterpart of HandlePlayerResponsePress — same reasoning. Unconditionally hides the
    // bubble regardless of *why* this release happened (on-time Hit, early-release Miss, anything) —
    // a bubble floating above a surfaced character would look wrong either way.
    void HandlePlayerResponseRelease()
    {
        if (playerAntImage == null) return;

        _playerSubmerged = false;
        SetY(playerAntImage.rectTransform, _playerBaseY + surfaceYOffset);
        if (_playerFailCr == null) playerAntImage.sprite = playerSurfaceSprite;
        SetBubble(playerBubbleImage, false);
    }

    // Briefly shows the fail sprite in place — deliberately never touches Y (HandlePlayerResponsePress/
    // Release already own that unconditionally, see their doc comments) — then snaps the sprite to
    // match whatever the player is actually doing by the time the hold ends.
    IEnumerator PlayerFailFlash()
    {
        playerAntImage.sprite = playerFailSprite;
        yield return new WaitForSeconds(failSpriteHoldTime);
        playerAntImage.sprite = _playerSubmerged ? playerDiveSprite : playerSurfaceSprite;
        _playerFailCr = null;
    }

    static void SetY(RectTransform rect, float y)
    {
        var pos = rect.anchoredPosition;
        pos.y = y;
        rect.anchoredPosition = pos;
    }

    // Scene-placed, permanently-existing bubble Images (see class doc) — just toggled on/off, never
    // spawned/destroyed.
    static void SetBubble(Image bubbleImage, bool visible)
    {
        if (bubbleImage != null) bubbleImage.gameObject.SetActive(visible);
    }

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
}
