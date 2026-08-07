using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Design-comparison variant of Stage1WaveRideVisuals for the planner to weigh against the original
/// (Assets/Scenes/Stage1_1Scene.unity + Stage1WaveRideVisuals.cs). Same clown target/spring/bob/tilt
/// logic — the only difference is the new single-sprite character art (광대.png / 그냥 개미.png /
/// 잭두 개미 융합.png), which has no idle/raised frame pair, so ants sell "hands up for the wave" by
/// physically hopping instead of swapping sprites. Lives only in Stage1_1Scene_NewArt.unity — delete
/// this file (and that scene) once the planner picks a direction, or fold it back into
/// Stage1WaveRideVisuals.cs if the new art wins.
/// </summary>
public class Stage1WaveRideVisuals_NewArt : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("Ant1..Ant4 = Call, Ant5 (last) = Response — single sprite each, no idle/raised pair")]
    [SerializeField] Image[] antImages = new Image[5];
    [SerializeField] Sprite callAntSprite;
    [SerializeField] Sprite responseAntSprite;

    [SerializeField] RectTransform clownRect;

    [Tooltip("How long the hop (up and back down) takes.")]
    [SerializeField] float antPulseDuration = 0.25f;
    [Tooltip("How high the ant hops, in UI pixels.")]
    [SerializeField] float antJumpHeight = 30f;

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
    readonly float[] _antBaseY = new float[5];
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
        {
            if (antImages[i] == null) continue;
            antImages[i].sprite = SpriteFor(i);
            _antBaseY[i] = antImages[i].rectTransform.anchoredPosition.y;
        }

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
        int antIndex = _callCueCount % 4; // relies on exactly 4 Call notes per group
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
        _antPulses[index] = StartCoroutine(JumpAntRoutine(index));
    }

    // Each ant is a single static sprite (already posed mid-cheer, arms up) — no idle/raised frame
    // swap to sell "hands up for the wave" anymore, so instead the ant physically hops: rises off
    // _antBaseY along a sine arc (0 at start/end, peak at the midpoint) and settles back down.
    IEnumerator JumpAntRoutine(int index)
    {
        var rt = antImages[index].rectTransform;
        float baseY = _antBaseY[index];
        float t = 0f;
        while (t < antPulseDuration)
        {
            t += Time.deltaTime;
            float frac = Mathf.Clamp01(t / antPulseDuration);
            float y = baseY + Mathf.Sin(frac * Mathf.PI) * antJumpHeight;
            var pos = rt.anchoredPosition;
            pos.y = y;
            rt.anchoredPosition = pos;
            yield return null;
        }
        var finalPos = rt.anchoredPosition;
        finalPos.y = baseY;
        rt.anchoredPosition = finalPos;
        _antPulses[index] = null;
    }

    // Ant1..Ant4 (index 0-3, Call role) share one look; Ant5 (index 4, Response role) is the
    // player's own distinct character.
    Sprite SpriteFor(int index) => index == 4 ? responseAntSprite : callAntSprite;

    float AntX(int index) => antImages[index].rectTransform.anchoredPosition.x;

    void ApplyTransform(float x, float y, float tiltDegrees)
    {
        clownRect.anchoredPosition = new Vector2(x, y);
        clownRect.localRotation    = Quaternion.Euler(0f, 0f, tiltDegrees);
    }
}
