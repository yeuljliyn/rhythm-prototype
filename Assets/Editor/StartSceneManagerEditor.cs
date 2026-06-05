using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StartSceneManager))]
public class StartSceneManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        bool done = PlayerPrefs.GetInt("tutorial_complete", 0) == 1;
        string status = done ? "완료됨 (다음 시작 시 로비로 이동)" : "미완료 (다음 시작 시 튜토리얼 진행)";
        EditorGUILayout.LabelField("튜토리얼 상태", status);

        EditorGUI.BeginDisabledGroup(!done);
        if (GUILayout.Button("튜토리얼 초기화 (다시보기)"))
        {
            PlayerPrefs.DeleteKey("tutorial_complete");
            PlayerPrefs.Save();
            Debug.Log("튜토리얼 초기화 완료");
        }
        EditorGUI.EndDisabledGroup();
    }
}
