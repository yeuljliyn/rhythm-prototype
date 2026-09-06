using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 채보에 붙은 효과음을 그 박자에 재생한다. 두 종류를 다룸:
///   1) CallEntry.clip — 각 콜/리스폰스 노트에 붙은 소리 (Call Map Editor / 인스펙터에서 지정).
///   2) CallPatternData.sfxCues — Chart Editor의 SFX 레인에 찍는, 노트와 무관한 순수 효과음 큐.
/// 둘 다 판정/입력/오브젝트 반응과 완전히 무관 — CallDirector의 OnCallCue/OnResponsePress 이벤트
/// (개미·손·보스 글루)와는 별개다. SfxCuePlayer와 같은 방식이되, 별도 SfxCuePatternData가 아니라
/// CallDirector가 실제로 돌리는 CallPatternData를 그대로 읽는다.
///
/// CallDirector는 CLAUDE.md 원칙대로 AudioSource를 전혀 모르는 상태를 유지 — 이 컴포넌트가
/// CallDirector와 같은 GameObject에 붙어서 director.Clock.SongTime을 폴링한다. CallEntry 쪽은
/// director.Calls(정렬됨 + PracticeLoopExtender 추가분 포함)를, sfxCues 쪽은 pattern.sfxCues를 훑되
/// 연습 루프 차트면 loopLengthBeats 단위로 스스로 반복시킨다.
///
/// 씬마다 손으로 배선하지 않도록, AspectRatioLetterbox/CameraBackgroundEnforcer와 동일하게
/// RuntimeInitializeOnLoadMethod + sceneLoaded 훅으로 씬 로드 시 모든 CallDirector에 자동 부착한다
/// (Director, PracticeSegment1/2, TutorialDirector, 리믹스 Director, 앞으로 만들 씬까지 전부).
/// </summary>
[RequireComponent(typeof(CallDirector))]
public class CallNoteSfxPlayer : MonoBehaviour
{
    CallDirector _director;
    AudioSource  _audio;

    // CallEntry.clip 재생 커서
    int  _nextIdx;
    bool _caughtUp;

    // sfxCues 재생 커서 (연습 루프면 _sfxRep가 계속 증가)
    List<SfxCueEntry> _sfxCache;
    int  _nextSfxIdx;
    int  _sfxRep;
    bool _sfxCaughtUp;

    void Awake()
    {
        _director = GetComponent<CallDirector>();
        // 음악 소스(CallClock/PracticeLoopExtender가 쓰는 것)와 겹치지 않게 전용 AudioSource를
        // 하나 새로 붙인다. 한 GameObject에 AudioSource 여러 개는 정상.
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
    }

    void Update()
    {
        if (CallDirector.SuppressAll) return; // 결과창 등 — 채보 완전 정지

        var clock = _director.Clock;
        if (clock == null) return;
        var pattern = clock.Pattern;
        if (pattern == null) return;
        if (clock.IsPaused) return; // SongTime이 얼어있음 — 아직 아무 큐도 발화하면 안 됨

        float t = clock.SongTime;

        PlayDueCallEntryClips(pattern, t);
        PlayDueSfxCues(pattern, t);
    }

    // ── 1) CallEntry.clip ──────────────────────────────────────────────────
    void PlayDueCallEntryClips(CallPatternData pattern, float t)
    {
        var calls = _director.Calls;
        if (calls == null || calls.Count == 0) return;

        // Calls는 cueBeat 오름차순 + append-only(AppendCalls)거나, 첫 Update 전에 통째로 교체됨
        // (PracticeLoopExtender.Start의 ReplaceCalls). 리스트가 줄었다면 교체된 것 — 커서 리셋.
        if (_nextIdx > calls.Count) { _nextIdx = 0; _caughtUp = false; }

        // 부트스트랩이 곡 도중에 이 컴포넌트를 붙였을 수도 있으니(sceneLoaded 레이스), 첫 유효
        // 틱에서는 이미 지나간 노트를 '재생 없이' 건너뛴다. 정상 시작(t가 0 근처/음수)이면 없음.
        if (!_caughtUp)
        {
            while (_nextIdx < calls.Count && t >= pattern.BeatToTime(calls[_nextIdx].cueBeat))
                _nextIdx++;
            _caughtUp = true;
            return;
        }

        while (_nextIdx < calls.Count)
        {
            var e = calls[_nextIdx];
            if (t < pattern.BeatToTime(e.cueBeat)) break;
            if (e.clip != null) _audio.PlayOneShot(e.clip, SettingsManager.Volume);
            _nextIdx++;
        }
    }

    // ── 2) CallPatternData.sfxCues ─────────────────────────────────────────
    void PlayDueSfxCues(CallPatternData pattern, float t)
    {
        if (_sfxCache == null)
        {
            _sfxCache = pattern.sfxCues != null
                ? new List<SfxCueEntry>(pattern.sfxCues)
                : new List<SfxCueEntry>();
            _sfxCache.Sort((a, b) => a.beat.CompareTo(b.beat));
        }
        if (_sfxCache.Count == 0) return;

        bool loop = pattern.loopLengthBeats > 0f;

        if (!_sfxCaughtUp)
        {
            // 곡 도중 부착 대비 — 이미 지난 큐를 재생 없이 건너뜀. 연습 루프면 rep도 맞춰 넘김.
            int cg = 0;
            while (t >= SfxTargetTime(pattern, loop) && cg++ < 100000)
            {
                _nextSfxIdx++;
                if (_nextSfxIdx >= _sfxCache.Count)
                {
                    if (!loop) break;
                    _sfxRep++;
                    _nextSfxIdx = 0;
                }
            }
            _sfxCaughtUp = true;
            return;
        }

        int guard = 0; // 한 프레임에 너무 많이 도는 것 방지(비정상 값 대비)
        while (_nextSfxIdx < _sfxCache.Count && guard++ < 512)
        {
            if (t < SfxTargetTime(pattern, loop)) break;
            var cue = _sfxCache[_nextSfxIdx];
            if (cue.clip != null) _audio.PlayOneShot(cue.clip, SettingsManager.Volume);
            _nextSfxIdx++;
            if (loop && _nextSfxIdx >= _sfxCache.Count) { _sfxRep++; _nextSfxIdx = 0; }
        }
    }

    float SfxTargetTime(CallPatternData pattern, bool loop)
    {
        var cue = _sfxCache[_nextSfxIdx];
        float beat = loop ? cue.beat + _sfxRep * pattern.loopLengthBeats : cue.beat;
        return pattern.BeatToTime(beat);
    }

    // ── 씬 로드 시 모든 CallDirector에 자동 부착 ──────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        AttachAll();
        SceneManager.sceneLoaded -= OnSceneLoaded; // 도메인 리로드 꺼진 환경 중복 구독 방지
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachAll();

    static void AttachAll()
    {
        var dirs = Object.FindObjectsByType<CallDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var d in dirs)
            if (!d.TryGetComponent<CallNoteSfxPlayer>(out _))
                d.gameObject.AddComponent<CallNoteSfxPlayer>();
    }
}
