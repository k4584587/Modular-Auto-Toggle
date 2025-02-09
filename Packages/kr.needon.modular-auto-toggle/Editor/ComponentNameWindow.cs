#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ComponentNameWindow : EditorWindow
{
    private string componentName = "Toggles"; // 기본 이름
    private bool initialized = false; // 텍스트 필드 초기 포커스 설정용

    public static string OpenComponentNameDialog()
    {
        var window = CreateInstance<ComponentNameWindow>();
        window.titleContent = new GUIContent("Enter Toggle Name\n    토글 이름 입력");
        // 창 크기를 늘리고 고정
        window.minSize = new Vector2(380, 140);
        window.maxSize = new Vector2(380, 140);
        window.ShowModal();
        return window.componentName;
    }

    private void OnGUI()
    {
        // 라벨용 커스텀 GUIStyle
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        };

        // 텍스트 필드용 커스텀 GUIStyle
        GUIStyle textFieldStyle = new GUIStyle(EditorStyles.textField)
        {
            fontSize = 12
        };

        GUILayout.Space(10); // 상단 여백

        // 중앙 정렬된 라벨 (영어와 한글, 한 줄 띄어쓰기로 구분)
        GUILayout.Label("Enter the name for the new Toggle Name\n    새 토글 이름 입력:", labelStyle);

        GUILayout.Space(10); // 라벨과 텍스트 필드 사이 여백

        // 중앙 정렬된 텍스트 필드
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.SetNextControlName("ComponentNameField");
        componentName = EditorGUILayout.TextField(componentName, textFieldStyle, GUILayout.Width(200));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        // 텍스트 필드에 한 번만 포커스 설정
        if (!initialized)
        {
            EditorGUI.FocusTextInControl("ComponentNameField");
            initialized = true;
        }

        GUILayout.Space(20); // 버튼 위 여백

        // 중앙 정렬된 버튼 (영어와 한글, 한 줄 띄어쓰기로 구분)
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("OK", GUILayout.Width(100)))
        {
            Close(); // 창 닫기
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }
}
#endif
