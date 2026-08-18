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
///     the rest of the cycle).
///   - CallDirector.OnHoldArmed (fires the instant the player's press claims the live Hold): drives
///     ONLY the player's own dive (sprite + Y) — decoupled from Clown/NormalAnt/shower, see above.
///   - CallDirector.OnGroupJudged: Hit -> player surfaces properly (with the same bubble-then-reveal
///     treatment as Clown/NormalAnt). Miss -> fail sprite, shown at the surfaced Y position if the
///     miss was an early release (OnHoldEarlyRelease fired first) or at the submerged Y position if
///     it was a timeout — see Stage1SurviveVisuals v1's reasoning for why only one fail sprite still
///     needs this position-based distinction (CallDirector.OnHoldArmed/OnHoldEarlyRelease are the
///     same additive events added for v1, unchanged here).
///   - CallDirector.OnResponsePress: player Y-scale pulse on every physical press regardless of
///     judgement, same movement-only pattern used by every other Stage1 minigame this session.
///
/// Bubble anticipation cue ("올라오기 직전마다 머리 위에 기포가 뽀로록"): no art asset exists yet, so
/// this is a fully procedural placeholder (a handful of soft round dots generated at runtime, not
/// loaded from any Sprite) — see BubbleSprite()/BubbleBurstRoutine(). Every surface transition (Clown,
/// NormalAnt, and the player's Hit) fires a bubble burst immediately, then waits `bubbleLeadTime`
/// before actually swapping to the surfaced sprite/position, so the bubbles visually lead the reveal
/// instead of appearing simultaneously with it. Swap this whole mechanism out once real bubble art
/// exists — everything reads it through BubbleBurstRoutine, so that's the only place to touch.
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
    [Tooltip("실패 스프라이트를 보여준 뒤 다시 물밖(대기) 스프라이트로 돌아가기까지 대기 시간")]
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

    [Header("Tuning — 기포 이펙트 (임시, 아트 리소스 없음)")]
    [Tooltip("기포가 뜨기 시작해서 실제로 캐릭터가 물 밖 스프라이트로 바뀌기까지 대기 시간 — " +
             "'올라오기 직전' 예고 역할.")]
    [SerializeField] float bubbleLeadTime = 0.25f;
    [SerializeField] int bubbleCount = 4;
    [SerializeField] float bubbleSpawnInterval = 0.05f;
    [SerializeField] float bubbleLifetime = 0.5f;
    [SerializeField] float bubbleRiseHeight = 40f;
    [SerializeField] float bubbleMinSize = 10f;
    [SerializeField] float bubbleMaxSize = 18f;
    [SerializeField] float bubbleSpreadX = 15f;
    [Tooltip("캐릭터 피벗 기준 기포가 뜨기 시작하는 로컬 Y 오프셋(머리 위쯤)")]
    [SerializeField] float bubbleHeadOffsetY = 90f;

    static Sprite _bubbleSprite;

    // groupId -> how many of this group's Call-role entries have fired OnCallCueGroup so far.
    // idx 0 = Clown, idx 1 = NormalAnt — see class doc.
    readonly System.Collections.Generic.Dictionary<int, int> _groupCallCounts = new System.Collections.Generic.Dictionary<int, int>();

    // groupId -> this group's upcoming Miss (about to arrive via OnGroupJudged) was an early release,
    // not a timeout. Consumed the moment OnGroupJudged actually fires for that groupId.
    readonly System.Collections.Generic.HashSet<int> _earlyReleaseGroups = new System.Collections.Generic.HashSet<int>();

    Coroutine _clownCr;
    Coroutine _antCr;
    Coroutine _submergeCr;
    Coroutine _playerFailCr;
    Coroutine _playerScaleCr;
    Vector3   _playerBaseScale;

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
        // Everyone starts submerged, shower on, player already "holding" — see class doc. The scene-
        // authored position IS the dive pose (see surfaceYOffset's tooltip), so no offset needed here.
        if (clownImage != null) { clownImage.sprite = clownDiveSprite; SetY(clownImage.rectTransform, _clownBaseY); }
        if (normalAntImage != null) { normalAntImage.sprite = antDiveSprite; SetY(normalAntImage.rectTransform, _antBaseY); }
        if (playerAntImage != null) { playerAntImage.sprite = playerDiveSprite; SetY(playerAntImage.rectTransform, _playerBaseY); }
        if (showerImage != null) showerImage.sprite = showerOnSprite;
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
                    // depend on the player's own press (see class doc) — the player's own dive is
                    // handled separately in HandleHoldArmed, driven by their actual input.
                if (_submergeCr != null) StopCoroutine(_submergeCr);
                _submergeCr = StartCoroutine(SubmergeCastNPCsDelayed());
                break;
            // default: more than 3 Call-role fires in one group shouldn't happen for this chart shape.
        }
    }

    // CallDirector.SpawnDueCalls fires OnCallCueGroup as soon as t >= cueTime - hitWindow — i.e. every
    // Call-role cue (Clown/NormalAnt/this one) actually spawns hitWindow seconds BEFORE its nominal
    // beat, uniformly. Surfacing already happens to land close to the true beat because
    // bubbleLeadTime (0.25s) outweighs that early fire, but submerging had no such compensation and
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
        StartCoroutine(BubbleBurstRoutine(clownImage.rectTransform));
        _clownCr = StartCoroutine(DelayedReveal(clownImage, clownSurfaceSprite, _clownBaseY + surfaceYOffset, () => _clownCr = null));
    }

    void SurfaceAnt()
    {
        if (normalAntImage == null || _antCr != null) return;
        StartCoroutine(BubbleBurstRoutine(normalAntImage.rectTransform));
        _antCr = StartCoroutine(DelayedReveal(normalAntImage, antSurfaceSprite, _antBaseY + surfaceYOffset, () => _antCr = null));
    }

    IEnumerator DelayedReveal(Image img, Sprite surfaceSprite, float targetY, Action onComplete)
    {
        yield return new WaitForSeconds(bubbleLeadTime);
        img.sprite = surfaceSprite;
        SetY(img.rectTransform, targetY);
        onComplete?.Invoke();
    }

    void HandleHoldArmed(int groupId)
    {
        // The player's press just claimed the next cycle's Hold — dive immediately. Deliberately
        // ONLY touches the player: Clown/NormalAnt/shower re-submerge on the chart's own beat4Cue
        // instead (see SubmergeCastNPCs), not on the player's personal press timing — see class doc.
        if (playerAntImage != null) { playerAntImage.sprite = playerDiveSprite; SetY(playerAntImage.rectTransform, _playerBaseY); }
    }

    void HandleHoldEarlyRelease(int groupId)
    {
        _earlyReleaseGroups.Add(groupId);
    }

    void HandleGroupJudged(int groupId, JudgementType j)
    {
        if (playerAntImage == null) return;

        bool wasEarlyRelease = _earlyReleaseGroups.Remove(groupId);

        if (_playerFailCr != null) { StopCoroutine(_playerFailCr); _playerFailCr = null; }

        if (j == JudgementType.Hit)
        {
            StartCoroutine(BubbleBurstRoutine(playerAntImage.rectTransform));
            _playerFailCr = StartCoroutine(DelayedReveal(playerAntImage, playerSurfaceSprite, _playerBaseY + surfaceYOffset, () => _playerFailCr = null));
        }
        else
        {
            // Only one fail sprite exists — "surfaced too early" vs "still submerged" is carried by Y
            // position instead: early release shows it up at the surface spot, timeout shows it down
            // at the dive (base) spot.
            float failY = wasEarlyRelease ? _playerBaseY + surfaceYOffset : _playerBaseY;
            _playerFailCr = StartCoroutine(HoldSpriteThenRevert(
                playerAntImage, playerFailSprite, playerSurfaceSprite, failY, _playerBaseY + surfaceYOffset, failSpriteHoldTime));
        }
    }

    // Fires on every physical press regardless of judgement (see CallDirector.OnResponsePress) —
    // movement-only reaction so the player character visibly responds even during the Call-only
    // portion of a cycle. Never touches sprite (that's HandleHoldArmed/HandleGroupJudged's job).
    void HandlePlayerResponsePress()
    {
        if (playerAntImage == null || _playerScaleCr != null) return;
        _playerScaleCr = StartCoroutine(ScalePulseRoutine(
            playerAntImage.rectTransform, _playerBaseScale, playerPressScaleY, pressScalePulseTime,
            () => _playerScaleCr = null));
    }

    static void SetY(RectTransform rect, float y)
    {
        var pos = rect.anchoredPosition;
        pos.y = y;
        rect.anchoredPosition = pos;
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

    // ── Placeholder bubble FX (procedural, no art asset — see class doc) ────────────────────────

    static Sprite BubbleSprite()
    {
        if (_bubbleSprite != null) return _bubbleSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
            float alpha = Mathf.Clamp01(1f - dist / radius);
            alpha *= alpha; // soften the edge instead of a hard-edged disc
            tex.SetPixel(x, y, new Color(0.85f, 0.95f, 1f, alpha));
        }
        tex.Apply();

        _bubbleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _bubbleSprite;
    }

    IEnumerator BubbleBurstRoutine(RectTransform parent)
    {
        var sprite = BubbleSprite();
        for (int i = 0; i < bubbleCount; i++)
        {
            var go = new GameObject("Bubble(Temp)", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;

            float size = UnityEngine.Random.Range(bubbleMinSize, bubbleMaxSize);
            rt.sizeDelta = new Vector2(size, size);
            float xOff = UnityEngine.Random.Range(-bubbleSpreadX, bubbleSpreadX);
            rt.anchoredPosition = new Vector2(xOff, bubbleHeadOffsetY);

            StartCoroutine(AnimateBubble(rt, img));
            yield return new WaitForSeconds(bubbleSpawnInterval);
        }
    }

    IEnumerator AnimateBubble(RectTransform rt, Image img)
    {
        Vector2 start = rt.anchoredPosition;
        Vector2 end = start + new Vector2(0f, bubbleRiseHeight);
        Color c = img.color;

        float t = 0f;
        while (t < bubbleLifetime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / bubbleLifetime);
            rt.anchoredPosition = Vector2.Lerp(start, end, p);
            c.a = 1f - p;
            img.color = c;
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }
}
