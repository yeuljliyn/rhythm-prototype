using UnityEngine;

/// One-shot "floating away on the waves" motion — linearly drifts this RectTransform's
/// anchoredPosition3D from `fromPosition` to `toPosition` over `duration` seconds (like
/// PositionRevealAnimation), while continuously layering a gentle vertical bob + slight rocking
/// rotation on top, both driven by sine waves so the motion reads as riding waves rather than
/// sliding in a straight line.
///
/// Meant for a scene-authored starting position (e.g. 1-5's intro: 양동이 탄 광대 — a clown riding a
/// bucket, entering from the right and slowly drifting left while bobbing "출렁출렁, 조금 잔잔하게").
/// Runs once for `duration` seconds starting on Enable, then holds at `toPosition` with the bob still
/// playing (never fully stops — a becalmed bucket still bobs in place) unless `stopBobAtEnd` is set.
public class WaveDriftAnimation : MonoBehaviour
{
    [Header("좌우 이동(파도에 떠내려가는 방향) — 시작 위치는 씬에 배치해둔 현재 위치")]
    [SerializeField] Vector3 fromPosition;
    [SerializeField] Vector3 toPosition;
    [SerializeField] float driftDuration = 5f;
    [Tooltip("애니메이션 시작 전 대기 시간(초).")]
    [SerializeField] float delay = 0f;
    [SerializeField] AnimationCurve driftEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("출렁출렁 — 위아래 흔들림 + 살짝 기울어짐, 둘 다 사인파")]
    [Tooltip("위아래로 흔들리는 폭(px). '조금 잔잔하게'가 기본값 — 크게 하고 싶으면 올릴 것.")]
    [SerializeField] float bobAmplitude = 15f;
    [Tooltip("1초에 몇 번 출렁이는지.")]
    [SerializeField] float bobFrequency = 0.6f;
    [Tooltip("좌우로 살짝 기울어지는 각도(도). 파도에 흔들리는 배/양동이 느낌.")]
    [SerializeField] float rockAmplitude = 4f;
    [Tooltip("bobFrequency와 같은 리듬으로 기울어지게 하려면 1. 다르게 흔들고 싶으면 조절.")]
    [SerializeField] float rockFrequencyMultiplier = 1f;
    [Tooltip("드리프트가 끝난 뒤에도 출렁임은 계속 재생할지 — 꺼두면 도착 위치에서 완전히 멈춤.")]
    [SerializeField] bool bobAfterDriftEnds = true;

    RectTransform _rt;
    float _t;
    bool _driftDone;

    void OnEnable()
    {
        _rt = (RectTransform)transform;
        _rt.anchoredPosition3D = fromPosition;
        _t = 0f;
        _driftDone = false;
    }

    void Update()
    {
        _t += Time.deltaTime;

        Vector3 basePos;
        if (_t < delay)
        {
            basePos = fromPosition;
        }
        else
        {
            float driftT = Mathf.Clamp01((_t - delay) / driftDuration);
            float p = driftEase.Evaluate(driftT);
            basePos = Vector3.Lerp(fromPosition, toPosition, p);
            if (driftT >= 1f) _driftDone = true;
        }

        if (!_driftDone || bobAfterDriftEnds)
        {
            float bob  = Mathf.Sin(_t * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            float rock = Mathf.Sin(_t * bobFrequency * rockFrequencyMultiplier * Mathf.PI * 2f) * rockAmplitude;
            _rt.anchoredPosition3D = basePos + new Vector3(0f, bob, 0f);
            _rt.localEulerAngles = new Vector3(0f, 0f, rock);
        }
        else
        {
            _rt.anchoredPosition3D = basePos;
            _rt.localEulerAngles = Vector3.zero;
        }
    }
}
