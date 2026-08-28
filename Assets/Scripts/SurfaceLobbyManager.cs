using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 지상 로비 — 스테이지 슬롯 5개를 가로로 늘어놓고, 방향키로 부드럽게 스크롤 이동하는 3겹 패럴랙스
/// 배경(밤하늘/벽/바닥) + 전경(스테이지1 타이틀/광대).
///
/// 벽/전경 레이어는 스크롤 인덱스와 정확히 1:1로 맞물림(parallax=1) — 스테이지마다 다른 그림(벽+
/// 타이틀+광대)이라 해당 스테이지로 이동했을 때 화면 정중앙에 정확히 와야 하기 때문. 전경(타이틀/
/// 광대)은 별도 레이어로 뺐지만 어디까지나 렌더 순서만을 위한 것(바닥 위에 그려져야 광대가 바닥의
/// 맨홀에서 튀어나온 것처럼 보임) — 스크롤 자체는 벽과 완전히 같이 움직임, 화면에 고정되지 않고
/// 다른 슬롯으로 이동하면 자연스럽게 화면 밖으로 스크롤되어 나감. 밤하늘/바닥은 같은 텍스처가
/// 반복될 뿐이라 원근감을 위해 속도를 다르게 줌(밤하늘은 느리게=더 멀리 있는 느낌, 바닥은 빠르게=더
/// 가까이 있는 느낌). 에디터에 미리 배치해둔 각 레이어의 타일 간격(cellWidth)과 스크립트의 cellWidth
/// 값이 반드시 같아야 함 — 실제 아트 원본 비율(2898:1472 ≈ 1.97:1, 딱 16:9는 아님)에 맞춰 잡음.
///
/// 광대는 "선택되어 있다(=스테이지1이 현재 인덱스)"를 나타내는 상태 표시 — 지상_광대1(거의 안
/// 보이는, 맨홀에 들어간 모습)과 지상_광대2(완전히 나온 모습) 두 장이 블링크 애니메이션이 아니라
/// 딱 이 두 상태만 나타냄: 선택 중엔 광대2, 아니면 광대1로 스프라이트만 즉시 스왑. 별도 위치
/// 애니메이션 없음 — 스프라이트가 바뀌는 것 자체가 "맨홀 아래로 들어감"을 표현함.
///
/// 아직 스테이지1만 실제로 입장 가능 — 나머지는 벽 그림만 있고(타이틀 없음) 선택해도 반응 없음.
/// 스테이지1 선택 시: §1 대사(Stage1DialogueScene)를 아직 한 번도 안 봤으면 그리로, 이미 봤으면
/// (오프닝 대사와 같은 패턴 — DialogueSceneManager 참고) 대사 없이 바로 UndergroundLobbyScene으로.
/// </summary>
public class SurfaceLobbyManager : MonoBehaviour
{
    // PlayerPrefs 키 — Stage1DialogueSceneManager.FinishDialogue()가 완주 시 이 키를 세팅함.
    // 게이팅 판단은(오프닝과 마찬가지로) 여기, 즉 대상 씬으로 들어가기 *전*의 호출부에서만 함 —
    // Stage1DialogueScene 안에서 다시 체크하면 그 씬을 에디터에서 직접 열고 Play로 테스트할 때마다
    // 이미 세팅된 플래그 때문에 대사 없이 튕겨버리는 문제가 생김(오프닝 때 실제로 겪음).
    const string Stage1IntroSeenKey = "stage1_underground_intro_seen";

    [SerializeField] RectTransform skyLayer;
    [SerializeField] RectTransform wallLayer;
    [SerializeField] RectTransform floorLayer;
    [SerializeField] RectTransform foregroundLayer; // 스테이지1 타이틀+광대 — 벽과 동일 속도로 스크롤

    [Header("스테이지1 광대 — 선택 상태에 따른 스프라이트 스왑(애니메이션 아님)")]
    [SerializeField] Image  stage1ClownImage;
    [Tooltip("선택 중(index0)일 때 — 완전히 나온 모습")]
    [SerializeField] Sprite clownSelectedSprite;
    [Tooltip("비선택일 때 — 맨홀에 들어간 모습")]
    [SerializeField] Sprite clownDeselectedSprite;

