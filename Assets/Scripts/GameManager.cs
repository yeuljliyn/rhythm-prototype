using UnityEngine;

public class GameManager : MonoBehaviour
{
    static GameManager _instance;
    public static GameManager Instance
    {
        get { if (_instance == null) _instance = FindObjectOfType<GameManager>(); return _instance; }
        private set { _instance = value; }
    }

    public int Score    { get; private set; }
    public int Combo    { get; private set; }
    public int MaxCombo { get; private set; }

    public int Perfects { get; private set; }
    public int Goods    { get; private set; }
    public int Misses   { get; private set; }
    public int Total    => Perfects + Goods + Misses;
    public float Accuracy => Total > 0 ? (float)(Perfects + Goods) / Total : 0f;

    public string GetRank()
    {
        float acc = Accuracy * 100f;
        if (acc >= 95f) return "S";
        if (acc >= 85f) return "A";
        if (acc >= 70f) return "B";
        return "C";
    }
    public bool IsClear => GetRank() != "C";

    public System.Action<int, int>      OnScoreChanged;
    public System.Action<JudgementType> OnJudgement;

    void Awake()
    {
        if (_instance == null) Instance = this;
        else if (_instance != this) Destroy(gameObject);
    }

    public void RegisterJudgement(JudgementType type)
    {
        switch (type)
        {
            case JudgementType.Perfect:
                Perfects++;
                Combo++;
                Score += 300 + Combo * 10;
                break;
            case JudgementType.Good:
                Goods++;
                Combo++;
                Score += 100 + Combo * 5;
                break;
            case JudgementType.Miss:
                Misses++;
                Combo = 0;
                break;
        }

        if (Combo > MaxCombo) MaxCombo = Combo;

        OnScoreChanged?.Invoke(Score, Combo);
        OnJudgement?.Invoke(type);
    }

    public void ResetGame()
    {
        Score = Combo = MaxCombo = Perfects = Goods = Misses = 0;
        OnScoreChanged?.Invoke(Score, Combo);
    }
}
