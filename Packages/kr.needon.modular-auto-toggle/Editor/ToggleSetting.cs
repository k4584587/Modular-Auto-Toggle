#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

//v1.0.68
namespace Editor
{
    public class ToggleSetting : EditorWindow
    {
        private bool _toggleReverse;
        private string _toggleMenuName = "Toggles"; 
        private const string jsonFilePath = "Assets/Hirami/Toggle/setting.json";

        [MenuItem("Hirami/Auto Toggle/Toggle Setting", false, 0)]
        private static void Init()
        {
            var window = GetWindowWithRect<ToggleSetting>(new Rect(0, 0, 600, 400), false, "Toggle Setting");
            window.Show();
            window.LoadJson();
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Space(10);

            _toggleMenuName = EditorGUILayout.TextField("Toggle Menu Name", _toggleMenuName);
            GUILayout.Space(10);

            _toggleReverse = EditorGUILayout.Toggle("토글 반전 (Toggle Reverse) ", _toggleReverse);
            GUILayout.Space(10);

            if (GUILayout.Button("Apply"))
            {
                ApplySettings();
                SaveJson();
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
                ToggleData data = new ToggleData
                {
                    toggleReverse = false,
                    toggleMenuName = "Toggles"
                };

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(jsonFilePath, json);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                _toggleReverse = false;
                _toggleMenuName = "Toggles";

                EditorUtility.DisplayDialog("Reset Settings / 설정 초기화", "Settings have been reset to default values.\n설정이 기본값으로 재설정되었습니다.", "OK");
            }
            else 
            {
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
                _toggleReverse = data.toggleReverse;
                _toggleMenuName = data.toggleMenuName;
            }
        }

        private void SaveJson()
        {
            ToggleData data = new ToggleData
            {
                toggleReverse = _toggleReverse,
                toggleMenuName = _toggleMenuName
            };

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
    }
}
#endif
