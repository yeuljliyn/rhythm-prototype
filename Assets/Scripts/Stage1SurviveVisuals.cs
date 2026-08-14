using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minigame 1-3 (존버) visual glue. A shower beam 2 characters wide sweeps left-to-right over a row
/// of 5 (3 normal ants, the player ant, the clown) across 4 beats — at each beat, the 2 characters the
/// beam currently spans over both do a quick dip (not a sustained hold — corrected after actually
/// testing this: a middle character like NormalAnt2 needs its OWN separate dip at each beat it's
/// paired at, not one continuous submersion bridging both beats, otherwise it visibly isn't diving at
/// the second beat it's supposed to be in). Only the PLAYER's own note is a true sustained Hold, since
/// that's the one thing actually physically held down by a real press. One round's chart shape (7
/// CallEntry objects, same groupId):
///
///   beat:      1           2           3           4
///   Ant1:    [Tap]                                        (Call — dips once, beat 1)
///   Ant2:    [Tap]     [Tap]                               (Call — dips twice, beat 1 AND beat 2)
///   Ant3:              [Tap]     [Tap]                     (Call — dips twice, beat 2 AND beat 3)
///   Player:                    [======Hold======]          (RESPONSE — sustained, beat 3→4)
///   Clown:                                       [Tap]      (Call — dips once, beat 4)
///
/// Only the player's Hold is role=Response (judged); everything else is role=Call (demo-only, never
/// judged, never reads input) — see CallEntry.role / CallDirector.BuildLiveCall.
///
/// NormalAnt1/Ant2/Ant3/Clown are all driven by CallDirector.OnCallCueGroup (fires once per Call-role
/// entry as it spawns — see CallDirector.cs), in a FIXED per-group firing order that exactly matches
/// how this chart's 6 Call entries are authored: Ant1, Ant2, Ant2, Ant3, Ant3, Clown.
/// _groupCallCounts turns that firing order into an index (0..5) the same way Stage1QuickTapVisuals'
/// per-group counter does — every one of these is now a plain short pulse (edgePulseTime), no more
/// per-entry duration math. Ant1's/Ant2's first Tap share beat 1, and Ant2's second/Ant3's first Tap
/// share beat 2 — same-beat entries have no guaranteed sort tie-break in CallDirector, so each later
/// one in a same-beat pair is authored with a tiny (imperceptible) cueBeat offset ahead of the earlier
/// one. See the CallPatternData asset's authoring notes for where that offset is applied.
///
/// PlayerAnt is Response-role, so it never fires OnCallCueGroup at all — its dive/surface/fail state
/// is driven entirely by three CallDirector events added specifically for this minigame (additive,
/// doesn't touch OnCallCue/OnResponsePress/OnGroupJudged's existing behavior for other minigames):
///   - OnHoldArmed: fires the instant the player's press successfully claims the live Hold — switch
///     to the dive sprite right here (not on OnResponsePress, which fires on every press including
///     ones that don't land in a live window at all).
///   - OnHoldEarlyRelease: fires just before a Miss when the player let go too early (while they
///     should still have been holding) — flags the upcoming OnGroupJudged Miss as "surfaced" rather
///     than "still submerged," so the fail sprite reads as happening above water vs. underwater. Both
///     cases currently show the same single fail sprite (only one was provided) — the distinction is
///     tracked so a future art pass can split it into two without touching CallDirector again.
///   - OnGroupJudged: the real Hit/Miss verdict — Hit -> surface sprite (successful resurface), Miss
///     -> fail sprite (held briefly, then reverts to the surface sprite for the next round).
///
/// Per this session's established pattern (see Stage1QuickTapVisuals' HandlePlayerResponsePress),
/// PlayerAnt also reacts to CallDirector.OnResponsePress with a Y-scale pulse — fires on every
/// physical press regardless of whether it landed in a live window, so the player character visibly
/// reacts even to a press during the Call-only portion of a round. This is movement only: it never
/// touches the dive/surface/fail sprite, which stays gated to the three events above (i.e. real
/// judgement still only ever happens inside CallDirector's Hit/Miss windows).
/// </summary>
public class Stage1SurviveVisuals : MonoBehaviour
{
    [SerializeField] CallDirector callDirector;

