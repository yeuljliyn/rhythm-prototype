using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placeholder "N of 3" progress readout for a practice segment — a row of dots that fill in as
/// Hits land. Stage1PracticeGate calls SetProgress each time the Hit count changes, and Configure()
/// whenever the required-hit count itself changes (segment to segment could in principle ask for a
/// different count, though today every segment uses 3).
///
/// The dot row is authored directly in the scene (editor-visible, no Play needed to see or move
/// it — see project convention) rather than built procedurally in code. Configure() just
/// shows/hides the pre-authored dots to match `required`; if a gate ever asks for more dots than
/// were authored, it falls back to creating the extra ones at runtime (matching the authored
/// dots' size/spacing) so nothing breaks, but the common case (every segment uses the same
/// requiredHits) never needs that path.
/// </summary>
public class Stage1PracticeCounter : MonoBehaviour
{
    [Header("씬에 미리 배치된 점들 — Play 없이 에디터에서 위치·크기 조정 가능")]
    [SerializeField] RectTransform root;
    [SerializeField] Image[] dots;

    [Tooltip("dots보다 더 많은 개수가 필요할 때만 쓰이는 폴백 — 기존 점과 같은 간격으로 추가 생성.")]
    [SerializeField] float dotSize = 26f;
    [SerializeField] float dotSpacing = 14f;
    [SerializeField] Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] Color filledColor = new Color(1f, 0.85f, 0.2f, 1f);

    int _required = -1;

    /// (Re)configures the dot row for `required` — safe to call every time a new segment starts
    /// even if the count didn't change (no-ops in that case).
    public void Configure(int required)
    {
        if (required == _required) return;
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

        SetProgress(0);
    }

    public void SetProgress(int hits)
    {
        for (int i = 0; i < dots.Length; i++)
            dots[i].color = i < hits ? filledColor : emptyColor;
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

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
