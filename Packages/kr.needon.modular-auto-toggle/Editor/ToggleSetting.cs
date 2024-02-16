#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

//v1.0.64
namespace Editor
{
    public class ToggleSetting : EditorWindow
    {
        private bool toggleReverse = false;
        private string toggleMenuName = "Toggles"; 
        private string groupToggleMenuName = "GroupToggles";
        private static string jsonFilePath = "Assets/Hirami/Toggle/setting.json";

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

            toggleReverse = EditorGUILayout.Toggle("토글 반전 (Toggle Reverse) ", toggleReverse);

            GUILayout.Space(10);

            if (GUILayout.Button("Apply"))
            {
                ApplySettings();
                SaveJson(); // JSON 파일 저장
            }
            
            if (GUILayout.Button("Reset Settings"))
            {
                ResetSettings(); 
            }
            GUILayout.EndVertical();
        }

        private void ResetSettings()
        {
            if (File.Exists(jsonFilePath))
            {
                // JSON 데이터를 기본 값으로 설정
                ToggleData data = new ToggleData
                {
                    toggleReverse = false,
                    toggleMenuName = "Toggles",
                    groupToggleMenuName = "GroupToggles"
                };

                // JSON으로 변환
                string json = JsonUtility.ToJson(data, true);

                // 파일에 쓰기
                File.WriteAllText(jsonFilePath, json);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 사용자 인터페이스에도 기본값 설정
                toggleReverse = false;
                toggleMenuName = "Toggles";
                groupToggleMenuName = "GroupToggles";

                // 사용자에게 적용됐음을 알림
                EditorUtility.DisplayDialog("Reset Settings / 설정 초기화", "Settings have been reset to default values.\n설정이 기본값으로 재설정되었습니다.", "OK");
            }
            else
            {
                // 파일이 없을 때 경고 표시
                EditorUtility.DisplayDialog("Warning / 경고", "Settings file not found.\n설정 파일을 찾을 수 없습니다.", "OK");
            }
        }



        private void ApplySettings()
        {
            EditorUtility.DisplayDialog("Confirmation / 확인", "Settings have been applied.\n설정이 적용되었습니다.", "OK");
        }


        private void LoadJson()
        {
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                ToggleData data = JsonUtility.FromJson<ToggleData>(json);
                toggleReverse = data.toggleReverse;
                toggleMenuName = data.toggleMenuName;
                groupToggleMenuName = data.groupToggleMenuName;
            }
        }

        private void SaveJson()
        {
            ToggleData data = new ToggleData();
            
            data.toggleReverse = toggleReverse;
            data.toggleMenuName = toggleMenuName;
            data.groupToggleMenuName = groupToggleMenuName;
            
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
        public string groupToggleMenuName;
    }

}
#endif