    [Tooltip("스테이지 슬롯 1개의 가로 폭 — 에디터에 배치한 실제 타일 간격과 반드시 같아야 함")]
    [SerializeField] float cellWidth = 2126f;
    [Tooltip("전체 스테이지 슬롯 개수(지금은 5, 벽 그림만 다 있음)")]
    [SerializeField] int stageCount = 5;
    [Tooltip("실제로 입장 가능한 마지막 인덱스(0-based) — 0이면 스테이지1까지만 입장 가능, 나머지는 " +
             "구경만 되고 선택해도 반응 없음")]
    [SerializeField] int maxEnterableIndex = 0;

    [SerializeField] float skyParallax = 0.35f;
    [SerializeField] float floorParallax = 1.15f;
    [Tooltip("스크롤이 목표 위치를 따라잡는 빠르기 — 클수록 빠르게 멈춤")]
    [SerializeField] float scrollSharpness = 6f;

    int   _targetIndex;
    float _wallX;
    bool  _stage1Selected = true; // _targetIndex 기본값 0과 맞춰 시작부터 선택된 상태

    void Awake()
    {
        // 이전 플레이 세션(일시정지 메뉴 등)에서 Time.timeScale=0이 남아있으면 Time.deltaTime이
        // 계속 0이 되어 아래 Lerp가 절대 움직이지 않음 — CallClock/RhythmManager와 동일한 방어 로직.
        Time.timeScale = 1f;

        if (stage1ClownImage != null) stage1ClownImage.sprite = clownSelectedSprite; // 시작은 스테이지1 선택 상태
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.rightArrowKey.wasPressedThisFrame) MoveTo(_targetIndex + 1);
            else if (kb.leftArrowKey.wasPressedThisFrame) MoveTo(_targetIndex - 1);
            else if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) TrySelect();
        }

        float targetX = -_targetIndex * cellWidth;
        // 프레임레이트 무관 지수 감쇠(exponential smoothing) — Time.deltaTime 기반이라
        // Lerp(a,b,t)를 매 프레임 고정 t로 쓰는 것과 달리 프레임이 들쭉날쭉해도 수렴 속도가 일정함.
        _wallX = Mathf.Lerp(_wallX, targetX, 1f - Mathf.Exp(-scrollSharpness * Time.deltaTime));

        SetX(wallLayer, _wallX);
        SetX(foregroundLayer, _wallX); // 벽과 완전히 같은 속도 — 화면 고정 아님, 같이 스크롤됨
        SetX(skyLayer, _wallX * skyParallax);
        SetX(floorLayer, _wallX * floorParallax);

        // _targetIndex(키를 누르는 순간 바로 바뀜) 대신 실제 스크롤된 현재 위치(_wallX)로 판단 —
        // 그래야 화면에 아직 보이는 동안엔 계속 "나온" 모습이다가, 실제로 화면 중앙 근처를 벗어날
        // 때 스왑됨. _targetIndex 기준으로 하면 스크롤이 시작되기도 전에 이미 스프라이트가 바뀌어서
        // 눈에 안 띄고 "항상 나온 모습"으로만 보이는 문제가 있었음(실제로 겪음).
        bool nowSelected = Mathf.Abs(_wallX) < cellWidth * 0.5f;
        if (nowSelected != _stage1Selected)
        {
            _stage1Selected = nowSelected;
            if (stage1ClownImage != null)
                stage1ClownImage.sprite = nowSelected ? clownSelectedSprite : clownDeselectedSprite;
        }
    }

    void MoveTo(int index) => _targetIndex = Mathf.Clamp(index, 0, stageCount - 1);

    void TrySelect()
    {
        if (_targetIndex > maxEnterableIndex) return; // 아직 안 열린 스테이지 — 조용히 무시
        // 지금은 스테이지1 하나뿐이라 분기 없음 — 스테이지가 늘어나면 _targetIndex별로 목적지를 나눌 것.
        bool introSeen = PlayerPrefs.GetInt(Stage1IntroSeenKey, 0) == 1;
        string next = introSeen ? "UndergroundLobbyScene" : "Stage1DialogueScene";
        SceneTransitionManager.Instance.LoadSceneWithFade(next);
    }

    static void SetX(RectTransform rt, float x)
    {
        if (rt == null) return;
        var pos = rt.anchoredPosition;
        pos.x = x;
        rt.anchoredPosition = pos;
    }
}