    [Header("일반 개미 3마리 (왼쪽부터) — 잠수/물밖 2컷")]
    [SerializeField] Image[] normalAntImages = new Image[3];
    [SerializeField] Sprite antDiveSprite;
    [SerializeField] Sprite antSurfaceSprite;

    [Header("플레이어 개미(잭두) — 잠수/물밖/실패 3컷")]
    [SerializeField] Image playerAntImage;
    [SerializeField] Sprite playerDiveSprite;
    [SerializeField] Sprite playerSurfaceSprite;
    [Tooltip("실패 스프라이트 1장뿐 — 일찍 뗀 경우(물 밖)/제때 못 뗀 경우(물 속) 둘 다 지금은 이걸로 " +
             "통일해서 보여줌. wasEarlyRelease 구분은 코드에 남겨둬서 나중에 그림이 2장 생기면 " +
             "CallDirector 안 건드리고 바로 나눠 쓸 수 있음.")]
    [SerializeField] Sprite playerFailSprite;

    [Header("광대")]
    [SerializeField] Image clownImage;
    [SerializeField] Sprite clownDiveSprite;
    [SerializeField] Sprite clownSurfaceSprite;

    [Header("샤워기 — 물 튼/끈 2컷")]
    [Tooltip("씬에 배치해둔 위치가 그대로 라운드 시작 위치(개미1+개미2 짝)로 쓰임 — Awake 시점 " +
             "anchoredPosition.x를 그대로 기준(idle X)으로 읽어서 저장하므로, 씬에서 위치만 옮기면 " +
             "코드 안 고쳐도 그대로 따라감.")]
    [SerializeField] Image showerImage;
    [SerializeField] Sprite showerOnSprite;
    [SerializeField] Sprite showerOffSprite;
    [Tooltip("한 라운드 안에서 짝이 넘어갈 때마다(개미1,2 -> 개미2,3 -> 개미3,잭두 -> 잭두,광대) " +
             "샤워기가 오른쪽으로 이동하는 X 간격 — 캐릭터들 사이 간격(현재 씬 300)과 맞춰야 " +
             "정확히 다음 짝 위로 옮겨감.")]
    [SerializeField] float showerStepX = 300f;
    [Tooltip("샤워기가 다음 짝 위치로 미끄러지듯 이동하는 데 걸리는 시간")]
    [SerializeField] float showerMoveTime = 0.15f;

    [Header("Tuning")]
    [Tooltip("잠수할 때 캐릭터가 아래로 내려가는 Y 오프셋(음수 = 아래로) — 평소(물 밖)엔 씬에 배치해둔 " +
             "그대로 어항 위쪽에 걸터앉은 위치, 잠수 중에만 이 값만큼 내려감. 판정과 무관하게 실패 " +
             "스프라이트를 보여줄 때도 이 위치를 씀(HandleGroupJudged 참고) — 일찍 뗀 실패는 물 " +
             "밖(원래 위치), 제때 못 뗀 실패는 물 속(이 오프셋) 위치로 구분.")]
    [SerializeField] float diveYOffset = -80f;
    [Tooltip("개미1/광대(한 박자만 걸치는 양 끝)가 잠수 자세를 유지하는 시간")]
    [SerializeField] float edgePulseTime = 0.3f;
    [Tooltip("플레이어가 입력할 때마다(Call 구간 포함) Body Y 스케일이 커지는 목표값 — 판정과 무관한 " +
             "움직임 전용 반응, Stage1QuickTapVisuals와 동일한 패턴")]
    [SerializeField] float playerPressScaleY = 1.15f;
    [Tooltip("실패 스프라이트를 보여준 뒤 다시 물밖(대기) 스프라이트로 돌아가기까지 대기 시간")]
    [SerializeField] float failSpriteHoldTime = 0.6f;

