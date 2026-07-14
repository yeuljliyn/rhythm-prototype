using System.Collections;
using UnityEngine;

/// <summary>
/// Placeholder per-lane call-cue visual. Hand-placed in the scene; CallDirector drives
/// it via these public methods. Swap for real minigame character art later.
/// </summary>
public class CallCueIndicator : MonoBehaviour
{
    [SerializeField] SpriteRenderer sr;
    [SerializeField] Color idleColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] Color openColor = Color.yellow;
    [SerializeField] Color demoColor = new Color(0.5f, 0.5f, 1f);
    [SerializeField] Color hitColor  = Color.green;
    [SerializeField] Color missColor = Color.red;
    [SerializeField] float  flashTime = 0.15f;

    Coroutine _cr;

    void Reset() { sr = GetComponent<SpriteRenderer>(); }
    void Awake() { if (sr == null) sr = GetComponent<SpriteRenderer>(); SetColor(idleColor); }

    public void ShowOpen(bool isDemo) => SetColor(isDemo ? demoColor : openColor);
    public void ShowHit()  => Flash(hitColor);
    public void ShowMiss() => Flash(missColor);

    void SetColor(Color c) { if (sr != null) sr.color = c; }

    void Flash(Color c)
    {
        if (_cr != null) StopCoroutine(_cr);
        _cr = StartCoroutine(DoFlash(c));
    }

    IEnumerator DoFlash(Color c)
    {
        SetColor(c);
        yield return new WaitForSeconds(flashTime);
        SetColor(idleColor);
    }
}
