using UnityEngine;
using TMPro;

/// <summary>
/// Minimal MVP rank-at-end display. No sprite art required.
/// Reuses GameManager.GetRank()/IsClear as-is. Replace with a BattleResultScreen-style
/// sprite UI once Stage1 art assets exist.
/// </summary>
public class Stage1ResultUI : MonoBehaviour
{
    [SerializeField] CallDirector    director;
    [SerializeField] GameObject      panel;       // hand-placed, inactive by default
    [SerializeField] TextMeshProUGUI rankLabel;
    [SerializeField] TextMeshProUGUI clearLabel;

    void Awake()  { if (panel != null) panel.SetActive(false); }
    void Start()  { if (director != null) director.OnSongEnded += ShowResult; }
    void OnDestroy() { if (director != null) director.OnSongEnded -= ShowResult; }

    void ShowResult()
    {
        var gm    = GameManager.Instance;
        string rank  = gm != null ? gm.GetRank() : "C";
        bool   clear = gm != null && gm.IsClear;

        if (rankLabel  != null) rankLabel.text  = $"RANK {rank}";
        if (clearLabel != null) clearLabel.text = clear ? "STAGE CLEAR" : "TRY AGAIN";
        if (panel != null) panel.SetActive(true);
    }
}
