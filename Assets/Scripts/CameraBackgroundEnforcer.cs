using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 모든 씬의 화면-클리어 카메라 배경을 런타임에 "검정 단색"으로 강제한다.
///
/// 유니티가 새 카메라를 만들 때 기본값이 Skybox 클리어 + 파란색 배경이라, 21:9 같은
/// 비율에서 (레터박스/배경 이미지가 안 덮는) 화면 밖 영역이 그 파란색으로 뜬다.
/// 기존 씬들은 에디터에서 일괄로 SolidColor/검정으로 바꿔뒀지만, 앞으로 새로 만들
/// 씬마다 매번 손대지 않아도 되도록 이 스크립트가 씬 로드 때마다 자동 처리한다.
///
/// 프레임을 실제로 지우는 카메라(Skybox / SolidColor)만 건드리고, Depth/Nothing으로
/// 세팅된 오버레이/UI 스택 카메라는 그대로 둔다. AspectRatioLetterbox와 같은
/// RuntimeInitializeOnLoadMethod 부트스트랩 패턴.
/// </summary>
public static class CameraBackgroundEnforcer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        Apply();
        SceneManager.sceneLoaded -= OnSceneLoaded; // 도메인 리로드 꺼진 환경에서 중복 구독 방지
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    static void Apply()
    {
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in cams)
        {
            if (cam.clearFlags != CameraClearFlags.Skybox && cam.clearFlags != CameraClearFlags.SolidColor)
                continue;
            cam.clearFlags     = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }
}
