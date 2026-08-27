using System.Collections;
using UnityEngine;

/// One-shot scale reveal — grows this Transform's localScale from `fromScale` to `toScale` over
/// `duration` seconds, starting automatically on Enable. Doesn't loop or reverse; just plays once.
///
/// Meant for a RectTransform whose pivot is set to the corner (or edge) the effect should visually
/// grow FROM — e.g. 1-3's intro shadow: pivot at (0,0) (bottom-left) so scaling up from 0 reads as
/// the shadow spreading out from that corner toward the center of the screen, instead of growing
/// from the middle outward the way the default (0.5, 0.5) pivot would.
public class ScaleRevealAnimation : MonoBehaviour
{
    [SerializeField] float fromScale = 0f;
    [SerializeField] float toScale   = 1f;
    [SerializeField] float duration  = 1.5f;
    [Tooltip("애니메이션 시작 전 대기 시간(초).")]
    [SerializeField] float delay = 0f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Transform _t;

    void OnEnable()
    {
        _t = transform;
        _t.localScale = Vector3.one * fromScale;
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
            _t.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, p);
            yield return null;
        }
        _t.localScale = Vector3.one * toScale;
    }
}
