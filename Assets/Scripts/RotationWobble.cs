using UnityEngine;

/// Oscillates this Transform's local Z rotation back and forth between -amplitudeDegrees and
/// +amplitudeDegrees on a sine wave — e.g. a speech bubble idly wobbling in an intro scene.
/// Purely visual, runs off Time.time so it needs no external trigger/state.
public class RotationWobble : MonoBehaviour
{
    [SerializeField] float amplitudeDegrees = 15f;
    [Tooltip("한 번 왕복(0 -> +amplitude -> 0 -> -amplitude -> 0)하는 데 걸리는 시간(초).")]
    [SerializeField] float periodSeconds = 2f;

    Transform _t;
    Vector3 _baseEuler;

    void Awake()
    {
        _t = transform;
        _baseEuler = _t.localEulerAngles;
    }

    void Update()
    {
        float angle = Mathf.Sin(Time.time * (2f * Mathf.PI / periodSeconds)) * amplitudeDegrees;
        _t.localEulerAngles = new Vector3(_baseEuler.x, _baseEuler.y, angle);
    }
}
