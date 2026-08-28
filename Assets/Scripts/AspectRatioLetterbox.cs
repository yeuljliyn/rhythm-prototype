using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프로젝트의 모든 UI Canvas는 Screen Space - Overlay + CanvasScaler(ScaleWithScreenSize,
/// referenceResolution 1920x1080)를 씀 — 지금까지 matchWidthOrHeight=0.5(너비/높이 절충)였는데,
/// 21:9 같은 초광각 모니터에서 두 가지 문제가 동시에 생겼음(신고받음: "위아래가 짤리고 옆에 16:9
/// 넘어서는 리소스 끝쪽들이 다 보인다"):
///   - 절충 매칭이라 화면이 기준(16:9)보다 넓을수록 세로로 "덜" 보임 → 위아래가 실제로 잘림.
///   - 동시에 가로로는 기준(1920)보다 "더" 보임 → 1920 폭 기준으로 그려둔 배경/레이아웃의
///     바깥쪽(원래 화면 밖이라고 가정하고 안 그려둔 여백)이 노출됨.
///
/// 근본 수정 두 가지가 합쳐져야 함:
///   1) 모든 CanvasScaler를 matchWidthOrHeight=1(세로/높이 기준 고정)로 바꿈 — 이러면 화면 종횡비와
///      무관하게 세로 1080만큼은 항상 그대로 다 보임(위아래 잘림 완전히 없어짐). 이건 각 씬/코드에서
///      직접 고쳤고, 이 스크립트가 하는 일은 아님.
///   2) 세로를 고정하면 반대로 16:9보다 넓은 화면(울트라와이드)에서는 가로로 1920보다 "더" 보이게
///      되는데(그게 바로 리소스 바깥쪽이 노출되던 원인) — 이 스크립트가 그 초과분을 검은 바(필러박스)로
///      덮어서, 실제로 플레이어 눈에 보이는 건 항상 정확히 1920x1080(16:9) 프레임 하나로 고정되게 함.
///
/// 구현: 모든 다른 Canvas보다 훨씬 높은 sortingOrder로 전용 Canvas 하나를 DontDestroyOnLoad로 딱 한 번
/// 만들고, 그 안에 좌/우 검은 막대 두 장을 "기준 해상도 좌표계에서 x=-960/+960에 각자의 안쪽 모서리가
/// 오도록" 배치함. matchWidthOrHeight=1 조합에서는 이 좌표가 항상 정확히 1920 폭 프레임의 좌우 경계와
/// 일치하므로(스케일은 CanvasScaler가 알아서 함), 한 번 배치해두면 해상도/창 크기가 바뀌어도 매 프레임
/// 다시 계산할 필요가 없음 — Update() 없이 Awake()에서 한 번만 배치.
///
/// 16:9보다 좁은 화면(세로가 긴 모니터 등)은 반대로 좌우가 잘리는 문제가 생기는데, 이번에 신고받은
/// 건 울트라와이드(더 넓은 경우)뿐이라 이 스크립트는 그 케이스만 다룸 — 필요해지면 그때 위/아래
/// 바로 확장.
/// </summary>
public class AspectRatioLetterbox : MonoBehaviour
{
    const float RefWidth  = 1920f;
    const float RefHeight = 1080f;
    const float BarThickness = 4000f; // 어떤 초광각 화면이 와도 충분히 덮을 만큼 넉넉하게

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindObjectOfType<AspectRatioLetterbox>() != null) return;
        var go = new GameObject("[AspectRatioLetterbox]");
        DontDestroyOnLoad(go);
        go.AddComponent<AspectRatioLetterbox>();
    }

    void Awake()
    {
        var cGO = new GameObject("LetterboxCanvas");
        cGO.transform.SetParent(transform, false);
        var canvas = cGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // SettingsManager(100)/Stage1PauseMenuManager(90)보다도 위 — 항상 최상단

        var scaler = cGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 1f; // 다른 모든 Canvas와 동일 — 좌표계가 정확히 일치해야 함

        cGO.AddComponent<GraphicRaycaster>();

        MakeBar(cGO.transform, "LeftBar",  new Vector2(1f, 0.5f), -RefWidth * 0.5f);
        MakeBar(cGO.transform, "RightBar", new Vector2(0f, 0.5f),  RefWidth * 0.5f);
    }

    // pivot.x=1(오른쪽 모서리가 기준점)이면 막대가 기준점에서 왼쪽으로 뻗어나가고,
    // pivot.x=0(왼쪽 모서리가 기준점)이면 오른쪽으로 뻗어나감 — 그래서 안쪽 모서리가 항상 x=±960에
    // 정확히 맞물림.
    static void MakeBar(Transform parent, string name, Vector2 pivot, float x)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = new Vector2(BarThickness, BarThickness);
        rt.anchoredPosition = new Vector2(x, 0f);

        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }
}
