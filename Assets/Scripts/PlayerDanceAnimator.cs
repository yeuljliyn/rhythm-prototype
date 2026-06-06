using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerDanceAnimator : MonoBehaviour
{
    public static PlayerDanceAnimator Instance { get; private set; }

    SpriteRenderer _sr;
    Sprite[]       _idleFrames;
    Sprite[]       _dirFrames;      // 상(0) 하(1) 좌(2) 우(3)
    Sprite[]       _falldownFrames; // 0: 쓰러지는 중, 1: 완전히 넘어짐

    const float IdleBeat = 60f / 80f; // 80 BPM — 한 비트마다 프레임 전환

    Coroutine _idleCr;
    Coroutine _dirCr;
    Coroutine _falldownCr;

    void Awake()
    {
        Instance = this;
        _sr = GetComponent<SpriteRenderer>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        _idleFrames     = Resources.LoadAll<Sprite>("잭두둠칫");
        _dirFrames      = Resources.LoadAll<Sprite>("잭두_상하좌우");
        _falldownFrames = Resources.LoadAll<Sprite>("JACKDO_falldown");

        if (_idleFrames != null && _idleFrames.Length > 0)
        {
            _sr.sprite = _idleFrames[0];
            _idleCr    = StartCoroutine(IdleLoop());
        }
    }

    public void PlayFalldown()
    {
        if (_falldownCr != null) return;
        if (_idleCr  != null) { StopCoroutine(_idleCr);  _idleCr  = null; }
        if (_dirCr   != null) { StopCoroutine(_dirCr);   _dirCr   = null; }
        _falldownCr = StartCoroutine(FalldownRoutine());
    }

    IEnumerator FalldownRoutine()
    {
        if (_falldownFrames != null && _falldownFrames.Length >= 2)
        {
            _sr.sprite = _falldownFrames[0];
            yield return new WaitForSeconds(0.15f);
            _sr.sprite = _falldownFrames[1];
            yield return new WaitForSeconds(0.6f);
        }

        _falldownCr = null;
        if (_idleFrames != null && _idleFrames.Length > 0)
            _idleCr = StartCoroutine(IdleLoop());
    }

    // 상=0, 하=1, 좌=2, 우=3
    public void ShowDirection(ArrowDir dir)
    {
        if (_dirFrames == null || _dirFrames.Length < 4) return;
        if (_dirCr != null) StopCoroutine(_dirCr);
        _dirCr = StartCoroutine(DirAnim(dir));
    }

    IEnumerator DirAnim(ArrowDir dir)
    {
        int idx = dir == ArrowDir.Up   ? 0
                : dir == ArrowDir.Down ? 1
                : dir == ArrowDir.Left ? 2
                :                        3;
        if (_idleCr != null) { StopCoroutine(_idleCr); _idleCr = null; }
        _sr.sprite = _dirFrames[idx];
        yield return new WaitForSeconds(0.25f);
        _idleCr = StartCoroutine(IdleLoop());
    }

    IEnumerator IdleLoop()
    {
        int i = 0;
        while (true)
        {
            _sr.sprite = _idleFrames[i % _idleFrames.Length];
            i++;
            yield return new WaitForSeconds(IdleBeat);
        }
    }
}
