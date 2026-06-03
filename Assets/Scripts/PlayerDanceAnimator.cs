using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerDanceAnimator : MonoBehaviour
{
    SpriteRenderer _sr;
    Sprite[]       _idleFrames;
    Sprite[]       _dirFrames;   // 상(0) 하(1) 좌(2) 우(3)

    const float IdleFps = 6f;

    Coroutine _idleCr;
    Coroutine _dirCr;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        _idleFrames = Resources.LoadAll<Sprite>("잭두_춤1");
        _dirFrames  = Resources.LoadAll<Sprite>("잭두_상하좌우");

        if (_idleFrames != null && _idleFrames.Length > 0)
        {
            _sr.sprite = _idleFrames[0];
            _idleCr    = StartCoroutine(IdleLoop());
        }
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
            yield return new WaitForSeconds(1f / IdleFps);
        }
    }
}
