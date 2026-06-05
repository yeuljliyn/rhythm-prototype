using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI hintText;

    void Start()
    {
        if (hintText != null) hintText.text = "";
        gameObject.AddComponent<ScreenVignetteFlash>();
    }
}
