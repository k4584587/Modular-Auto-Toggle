#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using ToggleTool.Global;
using ToggleTool.Models;

//v1.0.68
namespace ToggleTool.Editor
{
    public class ToggleSetting : EditorWindow
    {
        private bool _toggleSaved;
        private bool _toggleReverse;
        private string _toggleMenuName;

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

            _toggleMenuName = EditorGUILayout.TextField("Toggle Menu Name", Components.DEFAULT_COMPONENT_NAME);
            GUILayout.Space(10);

            _toggleSaved = EditorGUILayout.Toggle("저장됨 (Toggle Saved) ", _toggleSaved);
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
            if (File.Exists(FilePaths.JSON_FILE_PATH))
            {
                ToggleConfigModel data = new ToggleConfigModel
                {
                    toggleSaved = true,
                    toggleReverse = false,
                    toggleMenuName = Components.DEFAULT_COMPONENT_NAME
                };

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePaths.JSON_FILE_PATH, json);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                _toggleSaved = true;
                _toggleReverse = false;
                _toggleMenuName = Components.DEFAULT_COMPONENT_NAME;

                EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_RESET_SETTING, "Settings have been reset to default values.\n설정이 기본값으로 재설정되었습니다.", Messages.DIALOG_BUTTON_OK);
            }
            else 
            {
                EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_WARNING, "Settings file not found.\n설정 파일을 찾을 수 없습니다.", Messages.DIALOG_BUTTON_OK);
            }
        }

        private void ApplySettings()
        {
            EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_CONFIRM, "Settings have been applied.\n설정이 적용되었습니다.", Messages.DIALOG_BUTTON_OK);
        }
 
        private void LoadJson()
        {
            if (File.Exists(FilePaths.JSON_FILE_PATH))
            {
                string json = File.ReadAllText(FilePaths.JSON_FILE_PATH);
                ToggleConfigModel data = JsonUtility.FromJson<ToggleConfigModel>(json);
                _toggleSaved = data.toggleSaved;
                _toggleReverse = data.toggleReverse;
                _toggleMenuName = data.toggleMenuName;
            }
        }

        private void SaveJson()
        {
            ToggleConfigModel data = new ToggleConfigModel
            {
                toggleSaved = _toggleSaved,
                toggleReverse = _toggleReverse,
                toggleMenuName = _toggleMenuName
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePaths.JSON_FILE_PATH, json);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
