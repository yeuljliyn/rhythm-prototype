using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 지하 로비 — 스테이지 1-1~1-5 슬롯을 가로로 늘어놓고, 방향키로 스크롤 이동. 지상 로비와 달리
/// 밤하늘/바닥처럼 반복되는 공용 텍스처가 없음 — 배경/벽/바닥/타이틀 전부 스테이지마다 그림이 다른
/// 전용 아트(1920x1080, 정확히 16:9). 대신 벽마다 뚫린 구멍으로 "그 스테이지만의" 배경이 보이는
/// 구조라, 원근감을 표현하는 방식이 지상 로비(반복 텍스처에 일정한 배속을 곱함)와 다름:
///
///   - 벽/바닥/타이틀/가림벽(ForegroundGroup) — 스크롤 인덱스와 정확히 1:1(parallax 개념 없이 그냥
///     같이 움직임). 스테이지마다 다른 그림이라 해당 인덱스에서 화면 정중앙에 정확히 와야 함.
///   - 배경(BackgroundLayer) — 원근감(지연/진폭 축소/흔들림)을 여러 방식으로 시도했지만 튜닝에
///     시간이 너무 걸려서(요청: "일단 고치는데 너무 걸려서 일단 그냥 뒷배경 고정시켜놓자 움직이지
///     말고") 전부 걷어내고, 지금은 벽/바닥/타이틀과 완전히 같은 간격(cellWidth)으로 배치되고 매 프레임
///     벽과 정확히 같은 위치(_fgX)로 그냥 고정 — 스크롤 애니메이션도 원근감 연출도 없이 벽이 움직이면
///     배경도 1:1로 같이 움직여서 항상 구멍 자리에 딱 맞음. 나중에 시간 날 때 원근감을 다시 시도할
///     수 있으니 완전히 지우지 않고 이 메모만 남겨둠.
///
/// 가림벽(지하로비_가림벽.png, 공용 1장): 아직 해금 안 된 슬롯의 구멍을 덮어서 막힌 벽처럼 보이게
/// 함 — 이건 "멀리 있는" 게 아니라 "그냥 벽"이라는 뜻이라 원근감(지연) 없이 벽/바닥과 정확히 같은
/// 속도로 움직임(ForegroundGroup 소속). 배경보다 앞, 벽보다 뒤에 그려서 구멍 자리만 자연스럽게
/// 덮임.
///
/// 해금 상태는 아직 이 로비 전용 저장 시스템이 없어서 maxEnterableIndex를 코드에 고정 — 지금은
/// 1-1(인덱스0)만 해금. 나중에 실제 저장/진행 시스템이 생기면 여기서 그 값을 읽어오도록 바꿀 것.
/// </summary>
public class UndergroundLobbyManager : MonoBehaviour
{
    [SerializeField] RectTransform backgroundLayer;
    [SerializeField] RectTransform foregroundGroup;

    [Tooltip("스테이지 슬롯 1개의 가로 폭 — 에디터에 배치한 실제 타일 간격과 반드시 같아야 함")]
    [SerializeField] float cellWidth = 1920f;
    [SerializeField] int stageCount = 5;
    [Tooltip("실제로 입장 가능한 마지막 인덱스(0-based) — 0이면 1-1까지만 입장 가능, 나머지는 " +
             "구경만 되고(가림벽으로 막혀 있음) 선택해도 반응 없음")]
    [SerializeField] int maxEnterableIndex = 0;

    [Tooltip("벽/바닥/타이틀/가림벽/배경이 목표 위치를 따라잡는 빠르기")]
    [SerializeField] float scrollSharpness = 6f;

    int   _targetIndex;
    float _fgX;

    void Awake()
    {
        // 이전 플레이 세션에서 Time.timeScale=0이 남아있으면 Time.deltaTime이 계속 0이 되어 아래
        // Lerp가 절대 움직이지 않음 — CallClock/RhythmManager/SurfaceLobbyManager와 동일한 방어 로직.
        Time.timeScale = 1f;

        LobbyBGMPlayer.Ensure(); // 지상/지하 로비 공용 BGM — 지상 로비에서 내려온 거면 이미 재생 중이라 no-op

        // 인스펙터에 찍힌 값 대신 실제 저장된 해금 진행도를 씀 — Stage1ResultScreen이 랭크 A 이상
        // 클리어 시 UndergroundLobbyProgress.UnlockUpTo(...)로 갱신한 값.
        maxEnterableIndex = UndergroundLobbyProgress.GetUnlockedIndex();
        ApplyUnlockState();

        // 스테이지 플레이하고 돌아왔을 때 항상 1-1로 리셋되지 않고 마지막으로 있던 자리 그대로
        // 보이게 함(요청: "다시 지하로돌아올때 마지막에 있던 위치로 돌아와야지"). 해금 범위를 벗어난
        // 저장값(예: 진행도 초기화 등으로 어긋난 경우)은 방어적으로 clamp.
        _targetIndex = Mathf.Clamp(UndergroundLobbyProgress.GetLastIndex(), 0, stageCount - 1);
        _fgX = -_targetIndex * cellWidth; // 애니메이션 없이 그 자리에서 바로 시작(1-1을 거쳐가지 않음)
    }

