using UnityEngine;

public class BattleSceneTutorialSetup : MonoBehaviour
{
    void Start()
    {
        if (PlayerPrefs.GetInt("is_tutorial", 0) != 1) return;

        var failBG = GameObject.Find("FailureBG");
        if (failBG == null) return;

        var sr = failBG.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        var spr = Resources.Load<Sprite>("튜토_사이버신당");
        if (spr != null) sr.sprite = spr;
    }
}
