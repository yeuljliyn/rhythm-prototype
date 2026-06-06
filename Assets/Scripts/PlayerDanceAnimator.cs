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

    const float IdleBeat = 60f / 120f; // 120 BPM — 한 비트마다 프레임 전환

    float _refWorldHeight; // 방향 스프라이트 높이 기준값 (스케일 보정용)

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

        // 방향 스프라이트 높이를 기준값으로 기록 (모든 스프라이트가 이 높이로 보정됨)
        if (_dirFrames != null && _dirFrames.Length > 0)
            _refWorldHeight = (_dirFrames[0].rect.height / _dirFrames[0].pixelsPerUnit)
                              * transform.localScale.y;

        if (_idleFrames != null && _idleFrames.Length > 0)
        {
            SetSprite(_idleFrames[0]);
            _idleCr = StartCoroutine(IdleLoop());
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
            SetSprite(_falldownFrames[0]);
            yield return new WaitForSeconds(0.15f);
            SetSprite(_falldownFrames[1]);
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
        SetSprite(_dirFrames[idx]);
        yield return new WaitForSeconds(0.25f);
        _idleCr = StartCoroutine(IdleLoop());
    }

    IEnumerator IdleLoop()
    {
        int i = 0;
        while (true)
        {
            SetSprite(_idleFrames[i % _idleFrames.Length]);
            i++;
            yield return new WaitForSeconds(IdleBeat);
        }
    }

    // 스프라이트를 교체하면서 높이를 방향 스프라이트 기준으로 스케일 보정
    void SetSprite(Sprite s)
    {
        _sr.sprite = s;
        if (s == null || _refWorldHeight <= 0f) return;
        float spriteH = s.rect.height / s.pixelsPerUnit;
        float newScale = _refWorldHeight / spriteH;
        transform.localScale = new Vector3(newScale, newScale, transform.localScale.z);
    }
}
