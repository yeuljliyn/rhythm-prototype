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
    [SerializeField] Sprite callAntIdleSprite;
    [SerializeField] Sprite callAntRaisedSprite;
    [SerializeField] Sprite responseAntIdleSprite;
    [SerializeField] Sprite responseAntRaisedSprite;

    [SerializeField] RectTransform clownRect;

    [SerializeField] float antPulseDuration = 0.25f;

    [Tooltip("Seconds after entering the Response phase (ant5) before the clown proactively heads " +
             "back to ant1, instead of waiting for the next group's actual OnCallCue. There's usually " +
             "a multi-beat rest between a group's response and the next group's first call — without " +
             "this, the clown only starts the long ant5->ant1 trip exactly when that next call fires, " +
             "leaving it no time to arrive before immediately having to head on to ant2, so it never " +
             "visibly lands on ant1. Purely cosmetic head-start — gets overwritten harmlessly by the " +
             "real OnCallCue once it fires, so it never affects judgement/SFX timing. Tune to be " +
             "shorter than this chart's actual response->next-group gap.")]
    [SerializeField] float returnToStartDelay = 0.5f;

    [Header("Puppet motion — spring chasing the current ant")]
    [Tooltip("Higher = snappier pull toward the target, lower = looser/floppier string.")]
    [SerializeField] float springStiffness = 90f;
    [Tooltip("Higher = settles faster with less wobble, lower = bounces longer.")]
    [SerializeField] float springDamping = 8f;

    [Header("Puppet motion — idle bob/tilt")]
    [SerializeField] float bobAmplitude = 6f;
    [SerializeField] float bobSpeed = 5f;
    [Tooltip("Degrees of lean per unit of horizontal speed (px/s), clamped by maxTiltDegrees.")]
    [SerializeField] float tiltPerSpeed = 0.06f;
    [SerializeField] float maxTiltDegrees = 14f;

    readonly Coroutine[] _antPulses = new Coroutine[5];
    Coroutine _returnToStartCr;
    int _callCueCount;
    int _targetAntIndex;

    float _posX, _velX;
    float _baseY;
    float _bobPhase;

    void OnEnable()  { Bind(callDirector); }
    void OnDisable() { Unbind(callDirector); }

    void Start()
    {
        for (int i = 0; i < antImages.Length; i++)
            if (antImages[i] != null) antImages[i].sprite = IdleSpriteFor(i);

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

    float AntX(int index) => antImages[index].rectTransform.anchoredPosition.x;

    void ApplyTransform(float x, float y, float tiltDegrees)
    {
        clownRect.anchoredPosition = new Vector2(x, y);
        clownRect.localRotation    = Quaternion.Euler(0f, 0f, tiltDegrees);
    }
}