    // groupId -> how many of this group's Call-role entries have fired OnCallCueGroup so far. See
    // class doc — index 0/1/2/3 map to a FIXED sequence (Ant1, Ant2, Ant3, Clown) for this chart's
    // specific shape, not a general-purpose mechanism.
    readonly Dictionary<int, int> _groupCallCounts = new Dictionary<int, int>();

    // groupId -> this group's upcoming Miss (about to arrive via OnGroupJudged) was an early release,
    // not a timeout. Consumed the moment OnGroupJudged actually fires for that groupId.
    readonly HashSet<int> _earlyReleaseGroups = new HashSet<int>();

    Coroutine[] _antCrs;     // parallel to normalAntImages
    Coroutine   _clownCr;
    Coroutine   _playerFailCr;
    Coroutine   _playerScaleCr;
    Vector3     _playerBaseScale;

    Coroutine _showerMoveCr;
    float     _showerIdleX; // anchoredPosition.x as authored in the scene — see showerImage's tooltip

    // Each character's authored "surfaced, perched on the tank rim" Y — read once at Awake so
    // diving/surfacing can move Y between this and (this + diveYOffset) without needing the exact
    // scene-authored value hardcoded, and so repositioning a character in the scene doesn't require a
    // code change.
    float[] _antBaseY;   // parallel to normalAntImages
    float   _clownBaseY;
    float   _playerBaseY;

    void Awake()
    {
        _antCrs = new Coroutine[normalAntImages != null ? normalAntImages.Length : 0];
        _antBaseY = new float[_antCrs.Length];
        for (int i = 0; i < _antBaseY.Length; i++)
            if (normalAntImages[i] != null)
                _antBaseY[i] = normalAntImages[i].rectTransform.anchoredPosition.y;

        if (playerAntImage != null)
        {
            _playerBaseScale = playerAntImage.rectTransform.localScale;
            _playerBaseY = playerAntImage.rectTransform.anchoredPosition.y;
        }
        if (clownImage != null) _clownBaseY = clownImage.rectTransform.anchoredPosition.y;
        if (showerImage != null) _showerIdleX = showerImage.rectTransform.anchoredPosition.x;
    }

