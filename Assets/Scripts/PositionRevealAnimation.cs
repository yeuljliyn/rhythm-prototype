using System.Collections;
using UnityEngine;

/// One-shot position reveal — moves this RectTransform's anchoredPosition3D from `fromPosition` to
/// `toPosition` over `duration` seconds, starting automatically on Enable. Doesn't loop or reverse;
/// just plays once.
///
/// Meant for effects that should read as "sliding/spreading into place" rather than growing from a
/// point — e.g. 1-3's intro shadow moving from its corner-off starting position toward center, so it
/// looks like the shadow is being cast across the screen rather than scaling up in place.
[RequireComponent(typeof(RectTransform))]
public class PositionRevealAnimation : MonoBehaviour
{
    [SerializeField] Vector3 fromPosition;
    [SerializeField] Vector3 toPosition;
    [SerializeField] float duration = 1.5f;
    [Tooltip("애니메이션 시작 전 대기 시간(초).")]
    [SerializeField] float delay = 0f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    RectTransform _rt;

    void OnEnable()
    {
        _rt = (RectTransform)transform;
        _rt.anchoredPosition3D = fromPosition;
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = ease.Evaluate(Mathf.Clamp01(t / duration));
            _rt.anchoredPosition3D = Vector3.Lerp(fromPosition, toPosition, p);
            yield return null;
        }
        _rt.anchoredPosition3D = toPosition;
    }
}
