#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using ToggleTool.Global;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using ToggleTool.Utils;

namespace ToggleTool.Runtime
{
    //v1.0.71
    [CustomEditor(typeof(DeleteToggle))]
    public class DeleteToggleEditor : UnityEditor.Editor
    {
        private DeleteToggle _deleteToggle;
        private VRCAvatarDescriptor _avatarDescriptor;
        private Dictionary<string, bool> togglesToDelete = new Dictionary<string, bool>();
        private List<string> warnedNames = new List<string>();
            
        private void OnEnable()
        {
                
            this._deleteToggle = (DeleteToggle)target;
            this._deleteToggle._icon = AssetDatabase.LoadAssetAtPath<Texture2D>(FilePaths.PACKAGE_RESOURCES_PATH + FilePaths.IMAGE_NAME_TOGGLE_ON);;
            SetUnityObjectIcon(this._deleteToggle, this._deleteToggle._icon);
                
            // 아바타 자동 감지
            _avatarDescriptor = ((DeleteToggle)target).GetComponentInParent<VRCAvatarDescriptor>();
            if (_avatarDescriptor == null)
            {
                Debug.LogWarning("VRCAvatarDescriptor를 찾을 수 없습니다.");
            }
        }

        private void SetUnityObjectIcon(UnityEngine.Object unityObject, Texture2D icon)
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

        public override void OnInspectorGUI()
        {
            serializedObject.Update(); // serializedObject 업데이트

            if (_avatarDescriptor != null)
            {
                LoadToggles();
                EditorGUILayout.Space(2);

                EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));

                if (togglesToDelete.Count == 0)
                {
                    EditorGUILayout.TextArea("토글 항목이 없습니다.\nNo toggle items available.", EditorStyles.label);
                }
                else
                {
                    foreach (var toggleName in new List<string>(togglesToDelete.Keys))
                    {
                        bool currentState = EditorGUILayout.ToggleLeft(toggleName, togglesToDelete[toggleName]);
                        if (currentState != togglesToDelete[toggleName])
                        {
                            togglesToDelete[toggleName] = currentState;
                            SaveToggleState(toggleName, currentState);
                        }
                    }
                }

                EditorGUILayout.EndVertical();

                if (GUILayout.Button("Delete Toggle"))
                {
                    DeleteToggle();
                }
                    
                if (GUILayout.Button("All Delete Toggle"))
                {
                    AllDeleteToggle();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("VRCAvatarDescriptor를 찾을 수 없습니다. 아바타가 선택되어 있는지 확인하세요.\nCould not find VRCAvatarDescriptor. Please ensure an avatar is selected.", MessageType.Warning);
            }
        }
            
        private void AllDeleteToggle()
        {
            if (EditorUtility.DisplayDialog("Delete All Confirmation",
                    "Are you sure you want to delete all toggles, group toggles, and their animations?",
                    Messages.DIALOG_BUTTON_YES,
                    Messages.DIALOG_BUTTON_NO))
            {
                    
                Transform togglesParent = _avatarDescriptor.transform.Find(ReadToggleMenuNameSetting());
                if (togglesParent != null)
                {
                    foreach (Transform child in togglesParent)
                    {
                        DeleteToggleState(child.name);
                    }
                    DestroyImmediate(togglesParent.gameObject);
                }
                    
                DeleteAllFilesInFolder(FilePaths.TARGET_FOLDER_PATH + "/" + _avatarDescriptor.transform.name);

                togglesToDelete.Clear();
            }
        }

