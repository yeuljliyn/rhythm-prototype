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

    // 버튼 4개(Canvas 밑에 에디터에서 직접 배치된 오브젝트)의 OnClick()에서 각각 직접 연결하는
    // public 메서드 — 코드로 절차적 생성하던 걸 에디터 오브젝트로 옮기면서 SFX 재생도 각 메서드
    // 안으로 합침(예전엔 버튼 생성 시 람다로 감쌌음).
    public void OnStart()
    {
        SFXManager.PlayClick();
        SettingsManager.Instance?.Close();
        bool tutorialDone = PlayerPrefs.GetInt("tutorial_complete", 0) == 1;
        string next = tutorialDone ? "LobbyScene" : "TutorialIntroScene";
        SceneTransitionManager.Instance.LoadSceneWithFade(next);
    }
    public void OnBonus()    { SFXManager.PlayClick(); SettingsManager.Instance?.Open(1); }
    public void OnSettings() { SFXManager.PlayClick(); SettingsManager.Instance?.Open(); }
    public void OnQuit()     { SFXManager.PlayClick(); Application.Quit(); }
}
