using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "N of 3" progress readout for a practice segment — a row of dots that fill in as Hits land.
/// Stage1PracticeGate calls SetProgress each time the Hit count changes, and Configure() whenever a
/// segment starts (required-hit count itself changes segment to segment only in principle — today
/// every segment uses 3 — but Configure() must still reset the row's visuals every time it's called,
/// not just when the count changes, since a fresh segment always starts back at 0/3).
///
/// The dot row is authored directly in the scene (editor-visible, no Play needed to see or move
/// it — see project convention) rather than built procedurally in code. Configure() just
/// shows/hides the pre-authored dots to match `required`; if a gate ever asks for more dots than
/// were authored, it falls back to creating the extra ones at runtime (matching the authored
/// dots' size/spacing) so nothing breaks, but the common case (every segment uses the same
/// requiredHits) never needs that path.
///
/// Each dot shows one of two art frames (카운트_종아이콘.png, already sliced by the art team —
/// _0 is the lit/golden "rung" bell, _1 is the dim/dark "not yet" bell) instead of the original
/// plain-square color tint. The instant a dot's Hit actually lands, it doesn't just snap to the
/// gold frame — it plays a brief decaying side-to-side rotation wiggle (like a bell being struck
/// and settling) on the still-dark sprite, THEN swaps to the gold sprite once the wiggle finishes.
/// idleSprite/successSprite are optional: if either is left unassigned (a scene that hasn't been
/// wired up yet), that state falls back to the original emptyColor/filledColor tint on a spriteless
/// square, so nothing breaks mid-rollout across scenes.
/// </summary>
public class Stage1PracticeCounter : MonoBehaviour
{
    [Header("씬에 미리 배치된 점들 — Play 없이 에디터에서 위치·크기 조정 가능")]
    [SerializeField] RectTransform root;
    [SerializeField] Image[] dots;

    [Header("아이콘 리소스 (카운트_종아이콘.png — 이미 스프라이트 슬라이싱되어 있음, 슬라이싱 자체는 건드리지 않음)")]
    [Tooltip("아직 그 칸의 Hit이 안 난 상태 — 어둡고 안 울린 방울(카운트_종아이콘_1).")]
    [SerializeField] Sprite idleSprite;
    [Tooltip("그 칸의 Hit이 난 상태 — 밝고 금빛인, 울린 방울(카운트_종아이콘_0).")]
    [SerializeField] Sprite successSprite;

    [Tooltip("Hit이 나는 순간 재생되는 '방울이 짤랑거리는' 흔들림 모션의 최대 진폭(도).")]
    [SerializeField] float ringAmplitudeDegrees = 20f;
    [Tooltip("흔들림 모션이 잦아들어 성공 프레임으로 바뀌기까지 걸리는 전체 시간(초).")]
    [SerializeField] float ringDuration = 0.35f;
    [Tooltip("ringDuration 동안 왕복하는 횟수 — 클수록 더 잘게 떠는 느낌.")]
    [SerializeField] float ringCycles = 3f;

    [Tooltip("dots보다 더 많은 개수가 필요할 때만 쓰이는 폴백 — 기존 점과 같은 간격으로 추가 생성.")]
    [SerializeField] float dotSize = 26f;
    [SerializeField] float dotSpacing = 14f;
    [Tooltip("idleSprite/successSprite가 비어있을 때만 쓰이는 예전 색상 폴백.")]
    [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] Color filledColor = new Color(1f, 0.85f, 0.2f, 1f);

    int _required = -1;
    int _hits;
    Coroutine[] _ringCrs;

