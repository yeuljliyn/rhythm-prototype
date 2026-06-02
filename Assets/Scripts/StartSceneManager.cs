using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class StartSceneManager : MonoBehaviour
{
    void Start()
    {
        BuildUI();
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
        bgGO.AddComponent<Image>().color = new Color(0.04f, 0.02f, 0.10f, 1f);

        // Logo (left)
        var logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvasGO.transform, false);
        var logoRect = logoGO.AddComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0f, 0.35f);
        logoRect.anchorMax = new Vector2(0.55f, 0.78f);
        logoRect.offsetMin = logoRect.offsetMax = Vector2.zero;
        var logoText      = logoGO.AddComponent<TextMeshProUGUI>();
        logoText.text         = "FEELSOGUT";
        logoText.fontSize     = 130f;
        logoText.fontStyle    = FontStyles.Bold;
        logoText.alignment    = TextAlignmentOptions.Center;
        logoText.color        = new Color(0.20f, 1.00f, 0.45f, 1f);
        logoText.outlineWidth = 0.3f;
        logoText.outlineColor = new Color(0.00f, 0.55f, 0.20f, 1f);

        // Subtitle
        var subGO = new GameObject("Subtitle");
        subGO.transform.SetParent(canvasGO.transform, false);
        var subRect = subGO.AddComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.05f, 0.26f);
        subRect.anchorMax = new Vector2(0.55f, 0.36f);
        subRect.offsetMin = subRect.offsetMax = Vector2.zero;
        var subText              = subGO.AddComponent<TextMeshProUGUI>();
        subText.text             = "RHYTHM GAME";
        subText.fontSize         = 30f;
        subText.alignment        = TextAlignmentOptions.Center;
        subText.color            = new Color(0.5f, 0.9f, 0.6f, 0.7f);
        subText.characterSpacing = 12f;

        // Buttons — fixed size, stacked manually (avoid VerticalLayoutGroup font conflict)
        float btnW   = 340f;
        float btnH   = 90f;
        float btnGap = 20f;
        float startY = 180f;  // from center

        string[]   labels  = { "START", "CALIBRATION", "SETTINGS", "QUIT" };
        Color[]    colors  = {
            new Color(0.10f, 0.75f, 0.35f),
            new Color(0.12f, 0.45f, 0.70f),
            new Color(0.25f, 0.25f, 0.40f),
            new Color(0.50f, 0.10f, 0.12f),
        };
        UnityEngine.Events.UnityAction[] actions = { OnStart, OnBonus, OnSettings, OnQuit };

        for (int i = 0; i < labels.Length; i++)
        {
            float yOffset = startY - i * (btnH + btnGap);
            CreateButton(canvasGO.transform, labels[i], colors[i], actions[i], btnW, btnH, yOffset);
        }
    }

    void CreateButton(Transform parent, string label, Color color,
                      UnityEngine.Events.UnityAction onClick,
                      float w, float h, float yFromCenter)
    {
        var go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.75f, 0.5f);
        rect.anchorMax        = new Vector2(0.75f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(0f, yFromCenter);

        go.AddComponent<Image>().color = color;

        var btn = go.AddComponent<Button>();
        var c   = btn.colors;
        c.highlightedColor = new Color(Mathf.Min(color.r + 0.2f, 1f), Mathf.Min(color.g + 0.2f, 1f), Mathf.Min(color.b + 0.2f, 1f));
        c.pressedColor     = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f);
        btn.colors = c;
        btn.onClick.AddListener(onClick);

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txtRect = txtGO.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;

        var txt       = txtGO.AddComponent<TextMeshProUGUI>();
        txt.text      = label;
        txt.fontSize  = 40f;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color     = Color.white;
    }

    void OnStart()
    {
        SettingsManager.Instance?.Close();
        SceneManager.LoadScene("LobbyScene");
    }
    void OnBonus()    => SettingsManager.Instance?.Open(1);
    void OnSettings() => SettingsManager.Instance?.Open();
    void OnQuit()     => Application.Quit();
}
