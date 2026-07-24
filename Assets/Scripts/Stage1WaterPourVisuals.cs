using System.Collections;
using UnityEngine;

/// <summary>
/// Minigame 1 (물붓기) visual glue. Hand/Ants/Boss don't free-loop — each one only plays a single
/// pass of its flipbook when explicitly cued, then freezes again:
///   - Hand (and whichever Boss state is active) pulses on CallDirector.OnCallCue, i.e. exactly on
///     each Call-role beat, so the boss's demo rhythm has a legible "this is the beat" moment
///     instead of an ungrounded continuous loop.
///   - Ants (and Boss again) pulses on CallDirector.OnResponsePress — the player's own button press,
///     not the chart clock — so Ants only ever moves in direct reaction to player input.
/// Boss's ResponsePhase bool still switches Boss1<->Boss2 to show whose turn it is.
///
/// Rebindable at runtime via BindDirector() — Stage1TutorialGate uses this to point the same
/// Hand/Ants/Boss visuals at the tutorial CallDirector first, then at the main one once the
/// tutorial gate passes, so both phases share one set of characters. Kept separate from
/// CallDirector itself since each minigame will need its own character wiring.
/// </summary>
public class Stage1WaterPourVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector; // whichever director should drive visuals first
    [SerializeField] Animator bossAnimator;
    [SerializeField] Animator handAnimator;
    [SerializeField] Animator antsAnimator;

    static readonly int ResponsePhaseHash = Animator.StringToHash("ResponsePhase");

    Coroutine _handPulse, _antsPulse, _bossPulse;

    void OnEnable()  { Bind(callDirector); }
    void OnDisable() { Unbind(callDirector); }

    void Start()
    {
        SetPhase(callDirector != null && callDirector.StartsInResponsePhase);

        // Nothing moves until the first real cue/press pulses it.
        if (handAnimator != null) handAnimator.speed = 0f;
        if (antsAnimator != null) antsAnimator.speed = 0f;
        if (bossAnimator != null) bossAnimator.speed = 0f;
    }

    /// Switches which CallDirector drives these visuals — unsubscribes from the old one (if any),
    /// subscribes to the new one, and re-syncs Boss's ResponsePhase to the new chart's starting role.
    public void BindDirector(CallDirector newDirector)
    {
        Unbind(callDirector);
        callDirector = newDirector;
        Bind(callDirector);
        SetPhase(callDirector != null && callDirector.StartsInResponsePhase);
    }

    void Bind(CallDirector d)
    {
        if (d == null) return;
        d.OnPhaseChanged  += SetPhase;
        d.OnCallCue       += PulseCall;
        d.OnResponsePress += PulseResponse;
    }

    void Unbind(CallDirector d)
    {
        if (d == null) return;
        d.OnPhaseChanged  -= SetPhase;
        d.OnCallCue       -= PulseCall;
        d.OnResponsePress -= PulseResponse;
    }

    void SetPhase(bool isResponsePhase)
    {
        if (bossAnimator != null) bossAnimator.SetBool(ResponsePhaseHash, isResponsePhase);
    }

    void PulseCall()
    {
        Pulse(handAnimator, ref _handPulse);
        Pulse(bossAnimator, ref _bossPulse);
    }

    void PulseResponse()
    {
        Pulse(antsAnimator, ref _antsPulse);
        Pulse(bossAnimator, ref _bossPulse);
    }

    void Pulse(Animator anim, ref Coroutine handle)
    {
        if (anim == null) return;
        if (handle != null) StopCoroutine(handle);
        handle = StartCoroutine(PulseRoutine(anim));
    }

    // Replays whichever state the Animator is currently parked in (Pour / Boss1 / Boss2) from
    // frame 0, then freezes it again once that single pass finishes.
    IEnumerator PulseRoutine(Animator anim)
    {
        int hash = anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
        anim.speed = 1f;
        anim.Play(hash, 0, 0f);
        yield return new WaitForSeconds(anim.GetCurrentAnimatorStateInfo(0).length);
        anim.speed = 0f;
    }
}