    /// (Re)configures the dot row for `required` — safe to call every time a new segment starts.
    /// Always resets progress back to 0/`required` even if `required` itself didn't change from the
    /// previous segment (a fresh segment always starts empty; only the dot-count/spacing setup below
    /// is actually skippable when unchanged).
    public void Configure(int required)
    {
        if (required != _required)
        {
            _required = required;

            if (dots.Length < required)
            {
                var extended = new List<Image>(dots);
                for (int i = dots.Length; i < required; i++)
                    extended.Add(CreateFallbackDot(i));
                dots = extended.ToArray();
            }

            for (int i = 0; i < dots.Length; i++)
                dots[i].gameObject.SetActive(i < required);
        }

        if (_ringCrs == null || _ringCrs.Length != dots.Length)
            _ringCrs = new Coroutine[dots.Length];

        _hits = -1; // force every dot to be treated as "not yet filled" by the reset below
        SetProgress(0);
    }

    public void SetProgress(int hits)
    {
        hits = Mathf.Clamp(hits, 0, dots.Length);

        for (int i = 0; i < dots.Length; i++)
        {
            bool nowFilled  = i < hits;
            bool wasFilled  = i < _hits;

            if (nowFilled && !wasFilled)
                PlayRing(i);
            else if (!nowFilled)
                ResetDot(i);
            // nowFilled && wasFilled: already resolved (mid-ring or already gold) — leave it alone.
        }

        _hits = hits;
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    void PlayRing(int index)
    {
        if (_ringCrs[index] != null) StopCoroutine(_ringCrs[index]);
        _ringCrs[index] = StartCoroutine(RingRoutine(index));
    }

    // Snaps a dot straight back to its idle look with no animation — used for a dot that hasn't
    // been reached yet, or that's being reset at the start of a fresh segment (including one that
    // was still mid-ring from a previous round, which must not keep animating into this one).
    void ResetDot(int index)
    {
        if (_ringCrs != null && _ringCrs[index] != null)
        {
            StopCoroutine(_ringCrs[index]);
            _ringCrs[index] = null;
        }
        var dot = dots[index];
        dot.rectTransform.localEulerAngles = Vector3.zero;
        ApplyIdleVisual(dot);
    }

    // Decaying side-to-side rotation (amplitude shrinks linearly to 0 across ringDuration) so it
    // reads as a bell being struck and settling rather than one clean pendulum arc — then lands on
    // the success sprite exactly as the shake finishes.
    IEnumerator RingRoutine(int index)
    {
        var dot  = dots[index];
        var rect = dot.rectTransform;
        ApplyIdleVisual(dot); // stays on the dark frame for the whole shake — only pops gold at the end

        float t = 0f;
        while (t < ringDuration)
        {
            t += Time.deltaTime;
            float p     = Mathf.Clamp01(t / ringDuration);
            float decay = 1f - p;
            float angle = Mathf.Sin(p * ringCycles * Mathf.PI * 2f) * ringAmplitudeDegrees * decay;
            rect.localEulerAngles = new Vector3(0f, 0f, angle);
            yield return null;
        }

        rect.localEulerAngles = Vector3.zero;
        ApplySuccessVisual(dot);
        _ringCrs[index] = null;
    }

    // preserveAspect=true on both so the bell renders at its own native aspect ratio (109x121)
    // regardless of whatever box size a dot happens to be authored at in a given scene, instead of
    // being non-uniformly stretched to fill it (confirmed reported: the icon reads as squashed
    // slightly sideways compared to the source art).
    void ApplyIdleVisual(Image dot)
    {
        dot.preserveAspect = true;
        if (idleSprite != null) { dot.sprite = idleSprite; dot.color = Color.white; }
        else                    { dot.sprite = null;       dot.color = emptyColor; }
    }

    void ApplySuccessVisual(Image dot)
    {
        dot.preserveAspect = true;
        if (successSprite != null) { dot.sprite = successSprite; dot.color = Color.white; }
        else                       { dot.sprite = null;          dot.color = filledColor; }
    }

    Image CreateFallbackDot(int index)
    {
        var go = new GameObject("Dot" + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(dotSize, dotSize);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(index * (dotSize + dotSpacing) + dotSize * 0.5f, 0f);
        var img = go.GetComponent<Image>();
        img.color = emptyColor;
        img.raycastTarget = false;
        return img;
    }
}
