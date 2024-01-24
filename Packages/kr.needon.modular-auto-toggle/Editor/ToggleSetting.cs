#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

//v1.0.7
namespace Editor
{
    public class ToggleSetting : EditorWindow
    {
        private bool toggleReverse = false; // 토글의 초기 값
        private string toggleMenuName = "Toggle"; // 첫 번째 텍스트 입력 필드의 초기값
        private string groupToggleMenuName = "GroupToggle"; // 두 번째 텍스트 입력 필드의 초기값
        private static string jsonFilePath = "Assets/Hirami/Toggle/setting.json"; // JSON 파일 경로

        [MenuItem("Hirami/Auto Toggle/Toggle Setting", false, 0)]
        private static void Init()
        {
            var window = GetWindowWithRect<ToggleSetting>(new Rect(0, 0, 600, 400), false, "Toggle Setting");
            window.Show();
            window.LoadJson(); // JSON 파일 불러오기
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical("box");

            GUILayout.Space(10);

            // 텍스트 입력 필드 추가
            toggleMenuName = EditorGUILayout.TextField("Toggle Menu Name", toggleMenuName);
            GUILayout.Space(10);
            groupToggleMenuName = EditorGUILayout.TextField("GroupToggle Menu Name", groupToggleMenuName);

            GUILayout.Space(10);

            toggleReverse = EditorGUILayout.Toggle("토글 반전", toggleReverse);

            GUILayout.Space(10);

            if (GUILayout.Button("적용"))
            {
                ApplySettings();
                SaveJson(); // JSON 파일 저장
            }
            GUILayout.EndVertical();
        }

        private void ApplySettings()
        {
            EditorUtility.DisplayDialog("확인", "설정이 적용되었습니다.", "OK");
        }

        private void LoadJson()
        {
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                ToggleData data = JsonUtility.FromJson<ToggleData>(json);
                toggleReverse = data.toggleReverse;
                toggleMenuName = data.toggleMenuName;
                groupToggleMenuName = data.groupToggleName;
            }
        }

        private void SaveJson()
        {
            ToggleData data = new ToggleData();
            
            data.toggleReverse = toggleReverse;
            data.toggleMenuName = toggleMenuName;
            data.groupToggleName = groupToggleMenuName;
            
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(jsonFilePath, json);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }


    [System.Serializable]
    public class ToggleData
    {
        public bool toggleReverse;
        public string toggleMenuName;
        public string groupToggleName;
    }

}
#endif