    void OnEnable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup     += HandleCallCueGroup;
        callDirector.OnHoldArmed        += HandleHoldArmed;
        callDirector.OnHoldEarlyRelease += HandleHoldEarlyRelease;
        callDirector.OnGroupJudged      += HandleGroupJudged;
        callDirector.OnResponsePress    += HandlePlayerResponsePress;
    }

    void OnDisable()
    {
        if (callDirector == null) return;
        callDirector.OnCallCueGroup     -= HandleCallCueGroup;
        callDirector.OnHoldArmed        -= HandleHoldArmed;
        callDirector.OnHoldEarlyRelease -= HandleHoldEarlyRelease;
        callDirector.OnGroupJudged      -= HandleGroupJudged;
        callDirector.OnResponsePress    -= HandlePlayerResponsePress;
    }

    void Start()
    {
        if (normalAntImages != null)
            foreach (var img in normalAntImages)
                if (img != null) img.sprite = antSurfaceSprite;

        if (playerAntImage != null) playerAntImage.sprite = playerSurfaceSprite;
        if (clownImage != null) clownImage.sprite = clownSurfaceSprite;
        if (showerImage != null) showerImage.sprite = showerOffSprite;
    }

    void HandleCallCueGroup(int groupId)
    {
        _groupCallCounts.TryGetValue(groupId, out int idx);
        _groupCallCounts[groupId] = idx + 1;

        // Round start (Ant1's call, idx 0) is also when the shower turns on — it stays on for the
        // whole round and turns off again once the round's Response actually resolves (see
        // HandleGroupJudged), not per-character. It also snaps back to its authored resting spot here
        // (step 0 = the Ant1+Ant2 pair) in case a previous round left it mid-slide.
        if (idx == 0 && showerImage != null) showerImage.sprite = showerOnSprite;

        // idx 0..5 = Ant1, Ant2(1st dip, beat1), Ant2(2nd dip, beat2), Ant3(1st dip, beat2),
        // Ant3(2nd dip, beat3), Clown — see class doc. Every one of these is now a plain short pulse;
        // Ant2/Ant3 each just get triggered twice (their own busy-guard naturally allows a second
        // pulse once the first one's edgePulseTime has elapsed, well before the next beat).
        switch (idx)
        {
            case 0: TriggerAntPulse(0); MoveShowerToStep(0); break; // Ant1 dip — pair (Ant1,Ant2), beat 1
            case 1: TriggerAntPulse(1); break;                      // Ant2 1st dip — beat 1
            case 2: TriggerAntPulse(1); MoveShowerToStep(1); break; // Ant2 2nd dip — pair (Ant2,Ant3), beat 2
            case 3: TriggerAntPulse(2); break;                      // Ant3 1st dip — beat 2
            case 4: TriggerAntPulse(2); break;                      // Ant3 2nd dip — beat 3
            case 5: TriggerClownPulse(); MoveShowerToStep(3); break; // Clown dip — pair (Player,Clown), beat 4
            // default: more than 6 Call-role fires in one group shouldn't happen for this chart shape.
        }
    }

    // Steps: 0 = (Ant1,Ant2) — the authored resting X. 1 = (Ant2,Ant3). 2 = (Ant3,Player) — triggered
    // from HandleHoldArmed below, not from here, since nothing in this chart spawns a Call-role entry
    // exactly on the player's own cueBeat (Response entries never fire OnCallCueGroup — see class
    // doc). 3 = (Player,Clown).
    void MoveShowerToStep(int step)
    {
        if (showerImage == null) return;
        float targetX = _showerIdleX + step * showerStepX;
        if (_showerMoveCr != null) StopCoroutine(_showerMoveCr);
        _showerMoveCr = StartCoroutine(MoveShowerRoutine(targetX));
    }

    IEnumerator MoveShowerRoutine(float targetX)
    {
        var rect = showerImage.rectTransform;
        Vector2 start = rect.anchoredPosition;
        var end = new Vector2(targetX, start.y);

        float t = 0f;
        while (t < showerMoveTime)
        {
            t += Time.deltaTime;
            rect.anchoredPosition = Vector2.Lerp(start, end, Mathf.Clamp01(t / showerMoveTime));
            yield return null;
        }
        rect.anchoredPosition = end;
        _showerMoveCr = null;
    }

    void TriggerAntPulse(int antIndex)
    {
        if (normalAntImages == null || antIndex >= normalAntImages.Length) return;
        var img = normalAntImages[antIndex];
        if (img == null || _antCrs[antIndex] != null) return;
        int captured = antIndex;
        _antCrs[antIndex] = StartCoroutine(SpritePulseRoutine(
            img, antSurfaceSprite, antDiveSprite, _antBaseY[antIndex], edgePulseTime, () => _antCrs[captured] = null));
    }

    void TriggerClownPulse()
    {
        if (clownImage == null || _clownCr != null) return;
        _clownCr = StartCoroutine(SpritePulseRoutine(
            clownImage, clownSurfaceSprite, clownDiveSprite, _clownBaseY, edgePulseTime, () => _clownCr = null));
    }

    void HandleHoldArmed(int groupId)
    {
        // The player successfully started holding a live Hold — dive immediately (sprite AND
        // position, straight to diveYOffset). Not gated by a busy-guard coroutine since this is a
        // direct state-set (the real fail/hit events below always override it), not a timed pulse.
        if (playerAntImage != null)
        {
            playerAntImage.sprite = playerDiveSprite;
            SetY(playerAntImage.rectTransform, _playerBaseY + diveYOffset);
        }

        // Shower step 2 = (Ant3,Player) pair. Nothing in the chart spawns a Call-role entry exactly
        // on the player's own cueBeat (Response entries never fire OnCallCueGroup), so this is the
        // only signal that lands at the right moment — see MoveShowerToStep's doc.
        MoveShowerToStep(2);
    }

    void HandleHoldEarlyRelease(int groupId)
    {
        _earlyReleaseGroups.Add(groupId);
    }

    void HandleGroupJudged(int groupId, JudgementType j)
    {
        // The Response Hold resolving is this round's definitive end point (it always lands right
        // around the same beat as the round's last Call, Clown's Tap) — shower off here rather than
        // per-character, since it's one continuous spray for the whole round, not five separate ones.
        // Slides back to its resting spot (step 0) so the next round's MoveShowerToStep(0) is a no-op
        // if it already made it back in time, and a visible slide-back if not.
        if (showerImage != null) showerImage.sprite = showerOffSprite;
        MoveShowerToStep(0);

        if (playerAntImage == null) return;

        bool wasEarlyRelease = _earlyReleaseGroups.Remove(groupId);

        if (_playerFailCr != null) { StopCoroutine(_playerFailCr); _playerFailCr = null; }

        if (j == JudgementType.Hit)
        {
            playerAntImage.sprite = playerSurfaceSprite;
            SetY(playerAntImage.rectTransform, _playerBaseY);
        }
        else
        {
            // Only one fail sprite exists (see playerFailSprite's tooltip), so the "surfaced too
            // early" vs "still submerged" distinction is carried entirely by Y position instead:
            // early release shows the fail sprite up at the surface spot, timeout shows it down at
            // the dive spot.
            float failY = wasEarlyRelease ? _playerBaseY : _playerBaseY + diveYOffset;
            _playerFailCr = StartCoroutine(HoldSpriteThenRevert(
                playerAntImage, playerFailSprite, playerSurfaceSprite, failY, _playerBaseY, failSpriteHoldTime));
        }
    }

    // Fires on every physical press regardless of judgement (see CallDirector.OnResponsePress) —
    // movement-only reaction so the player character visibly responds even during the Call-only
    // portion of a round. Never touches sprite (that's HandleHoldArmed/HandleGroupJudged's job).
    void HandlePlayerResponsePress()
    {
        if (playerAntImage == null || _playerScaleCr != null) return;
        _playerScaleCr = StartCoroutine(ScalePulseRoutine(
            playerAntImage.rectTransform, _playerBaseScale, playerPressScaleY, edgePulseTime,
            () => _playerScaleCr = null));
    }

    static void SetY(RectTransform rect, float y)
    {
        var pos = rect.anchoredPosition;
        pos.y = y;
        rect.anchoredPosition = pos;
    }

    // baseY is the character's authored "surfaced" Y (see _antBaseY/_clownBaseY) — dips down to
    // baseY+diveYOffset for the sprite-active half, then back up to baseY.
    IEnumerator SpritePulseRoutine(Image img, Sprite idleSprite, Sprite activeSprite, float baseY, float duration, Action onComplete)
    {
        img.sprite = activeSprite;
        SetY(img.rectTransform, baseY + diveYOffset);
        yield return new WaitForSeconds(duration);
        img.sprite = idleSprite;
        SetY(img.rectTransform, baseY);
        onComplete?.Invoke();
    }

    IEnumerator HoldSpriteThenRevert(Image img, Sprite hold, Sprite revertTo, float holdY, float revertY, float duration)
    {
        img.sprite = hold;
        SetY(img.rectTransform, holdY);
        yield return new WaitForSeconds(duration);
        img.sprite = revertTo;
        SetY(img.rectTransform, revertY);
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
