#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ToggleTool.Global;
using ToggleTool.Models;
using Version = ToggleTool.Global.Version;
using ToggleTool.Utils;

namespace ToggleTool.Runtime
{
    //v1.0.71
    [CustomEditor(typeof(ToggleConfig))]
    [InitializeOnLoad]
    public class ToggleConfigEditor : Editor
    {
        private bool _toggleSaved;
        private bool _toggleReverse;
            
        private Texture2D _icon;
        private ToggleConfig _toggleConfig;
        
        static ToggleConfigEditor()
        {
            EditorApplication.update += UpdateIcons;
        }

        private static void UpdateIcons()
        {
            // 모든 ToggleConfig 인스턴스에 대해 아이콘 설정
            var toggleConfigs = Resources.FindObjectsOfTypeAll<ToggleConfig>();
            foreach (var toggleConfig in toggleConfigs)
            {
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(FilePaths.PACKAGE_RESOURCES_PATH + FilePaths.IMAGE_NAME_TOGGLE_ON);
                SetUnityObjectIcon(toggleConfig, icon);
            }
            
            // 설정 완료 후 이벤트 해제
            EditorApplication.update -= UpdateIcons;
        }

        private void OnEnable()
        {
            this._toggleConfig = (ToggleConfig)target;
            this._toggleConfig._icon = AssetDatabase.LoadAssetAtPath<Texture2D>(FilePaths.PACKAGE_RESOURCES_PATH + FilePaths.IMAGE_NAME_TOGGLE_ON);
            EditorApplication.update += SetIconImmediate;
            
            // 설정 불러오기
            LoadSettings();
        }
        
        private void OnDisable()
        {
            // EditorApplication.update 이벤트 해제
            EditorApplication.update -= SetIconImmediate;
        }

        private void SetIconImmediate()
        {
            SetUnityObjectIcon(this._toggleConfig, this._toggleConfig._icon);

            // 아이콘 설정 후 이벤트 해제
            EditorApplication.update -= SetIconImmediate;
        }

        public override void OnInspectorGUI()
        {
            // SetToggleConfig 구조체의 필드들 직접 표시
            SerializedProperty toggleConfigProp = serializedObject.FindProperty("toggleConfig");
            if (toggleConfigProp != null)
            {
                EditorGUILayout.PropertyField(toggleConfigProp.FindPropertyRelative("toggleSaved"), new GUIContent("Toggle Saved"));
                EditorGUILayout.PropertyField(toggleConfigProp.FindPropertyRelative("toggleReverse"), new GUIContent("Toggle Reverse"));
                EditorGUILayout.PropertyField(toggleConfigProp.FindPropertyRelative("toggleMenuName"), new GUIContent("Toggle Menu Name"));
            }
            else
            {
                EditorGUILayout.HelpBox("toggleConfig 필드를 찾을 수 없습니다.", MessageType.Error);
            }
                
            EditorGUILayout.Space();

            if (GUILayout.Button("Apply"))
            {
                ApplySettings();
            }
                
            if (GUILayout.Button("Reset Settings"))
            {
                ResetSettings();
            }

            serializedObject.ApplyModifiedProperties(); // 수정된 속성 적용
        }

        private static void SetUnityObjectIcon(UnityEngine.Object unityObject, Texture2D icon)
        {
            try
            {
                if (unityObject != null && icon != null)
                {
                    Type editorGUIUtilityType = typeof(EditorGUIUtility);
                    BindingFlags bindingFlags = BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic;
                    object[] args = new object[] { unityObject, icon };
                    editorGUIUtilityType.InvokeMember("SetIconForObject", bindingFlags, null, null, args);
                }
            }
            catch (MissingMethodException)
            { 
    #if UNITY_2022_3_OR_NEWER
                if (unityObject != null && icon != null)
                {
                    if (unityObject != null && icon != null)
                    {
                        EditorGUIUtility.SetIconForObject(unityObject, icon);
                    }
                }
    #endif
            }
        }

        private void ApplySettings()
        {
            var targetObject = (ToggleConfig)target;
            if (targetObject == null)
            {
                Debug.LogError("Target object is null.");
                return;
            }

            ToggleConfigModel data = new ToggleConfigModel
            {
                toggleSaved = targetObject.toggleConfig.toggleSaved,
                toggleReverse = targetObject.toggleConfig.toggleReverse,
                toggleMenuName = targetObject.toggleConfig.toggleMenuName
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePaths.JSON_FILE_PATH, json);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_CONFIRM, "Settings have been applied. They will take effect from the next toggle creation.\n\n설정이 적용되었습니다.\n다음 토글 생성부터 반영됩니다.", Messages.DIALOG_BUTTON_OK);
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

                var targetObject = (ToggleConfig)target;
                if (targetObject != null)
                {
                    targetObject.toggleConfig.toggleSaved = true;
                    targetObject.toggleConfig.toggleReverse = false;
                    targetObject.toggleConfig.toggleMenuName = Components.DEFAULT_COMPONENT_NAME;
                }

                EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_RESET_SETTING, "Settings have been reset to default values.\n설정이 기본값으로 재설정되었습니다.", Messages.DIALOG_BUTTON_OK);
            }
            else
            {
                EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_WARNING, "Settings file not found.\n설정 파일을 찾을 수 없습니다.", Messages.DIALOG_BUTTON_OK);
            }
        }

        private void LoadSettings()
        {
            if (File.Exists(FilePaths.JSON_FILE_PATH))
            {
                string json = File.ReadAllText(FilePaths.JSON_FILE_PATH);
                ToggleConfigModel data = JsonUtility.FromJson<ToggleConfigModel>(json);

                var targetObject = (ToggleConfig)target;
                if (targetObject != null)
                {
                    targetObject.toggleConfig.toggleSaved = data.toggleSaved;
                    targetObject.toggleConfig.toggleReverse = data.toggleReverse;
                    targetObject.toggleConfig.toggleMenuName = data.toggleMenuName;
                }
            }
            else
            {
                EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_WARNING, "Settings file not found.\n설정 파일을 찾을 수 없습니다.", Messages.DIALOG_BUTTON_OK);
            }
        }
    }
}
#endif