using UnityEngine;

/// <summary>
/// Orchestrates Stage1's embedded tutorial → main handoff, in one scene with no reload.
/// The tutorial is just a second, generously-repeated CallDirector/CallClock pair running the
/// short Stage1_Tutorial chart; this component counts real Hits (via GameManager.OnJudgement)
/// until requiredHits lands, then swaps over to the main CallDirector/CallClock (Stage1_MVP_Test)
/// and re-binds the shared Hand/Ants/Boss visuals to it. "무한 관대" isn't implemented as a real
/// rewind — the tutorial chart just has more repeated call/response sets authored into its data
/// than anyone should ever need, and this gate cuts over the moment 3 of them land as Hits.
///
/// First-ever entry to Stage1Scene plays the tutorial; retries skip straight to main via a
/// PlayerPrefs flag.
/// </summary>
public class Stage1TutorialGate : MonoBehaviour
{
    [SerializeField] GameObject tutorialDirectorGO;
    [SerializeField] GameObject mainDirectorGO;
    [SerializeField] Stage1WaterPourVisuals pourVisuals;
    [SerializeField] int requiredHits = 3;
    [SerializeField] string tutorialSeenKey = "stage1_tutorial_seen";

    int  _hitCount;
    bool _listening;

    void Start()
    {
        bool seen = PlayerPrefs.GetInt(tutorialSeenKey, 0) == 1;
        if (seen)
        {
            SwitchToMain();
        }
        else
        {
            if (tutorialDirectorGO != null) tutorialDirectorGO.SetActive(true);
            if (mainDirectorGO     != null) mainDirectorGO.SetActive(false);

            var tutorialDirector = tutorialDirectorGO != null ? tutorialDirectorGO.GetComponent<CallDirector>() : null;
            if (pourVisuals != null && tutorialDirector != null) pourVisuals.BindDirector(tutorialDirector);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnJudgement += HandleTutorialJudgement;
                _listening = true;
            }
        }
    }

    void OnDestroy()
    {
        if (_listening && GameManager.Instance != null)
            GameManager.Instance.OnJudgement -= HandleTutorialJudgement;
    }

    void HandleTutorialJudgement(JudgementType type)
    {
        if (type != JudgementType.Hit) return;
        _hitCount++;
        if (_hitCount >= requiredHits) FinishTutorial();
    }

    void FinishTutorial()
    {
        if (_listening && GameManager.Instance != null)
        {
            GameManager.Instance.OnJudgement -= HandleTutorialJudgement;
            _listening = false;
        }

        PlayerPrefs.SetInt(tutorialSeenKey, 1);
        PlayerPrefs.Save();

        if (tutorialDirectorGO != null) tutorialDirectorGO.SetActive(false);
        GameManager.Instance?.ResetGame(); // wipe tutorial hits/misses — scoring starts fresh at main
        SwitchToMain();
    }

    void SwitchToMain()
    {
        if (tutorialDirectorGO != null) tutorialDirectorGO.SetActive(false);
        if (mainDirectorGO     != null) mainDirectorGO.SetActive(true);

        var mainDirector = mainDirectorGO != null ? mainDirectorGO.GetComponent<CallDirector>() : null;
        if (pourVisuals != null && mainDirector != null) pourVisuals.BindDirector(mainDirector);
    }
}
