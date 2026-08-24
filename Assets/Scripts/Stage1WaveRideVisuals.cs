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
/// The clown's *target* ant is set directly by CallDirector's own rhythm events — OnCallCue for
/// ant0..ant3, OnPhaseChanged(true) for ant4 — the exact same events that drive each ant's hand-raise
/// pulse below. This is deliberate: those events are the game's actual rhythm source (whatever future
/// beat-synced SFX gets added will hang off the same source too), so the target must switch on them
/// directly rather than off an independently-computed SongTime/cueBeat schedule — a separate clock
/// can drift out of lockstep with "when the ant raises its hand" even if it's individually more
/// precise against the chart's raw beat math. What happens *between* target changes — the spring
/// chase, bob, and tilt below — is a purely cosmetic layer that doesn't affect *when* anything syncs,
/// only *how* it looks while getting there: overshoots past the target and wobbles back into place
/// like a marionette being tugged by a puppeteer's hand, instead of a perfectly-eased UI tween.
/// </summary>
public class Stage1WaveRideVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("Ant1..Ant4 = Call, Ant5 (last) = Response")]
    [SerializeField] Image[] antImages = new Image[5];
    [Tooltip("Ant1..Ant4가 각자 다른 리소스를 쓰기 위한 개별 idle/raised/fail 스프라이트 — 인덱스 0~3이 " +
             "antImages[0..3]과 1:1 대응. 예전엔 4마리가 callAntIdleSprite 하나를 공유했음.")]
    [SerializeField] Sprite[] callAntIdleSprites = new Sprite[4];
    [SerializeField] Sprite[] callAntRaisedSprites = new Sprite[4];
    [SerializeField] Sprite responseAntIdleSprite;
    [SerializeField] Sprite responseAntRaisedSprite;

    [SerializeField] RectTransform clownRect;

    [Header("Sea foreground — dips on every call/response note (chart-driven, not raw BPM)")]
    [SerializeField] RectTransform seaRect;
    [SerializeField] float seaWaveMinY = 135f;
    [SerializeField] float seaWaveMaxY = 160f;
    [Tooltip("노트가 뜬 후 seaWaveMinY까지 내려가는 데 걸리는 시간")]
    [SerializeField] float seaWaveDipTime = 0.12f;
    [Tooltip("내려갔다가 다시 seaWaveMaxY까지 올라오는 데 걸리는 시간")]
    [SerializeField] float seaWaveRiseTime = 0.18f;

    [Header("Clown — normal vs. Miss (falling) pose")]
    [SerializeField] Image clownImage;
    [SerializeField] Sprite clownNormalSprite;
    [SerializeField] Sprite clownFallSprite;
    [Tooltip("넘어지는 포즈가 유지되다가 원래 모습으로 돌아오기까지 걸리는 시간")]
    [SerializeField] float missPoseDuration = 0.4f;

    [Header("Ants — glare pose on Miss (Ant1-4 each have their own, Ant5 has its own)")]
    [SerializeField] Sprite[] callAntFailSprites = new Sprite[4];
    [SerializeField] Sprite ant5FailSprite;

    [SerializeField] float antPulseDuration = 0.25f;

    [Header("Ants — Scale-Y \"둠칫둠칫\" bop continuously in time with the song's BPM (reads bpm " +
            "live off clock.Pattern each frame, not a fixed number, so swapping in a different " +
            "song/chart just works without touching this script)")]
    [SerializeField] CallClock clock;
    [SerializeField] float antScaleMinY = 1.50f;
    [SerializeField] float antScaleMaxY = 1.55f;
    [Tooltip("한 박에 몇 번 출렁이는지. 1이면 한 박에 한 번 오르내림.")]
    [SerializeField] float antWaveCyclesPerBeat = 1f;

    [Tooltip("Response 구간(ant5)에 들어간 뒤, 다음 그룹의 실제 OnCallCue를 기다리지 않고 광대가 " +
             "미리 ant1 쪽으로 되돌아가기 시작하기까지의 시간(초). 보통 한 그룹의 response와 다음 " +
             "그룹의 첫 콜 사이에는 몇 박 정도 쉬는 구간이 있는데, 이 값이 없으면 광대는 다음 콜이 " +
             "실제로 뜨는 순간에야 ant5→ant1로 긴 이동을 시작해서, ant1에 도착하기도 전에 곧바로 " +
             "ant2로 다시 움직여야 하다 보니 ant1에 제대로 도착하는 모습을 볼 수가 없음. 순전히 " +
             "연출용 선행 이동이라 실제 OnCallCue가 뜨면 자연스럽게 덮어써지므로 판정/효과음 " +
             "타이밍에는 영향 없음. 이 차트의 실제 response→다음 그룹 간격보다는 짧게 설정할 것.")]
    [SerializeField] float returnToStartDelay = 0.5f;

    [Header("Puppet motion — spring chasing the current ant")]
    [Tooltip("클수록 목표 지점으로 더 빠르고 탄탄하게 당겨짐, 작을수록 헐렁하고 흐물흐물해짐")]
    [SerializeField] float springStiffness = 90f;
    [Tooltip("클수록 흔들림 없이 빨리 안정되고, 작을수록 더 오래 통통 튐")]
    [SerializeField] float springDamping = 8f;

    [Header("Puppet motion — idle bob/tilt")]
    [SerializeField] float bobAmplitude = 6f;
    [SerializeField] float bobSpeed = 5f;
    [Tooltip("가로 이동 속도(px/s) 단위당 기울어지는 각도. maxTiltDegrees로 상한 제한됨")]
    [SerializeField] float tiltPerSpeed = 0.06f;
    [SerializeField] float maxTiltDegrees = 14f;

    readonly Coroutine[] _antPulses = new Coroutine[5];
    Coroutine _returnToStartCr;
    Coroutine _missPoseCr;
    Coroutine _antFailCr;
    Coroutine _seaWaveCr;
    int _callCueCount;
    int _targetAntIndex;

    float _posX, _velX;
    float _baseY;
    float _bobPhase;

    void OnEnable()
    {
        Bind(callDirector);
        if (GameManager.Instance != null) GameManager.Instance.OnJudgement += HandleJudgement;
    }

    void OnDisable()
    {
        Unbind(callDirector);
        if (GameManager.Instance != null) GameManager.Instance.OnJudgement -= HandleJudgement;
    }

    void Start()
    {
        for (int i = 0; i < antImages.Length; i++)
            if (antImages[i] != null) antImages[i].sprite = IdleSpriteFor(i);

        if (clownImage != null) clownImage.sprite = clownNormalSprite;

        _targetAntIndex = callDirector != null && callDirector.StartsInResponsePhase ? 4 : 0;

        if (clownRect != null)
        {
            _baseY = clownRect.anchoredPosition.y;
            _posX  = AntX(_targetAntIndex);
            ApplyTransform(_posX, _baseY, 0f);
        }
    }

    void Update()
    {
        if (clownRect == null || antImages == null || antImages.Length == 0) return;

        float targetX = AntX(_targetAntIndex);

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f); // clamp so a frame hitch can't fling the spring
        SpringToTarget(targetX, dt);

        _bobPhase += dt * bobSpeed;
        float bobY = Mathf.Sin(_bobPhase) * bobAmplitude;
        float tilt = Mathf.Clamp(-_velX * tiltPerSpeed, -maxTiltDegrees, maxTiltDegrees);

        ApplyTransform(_posX, _baseY + bobY, tilt);

        AnimateAntsWithBpm();
    }

    // Cosine (not sine) so phase 0 lands on antScaleMaxY — matches each ant's authored resting
    // Scale Y in the scene, so there's no visible pop on the very first frame the song starts. Reads
    // BPM off clock.Pattern.BeatInterval every frame (instead of caching it once) specifically so a
    // chart swap mid-session — or just this CallClock's pattern reference being repointed at a
    // different song later — is picked up automatically; nothing here hardcodes a tempo. Only
    // Scale.y is touched so each ant's own x/z scale (already slightly different per ant) is left
    // untouched.
    void AnimateAntsWithBpm()
    {
        if (clock == null || antImages == null) return;

        float beatInterval = clock.Pattern != null ? clock.Pattern.BeatInterval : 60f / 128f;
        float phase = clock.SongTime / beatInterval * antWaveCyclesPerBeat;
        float wave01 = (Mathf.Cos(phase * Mathf.PI * 2f) + 1f) * 0.5f;
        float scaleY = Mathf.Lerp(antScaleMinY, antScaleMaxY, wave01);

        for (int i = 0; i < antImages.Length; i++)
        {
            if (antImages[i] == null) continue;
            var rect = antImages[i].rectTransform;
            var scale = rect.localScale;
            scale.y = scaleY;
            rect.localScale = scale;
        }
    }

    // Damped harmonic oscillator (semi-implicit Euler) — _posX chases targetX, overshooting and
    // wobbling back rather than smoothly easing straight in. Frame-rate dependent but stable enough
    // at typical UI update rates with dt clamped above; good enough for a cosmetic flourish.
    void SpringToTarget(float targetX, float dt)
    {
        float accel = (targetX - _posX) * springStiffness - _velX * springDamping;
        _velX += accel * dt;
        _posX += _velX * dt;
    }

    /// Switches which CallDirector drives these visuals — same rebind pattern as
    /// Stage1WaterPourVisuals, kept for consistency if 1-1 ever grows a tutorial gate.
    public void BindDirector(CallDirector newDirector)
    {
        Unbind(callDirector);
        callDirector = newDirector;
        Bind(callDirector);
        _callCueCount   = 0;
        _targetAntIndex = callDirector != null && callDirector.StartsInResponsePhase ? 4 : 0;
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
        _targetAntIndex = antIndex; // same event that pulses the ant also retargets the clown
        PulseSea();

        if (_returnToStartCr != null) { StopCoroutine(_returnToStartCr); _returnToStartCr = null; }
    }

    void HandlePhaseChanged(bool isResponsePhase)
    {
        if (!isResponsePhase) return;

        _targetAntIndex = 4;

        if (_returnToStartCr != null) StopCoroutine(_returnToStartCr);
        _returnToStartCr = StartCoroutine(ReturnToStartAfterDelay());
    }

    // Cosmetic head-start for the ant5->ant1 wrap — see returnToStartDelay's tooltip.
    IEnumerator ReturnToStartAfterDelay()
    {
        yield return new WaitForSeconds(returnToStartDelay);
        if (_targetAntIndex == 4) _targetAntIndex = 0; // no-op if a real OnCallCue already moved on
        _returnToStartCr = null;
    }

    void HandleResponsePress()
    {
        PulseAnt(4);
        PulseSea();
    }

    // Dips the sea down then back up on every note (Call or Response), restarting from wherever it
    // currently sits rather than skipping-while-busy like PulseAnt does — notes can land closer
    // together than a full dip+rise cycle, and retargeting mid-tween here just changes direction
    // smoothly instead of popping, so it reads as the sea reacting to the chart's actual rhythm
    // instead of an independent metronome (which is what the old free-running BPM sine wave looked
    // like — synced to the song's tempo number, not to when notes actually land).
    void PulseSea()
    {
        if (seaRect == null) return;
        if (_seaWaveCr != null) StopCoroutine(_seaWaveCr);
        _seaWaveCr = StartCoroutine(SeaWaveRoutine());
    }

    IEnumerator SeaWaveRoutine()
    {
        yield return TweenRectY(seaRect, seaWaveMinY, seaWaveDipTime);
        yield return TweenRectY(seaRect, seaWaveMaxY, seaWaveRiseTime);
        _seaWaveCr = null;
    }

    // Generic anchoredPosition.y tween, restarting cleanly from wherever the rect currently sits
    // (reads startY fresh each call) rather than from a fixed value — used by both the sea and the
    // ants so a retrigger mid-tween just changes direction instead of popping.
    IEnumerator TweenRectY(RectTransform rect, float targetY, float duration)
    {
        float startY = rect.anchoredPosition.y;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var pos = rect.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, Mathf.Clamp01(t / duration));
            rect.anchoredPosition = pos;
            yield return null;
        }
        var finalPos = rect.anchoredPosition;
        finalPos.y = targetY;
        rect.anchoredPosition = finalPos;
    }

    // Briefly swaps the clown to its falling pose on a Miss, regardless of which note (Call demo or
    // the Response ant5 note) caused it — GameManager.OnJudgement doesn't say which, and the clown is
    // the single shared "how's it going" readout for the whole group either way.
    void HandleJudgement(JudgementType j)
    {
        if (j != JudgementType.Miss) return;

        if (clownImage != null && clownFallSprite != null)
        {
            if (_missPoseCr != null) StopCoroutine(_missPoseCr);
            _missPoseCr = StartCoroutine(MissPoseRoutine());
        }

        if (antImages != null && antImages.Length > 0)
        {
            // Cancel any still-running call-cue raise pulses first. Ant4's call is the very last one
            // right before the response note, so its 0.25s raise-pulse (PulseAntRoutine) is often
            // still mid-flight when the response gets judged — left alone, that pulse's own
            // revert-to-idle line fires moments later and silently stomps the fail sprite this
            // routine is about to set, before missPoseDuration elapses. Ant1-3's pulses have already
            // finished by then (their calls happened earlier in the group), which is why only Ant4's
            // fail pose was going missing.
            for (int i = 0; i < _antPulses.Length; i++)
            {
                if (_antPulses[i] == null) continue;
                StopCoroutine(_antPulses[i]);
                _antPulses[i] = null;
            }

            if (_antFailCr != null) StopCoroutine(_antFailCr);
            _antFailCr = StartCoroutine(AntFailPoseRoutine());
        }
    }

    IEnumerator MissPoseRoutine()
    {
        clownImage.sprite = clownFallSprite;
        yield return new WaitForSeconds(missPoseDuration);
        clownImage.sprite = clownNormalSprite;
        _missPoseCr = null;
    }

    // Ant1..Ant4 each glare with their own look on Miss, Ant5 has its own — same brief swap-then-revert
    // shape as the clown's fall pose, on the same missPoseDuration so every reaction to a single
    // Miss lands and clears together.
    IEnumerator AntFailPoseRoutine()
    {
        for (int i = 0; i < antImages.Length; i++)
        {
            if (antImages[i] == null) continue;
            antImages[i].sprite = i == 4 ? ant5FailSprite : callAntFailSprites[i];
        }

        yield return new WaitForSeconds(missPoseDuration);

        for (int i = 0; i < antImages.Length; i++)
            if (antImages[i] != null) antImages[i].sprite = IdleSpriteFor(i);

        _antFailCr = null;
    }

    // Skips the trigger (rather than stopping-and-restarting) while already mid-pulse. ant4's pulse
    // is driven by OnResponsePress, which fires on every physical press regardless of the chart's
    // input cooldown (visuals must react to every press even during that cooldown — see
    // CallDirector). A stop-and-restart guard would mean mashing faster than antPulseDuration keeps
    // re-triggering the "raised" sprite before the revert-to-idle step ever lands, so the ant looks
    // permanently raised. Skipping instead guarantees every pulse that does play completes its full
    // raised->idle cycle, capping the visible reaction rate at antPulseDuration.
    void PulseAnt(int index)
    {
        if (index < 0 || index >= antImages.Length || antImages[index] == null) return;
        if (_antPulses[index] != null) return; // still mid-pulse — let it finish
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

    // Ant1..Ant4 (index 0-3, Call role) each have their own look; Ant5 (index 4, Response role) is
    // the player's own distinct character.
    Sprite IdleSpriteFor(int index)   => index == 4 ? responseAntIdleSprite   : callAntIdleSprites[index];
    Sprite RaisedSpriteFor(int index) => index == 4 ? responseAntRaisedSprite : callAntRaisedSprites[index];

    float AntX(int index) => antImages[index].rectTransform.anchoredPosition.x;

    void ApplyTransform(float x, float y, float tiltDegrees)
    {
        clownRect.anchoredPosition = new Vector2(x, y);
        clownRect.localRotation    = Quaternion.Euler(0f, 0f, tiltDegrees);
    }
}
