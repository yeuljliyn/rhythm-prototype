using System;
using System.Collections;
using System.Collections.Generic;
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
///     the rest of the cycle). Head-bubbles are NOT driven off this event — see the bubble paragraph
///     below for why and how they're scheduled independently.
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
/// Bubble anticipation cue ("올라오기 직전마다 머리 위에 기포가 뽀로록"), v3 — replaces the v2 cascade
/// implementation (each character's bubble triggered off the *previous* character's own cue firing),
/// which silently assumed every character always surfaces exactly 1 beat after the one before it in
/// the cycle. That assumption isn't the actual spec and isn't guaranteed by the chart data — the real
/// rule, per character, independent of any other character's timing:
///   - Clown/NormalAnt: their bubble appears exactly 1 beat before *their own* cueBeat, and
///     disappears the instant they actually surface.
///   - PlayerAnt (Response): its bubble appears 1 beat before the beat they need to surface by (their
///     own Hold entry's releaseBeat) — but ONLY if they're still submerged at that moment (they may
///     have already surfaced early on a mistimed release, in which case there's nothing left to warn
///     them about). It disappears the instant they surface too, for the same reason a bubble floating
///     above someone already out of the water would look wrong ("기포가 물밖에 있으면 좀 이상하잖아").
/// Implementation: ScanForBubbleScheduling walks CallDirector.Calls (the director's actual live
/// schedule — NOT CallPatternData.calls directly, which for a looping practice chart only ever holds
/// the one authored cycle; PracticeLoopExtender appends synthesized reps straight into the director,
/// several beats ahead of when they're due, which is exactly the lead time this needs) and, for every
/// entry it hasn't seen yet, independently queues a "show at (this entry's own relevant beat) - 1"
/// pending bubble — Clown/NormalAnt off their own cueBeat, Player off their own Hold's releaseBeat. No
/// character's timing is ever inferred from another's. Queued bubbles are resolved by ResolvePendingBubbles
/// every Update(), comparing each one's target time directly against CallClock.SongTime (dspTime-based)
/// — deliberately NOT a WaitForSeconds coroutine (Time.deltaTime-based): the two clocks can drift apart
/// under frame-pacing stalls (confirmed while testing headless via MCP — SongTime and Time.time visibly
/// diverged), which would show a bubble at the wrong real moment even though the *beat* math was right.
/// Polling SongTime directly keeps this exact regardless of frame pacing, same principle CallDirector's
/// own judgement loop already follows. Clown/NormalAnt's bubble is still hidden in DelayedReveal's
/// completion callback (the exact moment they become visually surfaced); PlayerAnt's bubble is still
/// hidden unconditionally in HandlePlayerResponsePress/Release (covers every way it can surface:
/// on-time Hit, early-release Miss, everything) — only the SHOW trigger changed, not the HIDE side.
/// </summary>
public class Stage1SurviveVisuals : MonoBehaviour, IPracticeRebindable
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

    [Header("SFX")]
    [SerializeField] AudioSource sfxSource;
    [Tooltip("기포가 뜰 때(광대/개미/플레이어 공통, Assets/Sound/1-3_보글.wav)")]
    [SerializeField] AudioClip bubbleSfx;
    [Tooltip("캐릭터가 물 밖으로 나올 때(광대/개미/플레이어 공통, Assets/Sound/1-3탬버린.wav)")]
    [SerializeField] AudioClip surfaceSfx;

    // groupId -> how many of this group's Call-role entries have fired OnCallCueGroup so far.
    // idx 0 = Clown, idx 1 = NormalAnt — see class doc.
    readonly Dictionary<int, int> _groupCallCounts = new Dictionary<int, int>();

    // Bubble scheduling — see class doc's "Bubble anticipation cue" section. _bubbleCallIdxPerGroup is
    // NOT the same idx counter as _groupCallCounts above, which only tracks entries that have actually
    // FIRED — this one tracks entries merely SEEN in the live schedule, ahead of time, so bubbles can
    // be queued a full beat-plus before they're due. _pendingBubbles holds every queued-but-not-yet-
    // shown bubble; ResolvePendingBubbles (called every Update()) is what actually shows them once
    // CallClock.SongTime reaches each one's target time.
    readonly Dictionary<int, int> _bubbleCallIdxPerGroup = new Dictionary<int, int>();
    struct PendingBubble
    {
        public Image bubbleImage;
        public CallClock clock;
        public float targetTime;
        public bool requireStillSubmerged;
    }
    readonly List<PendingBubble> _pendingBubbles = new List<PendingBubble>();
    int _bubbleScanIdx;

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

    // Stage1_5Scene(리믹스)에서 같은 스타일이 한 곡 안에서 여러 번 반복될 때마다 이 GameObject가
    // SetActive(true/false)로 재진입됨 — Start()는 최초 1회뿐이라 두 번째 이후 방문은 지난 방문이
    // 끝난 시점의 스프라이트/위치/기포 예약 상태가 그대로 남아있었음(실제로 겪은 버그). 재진입마다
    // ResetVisualState를 돌려서 항상 "모두 잠수, 샤워 켜짐" 초기 상태로 시작하게 함.
    void OnEnable()
    {
        Bind(callDirector);
        ResetVisualState();
    }
    void OnDisable() => Unbind(callDirector);

    void Bind(CallDirector d)
    {
        if (d == null) return;
        d.OnCallCueGroup    += HandleCallCueGroup;
        d.OnGroupJudged     += HandleGroupJudged;
        d.OnResponsePress   += HandlePlayerResponsePress;
        d.OnResponseRelease += HandlePlayerResponseRelease;
    }

    void Unbind(CallDirector d)
    {
        if (d == null) return;
        d.OnCallCueGroup    -= HandleCallCueGroup;
        d.OnGroupJudged     -= HandleGroupJudged;
        d.OnResponsePress   -= HandlePlayerResponsePress;
        d.OnResponseRelease -= HandlePlayerResponseRelease;
    }

    /// Switches which CallDirector drives these visuals — see Stage1WaveRideVisuals.BindDirector.
    /// Also resets bubble-scheduling state: a rebind means a brand new director with its own,
    /// freshly-restarted Calls list (Stage1PracticeGate segment swap, tutorial gate), so anything
    /// still waiting to fire from the OLD director's schedule is stale and must be cancelled — never
    /// let a bubble from a torn-down segment pop up over whatever's playing now.
    public void BindDirector(CallDirector newDirector)
    {
        Unbind(callDirector);
        callDirector = newDirector;
        Bind(callDirector);

        _pendingBubbles.Clear();
        _bubbleCallIdxPerGroup.Clear();
        _bubbleScanIdx = 0;
        SetBubble(clownBubbleImage, false);
        SetBubble(antBubbleImage, false);
        SetBubble(playerBubbleImage, false);
    }

    void Update()
    {
        ScanForBubbleScheduling();
        ResolvePendingBubbles();
    }

    void Start() => ResetVisualState();

    // 그룹ID로 키가 잡힌 상태(_groupCallCounts, 기포 스캔용 _bubbleScanIdx 등)는 일부러 안 건드림 —
    // 이 채보의 groupId는 곡 전체(리믹스 전체)에서 유일하게 부여되므로(에디터가 배치 시점에 명시적으로
    // 찍음, 스타일마다 리셋되지 않음) 재진입한다고 다시 훑을 필요가 없고, 특히 _bubbleScanIdx를
    // 0으로 되돌리면 이미 지난 노트들의 기포를 전부 "아직 안 보여준 것"으로 착각해 재진입 즉시 한꺼번에
    // 쏟아내는 훨씬 나쁜 버그가 생김(ResolvePendingBubbles가 SongTime>=targetTime인 걸 전부 즉시
    // 발화시키므로). 여기서 리셋하는 건 순전히 "화면에 지금 보이는 모습"(스프라이트/위치/코루틴
    // 핸들)뿐 — Stage1_5Scene 리믹스처럼 같은 스타일이 여러 번 반복 등장할 때 지난 방문 종료 시점의
    // 포즈가 다음 방문 시작 프레임에 그대로 남아있던 버그(실제로 겪음) 수정.
    void ResetVisualState()
    {
        if (_clownCr != null) { StopCoroutine(_clownCr); _clownCr = null; }
        if (_antCr != null) { StopCoroutine(_antCr); _antCr = null; }
        if (_submergeCr != null) { StopCoroutine(_submergeCr); _submergeCr = null; }
        if (_playerFailCr != null) { StopCoroutine(_playerFailCr); _playerFailCr = null; }
        if (_playerScaleCr != null) { StopCoroutine(_playerScaleCr); _playerScaleCr = null; }
        _playerSubmerged = true;

        // Everyone starts submerged, shower on, player already "holding" — see class doc. The scene-
        // authored position IS the dive pose (see surfaceYOffset's tooltip), so no offset needed here.
        if (clownImage != null) { clownImage.sprite = clownDiveSprite; SetY(clownImage.rectTransform, _clownBaseY); }
        if (normalAntImage != null) { normalAntImage.sprite = antDiveSprite; SetY(normalAntImage.rectTransform, _antBaseY); }
        if (playerAntImage != null)
        {
            playerAntImage.sprite = playerDiveSprite;
            SetY(playerAntImage.rectTransform, _playerBaseY);
            playerAntImage.rectTransform.localScale = _playerBaseScale;
        }
        if (showerImage != null) showerImage.sprite = showerOnSprite;

        if (clownBubbleImage != null)  clownBubbleImage.sprite  = bubbleSprite;
        if (antBubbleImage != null)    antBubbleImage.sprite    = bubbleSprite;
        if (playerBubbleImage != null) playerBubbleImage.sprite = bubbleSprite;
        SetBubble(clownBubbleImage, false);
        SetBubble(antBubbleImage, false);
        SetBubble(playerBubbleImage, false);
    }

    // Walks callDirector.Calls (the director's actual live schedule, including whatever
    // PracticeLoopExtender has appended so far) forward from wherever it last left off, and
    // independently schedules a bubble for every entry that needs one — see class doc's "Bubble
    // anticipation cue" section for why this reads the live schedule instead of CallPatternData.calls
    // directly, and why each entry's timing is computed from its own beat rather than cascaded off
    // another character's cue. _bubbleScanIdx only ever walks forward and Calls only ever grows
    // (AppendCalls/ReplaceCalls — see CallDirector), so re-running this every Update() and resuming
    // from _bubbleScanIdx is safe and cheap (a no-op scan once caught up).
    void ScanForBubbleScheduling()
    {
        if (callDirector == null) return;
        var calls = callDirector.Calls;
        if (calls == null) return;

        for (; _bubbleScanIdx < calls.Count; _bubbleScanIdx++)
        {
            var entry = calls[_bubbleScanIdx];

            if (entry.role == CallRole.Call)
            {
                // idx 0 = Clown, idx 1 = NormalAnt, idx 2 = beat4Cue (submerge cue — no bubble; see
                // class doc's "Chart shape per cycle"). This idx is purely a per-group ordinal
                // position among Call-role entries, independent of any timing/spacing assumption.
                _bubbleCallIdxPerGroup.TryGetValue(entry.groupId, out int idx);
                _bubbleCallIdxPerGroup[entry.groupId] = idx + 1;

                if (idx == 0) ScheduleBubble(clownBubbleImage, entry.cueBeat - 1f, requireStillSubmerged: false);
                else if (idx == 1) ScheduleBubble(antBubbleImage, entry.cueBeat - 1f, requireStillSubmerged: false);
            }
            else if (entry.role == CallRole.Response && entry.type == CallType.Hold)
            {
                // The player's own Hold — "the beat they need to surface by" is its releaseBeat.
                ScheduleBubble(playerBubbleImage, entry.releaseBeat - 1f, requireStillSubmerged: true);
            }
        }
    }

    void ScheduleBubble(Image bubbleImage, float targetBeat, bool requireStillSubmerged)
    {
        if (bubbleImage == null || callDirector == null) return;
        var clock = callDirector.Clock;
        if (clock == null || clock.Pattern == null) return;

        _pendingBubbles.Add(new PendingBubble
        {
            bubbleImage = bubbleImage,
            clock = clock,
            targetTime = clock.Pattern.BeatToTime(targetBeat),
            requireStillSubmerged = requireStillSubmerged,
        });
    }

    // Polls every queued bubble against its own clock's SongTime — see class doc for why this is a
    // poll instead of a WaitForSeconds coroutine. Iterates backward so RemoveAt mid-loop is safe.
    void ResolvePendingBubbles()
    {
        for (int i = _pendingBubbles.Count - 1; i >= 0; i--)
        {
            var p = _pendingBubbles[i];
            if (p.clock == null || p.clock.SongTime < p.targetTime) continue;

            // Player only: if they already surfaced early on a mistimed release, there's nothing left
            // to warn them about (see class doc). Clown/NormalAnt have no such exception — they're
            // chart-timed demo characters, never player-input-dependent.
            if (!p.requireStillSubmerged || _playerSubmerged)
            {
                SetBubble(p.bubbleImage, true);
                PlaySfx(bubbleSfx);
            }
            _pendingBubbles.RemoveAt(i);
        }
    }

    void HandleCallCueGroup(int groupId)
    {
        _groupCallCounts.TryGetValue(groupId, out int idx);
        _groupCallCounts[groupId] = idx + 1;

        switch (idx)
        {
            case 0: // Clown — beat 1. Also the moment the shower shuts off for this cycle.
                if (showerImage != null) showerImage.sprite = showerOffSprite;
                SurfaceClown();
                break;
            case 1: // NormalAnt — beat 2.
                SurfaceAnt();
                break;
            case 2: // beat4Cue — shower back on, Clown/NormalAnt dive together. Chart-timed, does NOT
                    // depend on the player's own press (see class doc).
                if (_submergeCr != null) StopCoroutine(_submergeCr);
                _submergeCr = StartCoroutine(SubmergeCastNPCsDelayed());
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
        PlaySfx(surfaceSfx);
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
        PlaySfx(surfaceSfx);
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

    void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
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