        private void DeleteAllFilesInFolder(string folderPath)
        {
            if (System.IO.Directory.Exists(folderPath))
            {
                string[] files = System.IO.Directory.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    // setting.json 파일도 아니며, .meta 확장자도 아닌 파일만 삭제
                    if (fileName != "setting.json" && System.IO.Path.GetExtension(file) != ".meta")
                    {
                        AssetDatabase.DeleteAsset(file);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogWarning("Folder path does not exist: " + folderPath);
            }
        }



        private void LoadToggles()
        {
            togglesToDelete.Clear();

            Transform togglesParent = _avatarDescriptor.transform.Find(ReadToggleMenuNameSetting());
            if (togglesParent != null)
            {
                foreach (Transform child in togglesParent)
                {
                    if (!togglesToDelete.ContainsKey(child.name))
                    {
                        togglesToDelete.Add(child.name, LoadToggleState(child.name));
                    }
                    else if (!warnedNames.Contains(child.name))
                    {
                        if (EditorUtility.DisplayDialog("Duplicate Name Warning / 중복된 이름 경고",
                                "A duplicate name was found: " + child.name + ".\nPlease change to a different name.\n\n중복된 이름이 발견되었습니다: " + child.name + ".\n다른 이름으로 변경해주세요.",
                                Messages.DIALOG_BUTTON_OK))
                        {
                            warnedNames.Add(child.name);
                        }
                    }
                }
            }
        }

        private void SaveToggleState(string key, bool state)
        {
            EditorPrefs.SetBool("DeleteToggleObjects_" + key, state);
        }

        private bool LoadToggleState(string key)
        {
            return EditorPrefs.GetBool("DeleteToggleObjects_" + key, false);
        }

        private void DeleteToggle()
        {
            bool togglesRemoved = false;

            if (EditorUtility.DisplayDialog("Delete Confirmation",
                    "Are you sure you want to delete the selected toggles and their animations?",
                    Messages.DIALOG_BUTTON_YES,
                    Messages.DIALOG_BUTTON_NO))
            {
                foreach (var toggleName in togglesToDelete)
                {
                    if (toggleName.Value)
                    {
                        var toggleObj = _avatarDescriptor.transform.Find(ReadToggleMenuNameSetting() + "/" + toggleName.Key);
                        var rootObject = toggleObj.transform.root.gameObject;

                        if (toggleObj != null)
                        {
                            ModularAvatarParameters maParams = toggleObj.GetComponent<ModularAvatarParameters>();
                            if (maParams != null)
                            {
                                foreach (var paramConfig in maParams.parameters)
                                {
                                    var toggleGuidParamName = paramConfig.nameOrPrefix;
                                    string fullPath = FindFileByGuid(toggleGuidParamName, FilePaths.TARGET_FOLDER_PATH + "/" + rootObject.name).Replace("_off.anim", "");

                                    if (!string.IsNullOrEmpty(fullPath))
                                    {
                                        DeleteAnimationFiles(toggleName.Key, "/" + rootObject.name + "/toggle_fx", fullPath, toggleGuidParamName);
                                        DestroyImmediate(toggleObj.gameObject);
                                        DeleteToggleState(toggleName.Key);
                                        togglesRemoved = true;
                                        AssetDatabase.Refresh();
                                    }
                                }
                            }
                        }
                    }
                }

                if (togglesRemoved)
                {
                    LoadToggles();
                }
            }
        }

        private void DeleteAnimationFiles(string toggleName, string controllerType, string animePath, string hashName)
        {
            string animationFileOn = $"" + animePath + "_on.anim";
            string animationFileOff = $"" + animePath + "_off.anim";
            string animatorControllerPath = FilePaths.TARGET_FOLDER_PATH + $"/{controllerType}.controller";
            DeleteStatesAndParametersFromAnimator(animatorControllerPath, hashName, toggleName, "toggle");
            DeleteAssetIfItExists(animationFileOn);
            DeleteAssetIfItExists(animationFileOff);
        }

        private void DeleteStatesAndParametersFromAnimator(string controllerPath, string toggleName, string originalToggleName, string type)
        {
            AnimatorController animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (animatorController != null)
            {
                for (int i = 0; i < animatorController.layers.Length; i++)
                {
                    AnimatorControllerLayer layer = animatorController.layers[i];
                    if (layer.name == toggleName)
                    {
                        animatorController.RemoveLayer(i);
                        break;
                    }
                }

                for (int i = 0; i < animatorController.parameters.Length; i++)
                {
                    AnimatorControllerParameter param = animatorController.parameters[i];
                    if (param.name == toggleName)
                    {
                        animatorController.RemoveParameter(param);
                        break;
                    }
                }

                EditorUtility.SetDirty(animatorController);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogError("Animator Controller not found at path: " + controllerPath);
            }
        }

        private void DeleteAssetIfItExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        private void DeleteToggleState(string key)
        {
            EditorPrefs.DeleteKey("DeleteToggleObjects_" + key);
        }

        private static string ReadToggleMenuNameSetting()
        {
            if (File.Exists(FilePaths.JSON_FILE_PATH))
            {
                string json = File.ReadAllText(FilePaths.JSON_FILE_PATH);
                var settings = JsonUtility.FromJson<ToggleSettings>(json);
                return settings.toggleMenuName;
            }

            return "";
        }

        public static string FindFileByGuid(string guid, string searchFolder)
        {
            var allFiles = Directory.GetFiles(searchFolder, "*", SearchOption.AllDirectories);
            var fileWithGuid = allFiles.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file).Contains(guid));
            return fileWithGuid;
        }

        [System.Serializable]
        public class ToggleSettings
        {
            public string toggleMenuName;
        }
    }
}
#endif