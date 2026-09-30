using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class StartSceneManager : MonoBehaviour
{
    void Start()
    {
        PlayBGM("Music/StartBGM");
    }

    void PlayBGM(string resourcePath)
    {
        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null) { Debug.LogWarning($"BGM 로드 실패: {resourcePath}"); return; }
        var go  = new GameObject("BGM");
        var src = go.AddComponent<AudioSource>();
        src.clip        = clip;
        src.loop        = true;
        src.playOnAwake = false;
        src.volume      = SettingsManager.Volume;
        src.Play();
    }

    [ContextMenu("튜토리얼 초기화")]
    void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("tutorial_complete");
        PlayerPrefs.Save();
        Debug.Log("튜토리얼 초기화 완료 — 다음 시작 시 튜토리얼 진행");
    }

    [ContextMenu("오프닝 대사 초기화")]
    void ResetOpening()
    {
        PlayerPrefs.DeleteKey("opening_seen");
        PlayerPrefs.Save();
        Debug.Log("오프닝 대사 초기화 완료 — 다음 시작 시 오프닝부터 재생");
    }

    // 버튼 4개(Canvas 밑에 에디터에서 직접 배치된 오브젝트)의 OnClick()에서 각각 직접 연결하는
    // public 메서드 — 코드로 절차적 생성하던 걸 에디터 오브젝트로 옮기면서 SFX 재생도 각 메서드
    // 안으로 합침(예전엔 버튼 생성 시 람다로 감쌌음).
    //
    // 새 로비 플로우(StartScene → 오프닝 대사 → 지상 로비)로 전환하면서 예전 tutorial_complete/
    // TutorialIntroScene/LobbyScene 분기는 제거함 — 이 씬들은 이제 참고용으로만 남고 실제 플레이
    // 경로에서 빠짐(사용자 확인: "이제 뭐 참고하는 용도 아니면 다시 쓸 일 없을 것 같긴 해").
    // opening_seen은 DialogueScene(오프닝 대사, §0)이 다 재생됐을 때 세팅함 — 딱 한 번만 나옴.
    public void OnStart()
    {
        SFXManager.PlayClick();
        SettingsManager.Instance?.Close();
        bool openingSeen = PlayerPrefs.GetInt("opening_seen", 0) == 1;
        string next = openingSeen ? "SurfaceLobbyScene" : "DialogueScene";
        SceneTransitionManager.Instance.LoadSceneWithFade(next);
    }
    // "처음부터" — 이야기 진행 플래그를 전부 지우고 §0 오프닝부터 다시. 스타트 씬의 텍스트 버튼(NewGameButton)에
    // 배선. 테스트 중 플래그가 쌓여 대사가 하나도 안 나오는 상황("스타트부터 돌렸는데 대사가 사라짐")을
    // 에디터 ContextMenu 없이 게임 안에서 풀 수 있게. 해금 진행도(underground_unlocked_index)도 같이 지운다.
    public void OnNewGame()
    {
        SFXManager.PlayClick();
        SettingsManager.Instance?.Close();
        ResetAllProgress();
        SceneTransitionManager.Instance.LoadSceneWithFade("DialogueScene");
    }

    public static void ResetAllProgress()
    {
        PlayerPrefs.DeleteKey("opening_seen");
        PlayerPrefs.DeleteKey("stage1_underground_intro_seen");
        for (int i = 0; i < 5; i++) PlayerPrefs.DeleteKey(Stage1PostDialogue.SeenKey(i));
        PlayerPrefs.DeleteKey("underground_unlocked_index");
        PlayerPrefs.DeleteKey("underground_last_index");
        PlayerPrefs.Save();
        Debug.Log("[StartScene] 진행 초기화 — 오프닝·§1·판 후 대사·해금 전부 리셋");
    }

    [ContextMenu("진행 전체 초기화(처음부터)")]
    void ResetAllProgressMenu() => ResetAllProgress();

    public void OnBonus()    { SFXManager.PlayClick(); SettingsManager.Instance?.Open(1); }
    public void OnSettings() { SFXManager.PlayClick(); SettingsManager.Instance?.Open(); }
    public void OnQuit()     { SFXManager.PlayClick(); Application.Quit(); }
}
