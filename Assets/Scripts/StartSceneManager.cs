using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StartSceneManager : MonoBehaviour
{
    void Start()
    {
        BuildUI();
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

    void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var bgImg    = bgGO.AddComponent<Image>();
        var bgSprite = Resources.Load<Sprite>("시작화면 배경 이미지");
        if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }
        else bgImg.color = new Color(0.04f, 0.02f, 0.10f, 1f);

        // Logo (left)
        var logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvasGO.transform, false);
        var logoRect = logoGO.AddComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.03f, 0f);
        logoRect.anchorMax = new Vector2(0.75f, 1f);
        logoRect.offsetMin = new Vector2(-30f, 8f);
        logoRect.offsetMax = new Vector2(-140f, -40f);
        var logoImg    = logoGO.AddComponent<Image>();
        var logoSprite = Resources.Load<Sprite>("FeelSoGut로고");
        if (logoSprite != null)
        {
            logoImg.sprite             = logoSprite;
            logoImg.preserveAspect     = true;
            logoImg.color              = Color.white;
        }


        // Buttons
        float btnW = 520f;
        float btnH = 210f;

        string[]   spriteNames = { "시작하기 버튼", "보정버튼", "설정버튼", "종료버튼" };
        float[]    btnYs       = { 253f, 89f, -80f, -253f };
        UnityEngine.Events.UnityAction[] actions = { OnStart, OnBonus, OnSettings, OnQuit };

        for (int i = 0; i < spriteNames.Length; i++)
            CreateButton(canvasGO.transform, spriteNames[i], actions[i], btnW, btnH, btnYs[i]);
    }

    void CreateButton(Transform parent, string spriteName,
                      UnityEngine.Events.UnityAction onClick,
                      float w, float h, float yFromCenter)
    {
        var go = new GameObject(spriteName + "Button");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.80f, 0.5f);
        rect.anchorMax        = new Vector2(0.80f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(0f, yFromCenter);

        var img    = go.AddComponent<Image>();
        var sprite = Resources.Load<Sprite>(spriteName);
        if (sprite != null)
        {
            img.sprite         = sprite;
            img.preserveAspect = true;
        }
        img.color = Color.white;
        img.alphaHitTestMinimumThreshold = 0.1f;

        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() => { SFXManager.PlayClick(); onClick(); });
    }

    void OnStart()
    {
        SettingsManager.Instance?.Close();
        SceneTransitionManager.Instance.LoadSceneWithFade("LobbyScene");
    }
    void OnBonus()    => SettingsManager.Instance?.Open(1);
    void OnSettings() => SettingsManager.Instance?.Open();
    void OnQuit()     => Application.Quit();
}
