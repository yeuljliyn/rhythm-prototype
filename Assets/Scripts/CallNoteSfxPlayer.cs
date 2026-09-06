using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 채보의 각 노트(CallEntry.clip)에 붙은 효과음을 그 노트의 cueBeat에 한 번씩 재생한다.
/// 판정/입력/오브젝트 반응과 완전히 무관한 순수 소리 — CallDirector의 OnCallCue/OnResponsePress
/// 이벤트(개미·손·보스 글루)와는 별개다. SfxCuePlayer와 같은 방식이되, 별도 SfxCuePatternData가
/// 아니라 CallDirector가 실제로 돌리는 CallPatternData를 그대로 읽는다.
///
/// CallDirector는 CLAUDE.md 원칙대로 AudioSource를 전혀 모르는 상태를 유지 — 이 컴포넌트가
/// CallDirector와 같은 GameObject에 붙어서 director.Clock.SongTime을 폴링하고
/// director.Calls(정렬됨 + PracticeLoopExtender 추가분 포함)를 훑는다.
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
    int          _nextIdx;
    bool         _caughtUp;

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

        var calls = _director.Calls;
        if (calls == null || calls.Count == 0) return;

        // Calls는 cueBeat 오름차순 + append-only(AppendCalls)거나, 첫 Update 전에 통째로 교체됨
        // (PracticeLoopExtender.Start의 ReplaceCalls). 리스트가 줄었다면 교체된 것 — 커서 리셋.
        if (_nextIdx > calls.Count) { _nextIdx = 0; _caughtUp = false; }

        float t = clock.SongTime;

        // 부트스트랩이 곡 도중에 이 컴포넌트를 붙였을 수도 있으니(sceneLoaded 레이스), 첫 유효
        // 틱에서는 이미 지나간 노트를 '재생 없이' 건너뛴다. 정상 시작(t가 0 근처/음수)이면
        // 건너뛸 게 없다.
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