    // 씬 빌드 시점엔 아직 안 열린 슬롯(1-2~1-5)마다 가림벽(Blocker2~5) 오브젝트를 미리 다 심어놨음 —
    // 그건 "고정된 배치"일 뿐이고 실제 해금 여부는 런타임에 결정되므로, 저장된 진행도만큼 가림벽을
    // 꺼줘야 함. 이걸 안 하면 진행도가 올라가도(1-2 해금) 가림벽이 그대로 남아있어서 안 열린 것처럼
    // 보이는 버그가 생김(실제로 겪음).
    void ApplyUnlockState()
    {
        if (foregroundGroup == null) return;
        for (int i = 1; i <= maxEnterableIndex; i++) // Blocker1은 애초에 없음(인덱스0은 항상 해금)
        {
            var blocker = foregroundGroup.Find("Blocker" + (i + 1));
            if (blocker != null) blocker.gameObject.SetActive(false);
        }
    }

    /// <summary>지상 로비로 돌아가기 버튼에 연결.</summary>
    public void GoSurfaceLobby()
    {
        SFXManager.PlayBack();
        SceneTransitionManager.Instance.LoadSceneWithFade("SurfaceLobbyScene");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.rightArrowKey.wasPressedThisFrame) MoveTo(_targetIndex + 1);
            else if (kb.leftArrowKey.wasPressedThisFrame) MoveTo(_targetIndex - 1);
            else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) TrySelect();
            else if (kb.escapeKey.wasPressedThisFrame) GoSurfaceLobby();
        }

        float targetX = -_targetIndex * cellWidth;
        // 프레임레이트 무관 지수 감쇠(exponential smoothing).
        _fgX = Mathf.Lerp(_fgX, targetX, 1f - Mathf.Exp(-scrollSharpness * Time.deltaTime));

        SetX(foregroundGroup, _fgX);
        SetX(backgroundLayer, _fgX); // 원근감 없이 벽과 완전히 같은 위치로 고정
    }

    void MoveTo(int index)
    {
        int clamped = Mathf.Clamp(index, 0, stageCount - 1);
        if (clamped == _targetIndex) return; // 이미 끝 슬롯 — 헛 이동음 안 냄
        _targetIndex = clamped;
        UndergroundLobbyProgress.SaveLastIndex(_targetIndex); // 다음에 돌아왔을 때 이 자리부터 시작
        SFXManager.PlayNavigate();
    }

    // Stage1Scene(구 물타기 프로토타입)은 이제 안 쓰는 씬이라 여기서 목적지로 안 씀(사용자 확인 완료).
    // 인덱스 순서 = 1-1,1-2,1-3,1-4,1-5.
    static readonly string[] StageIntroScenes =
    {
        "Stage1_1IntroScene",
        "Stage1_2IntroScene",
        "Stage1_3IntroScene",
        "Stage1_4IntroScene",
        "Stage1_5IntroScene",
    };

    void TrySelect()
    {
        if (_targetIndex > maxEnterableIndex) return; // 가림벽으로 막힌 슬롯 — 조용히 무시
        if (_targetIndex >= StageIntroScenes.Length) return; // 아직 목적지 안 정해진 스테이지
        SFXManager.PlayConfirm();
        // 판에 들어갈 땐 검정 페이드 대신 연기 와이프 — "판에 들어간다 = 개미로 변해 들어간다"(docs/design.md 인트로 등장)
        SceneTransitionManager.Instance.LoadSceneWithSmoke(StageIntroScenes[_targetIndex]);
    }

    static void SetX(RectTransform rt, float x)
    {
        if (rt == null) return;
        var pos = rt.anchoredPosition;
        pos.x = x;
        rt.anchoredPosition = pos;
    }
}
