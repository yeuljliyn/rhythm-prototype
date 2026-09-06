using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지상/지하 로비 공용 배경음악(Assets/Resources/Music/LobbyBGM.wav). 두 로비 씬을 오갈 때
/// 음악이 끊기지 않고 이어지도록 DontDestroyOnLoad 싱글톤으로 유지 — 각 로비 매니저의 Awake()에서
/// LobbyBGMPlayer.Ensure()만 호출하면 됨(이미 재생 중이면 아무것도 안 함). 로비가 아닌 씬으로
/// 넘어가면 sceneLoaded에서 스스로 정리. StartSceneManager의 StartBGM(씬마다 새로 만드는 방식)과
/// 같은 컨벤션을 따르되, 로비만 지속 재생이 필요해서 별도 컴포넌트로 분리.
/// </summary>
public class LobbyBGMPlayer : MonoBehaviour
{
    const string ResourcePath = "Music/LobbyBGM";
    const float  BgmLevel = 0.55f; // BGM은 UI 효과음(클릭/이동/선택)에 묻히지 않게 살짝 낮춰서
    static readonly string[] LobbyScenes = { "SurfaceLobbyScene", "UndergroundLobbyScene" };

    static LobbyBGMPlayer _instance;
    AudioSource _src;

    /// <summary>로비 씬 매니저의 Awake()에서 호출. 이미 재생 중이면 볼륨만 갱신하고 리턴.</summary>
    public static void Ensure()
    {
        if (_instance != null)
        {
            if (_instance._src != null) _instance._src.volume = SettingsManager.Volume * BgmLevel;
            return;
        }

        var clip = Resources.Load<AudioClip>(ResourcePath);
        if (clip == null) { Debug.LogWarning($"[LobbyBGM] 로드 실패: {ResourcePath}"); return; }

        var go = new GameObject("LobbyBGM");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LobbyBGMPlayer>();
        _instance._src = go.AddComponent<AudioSource>();
        _instance._src.clip        = clip;
        _instance._src.loop        = true;
        _instance._src.playOnAwake = false;
        _instance._src.volume      = SettingsManager.Volume * BgmLevel;
        _instance._src.Play();

        SceneManager.sceneLoaded += _instance.OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (System.Array.IndexOf(LobbyScenes, scene.name) >= 0) return; // 로비 → 로비: 계속 재생
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _instance = null;
        Destroy(gameObject);
    }
}
