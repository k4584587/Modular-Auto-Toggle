#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

//v1.0.68
[CustomEditor(typeof(GroupDeleteToggle))]
[InitializeOnLoad]
public class GroupDeleteToggleEditor : Editor
{
    private VRCAvatarDescriptor _avatarDescriptor;
    private Dictionary<string, bool> groupTogglesToDelete = new Dictionary<string, bool>();
    private List<string> warnedNames = new List<string>();

    private GroupDeleteToggle _groupDeleteToggle;
    
    private const string iconFilePath = "Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png";

    static GroupDeleteToggleEditor()
    {
        EditorApplication.update += UpdateIcons;
    }

    private static void UpdateIcons()
    {
        // 모든 ToggleConfig 인스턴스에 대해 아이콘 설정
        var toggleConfigs = Resources.FindObjectsOfTypeAll<ToggleConfig>();
        foreach (var toggleConfig in toggleConfigs)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconFilePath);
            SetUnityObjectIcon(toggleConfig, icon);
        }
        
        // 설정 완료 후 이벤트 해제
        EditorApplication.update -= UpdateIcons;
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

    private void OnEnable()
    {
        
        this._groupDeleteToggle = (GroupDeleteToggle)target;
        this._groupDeleteToggle._icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconFilePath);
        EditorApplication.update += SetIconImmediate;
        
        // 아바타 자동 감지
        var groupDeleteToggle = target as GroupDeleteToggle;
        if (groupDeleteToggle != null)
        {
            _avatarDescriptor = groupDeleteToggle.GetComponentInParent<VRCAvatarDescriptor>();
            if (_avatarDescriptor == null)
            {
                Debug.LogWarning("VRCAvatarDescriptor를 찾을 수 없습니다.");
            }
        }
    }

    private void SetIconImmediate()
    {
        SetUnityObjectIcon(this._groupDeleteToggle, this._groupDeleteToggle._icon);

        // 아이콘 설정 후 이벤트 해제
        EditorApplication.update -= SetIconImmediate;
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update(); // serializedObject 업데이트

        if (_avatarDescriptor != null)
        {
            LoadGroupToggles();
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));

            if (groupTogglesToDelete.Count == 0)
            {
                EditorGUILayout.TextArea("그룹 토글 항목이 없습니다.\nNo group toggle items available.", EditorStyles.label);
            }
            else
            {
                foreach (var groupToggleName in new List<string>(groupTogglesToDelete.Keys))
                {
                    bool currentState = EditorGUILayout.ToggleLeft(groupToggleName, groupTogglesToDelete[groupToggleName]);
                    if (currentState != groupTogglesToDelete[groupToggleName])
                    {
                        groupTogglesToDelete[groupToggleName] = currentState;
                        SaveToggleState(groupToggleName, currentState);
                    }
                }
            }

            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Delete Group Toggle"))
            {
                DeleteGroupToggle();
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
                "Yes",
                "No"))
        {
            Transform togglesParent = _avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting());
            if (togglesParent != null)
            {
                foreach (Transform child in togglesParent)
                {
                    DeleteToggleState(child.name);
                }
                DestroyImmediate(togglesParent.gameObject);
            }
                
            DeleteAllFilesInFolder("Assets/Hirami/Toggle");

            groupTogglesToDelete.Clear();
        }
    }

    private void DeleteAllFilesInFolder(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                // "Group" 문구가 포함되지 않고, setting.json 파일도 아니며, .meta 확장자도 아닌 파일만 삭제
                if ((fileName.Contains("Group") || fileName.Contains("group")) && fileName != "setting.json" && Path.GetExtension(file) != ".meta")
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
        
    private void LoadGroupToggles()
    {
        groupTogglesToDelete.Clear();

        Transform groupTogglesParent = _avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting());
        if (groupTogglesParent != null)
        {
            foreach (Transform child in groupTogglesParent)
            {
                if (!groupTogglesToDelete.ContainsKey(child.name))
                {
                    groupTogglesToDelete.Add(child.name, LoadToggleState(child.name));
                }
                else if (!warnedNames.Contains(child.name))
                {
                    if (EditorUtility.DisplayDialog("Duplicate Name Warning / 중복된 이름 경고",
                            "A duplicate name was found: " + child.name + ".\nPlease change to a different name.\n\n중복된 이름이 발견되었습니다: " + child.name + ".\n다른 이름으로 변경해주세요.",
                            "OK"))
                    {
                        warnedNames.Add(child.name);
                    }
                }
            }
        }
    }

    private void SaveToggleState(string key, bool state)
    {
        EditorPrefs.SetBool("GroupDeleteToggleObjects_" + key, state);
    }

    private bool LoadToggleState(string key)
    {
        return EditorPrefs.GetBool("GroupDeleteToggleObjects_" + key, false);
    }

    private void DeleteGroupToggle()
    {
        bool groupTogglesRemoved = false;

        if (EditorUtility.DisplayDialog("Delete Confirmation",
                "Are you sure you want to delete the selected group toggles and their animations?",
                "Yes",
                "No"))
        {
            foreach (var groupToggleName in groupTogglesToDelete)
            {
                if (groupToggleName.Value)
                {
                    var groupToggleObj = _avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting() + "/" + groupToggleName.Key);
                    if (groupToggleObj != null)
                    {
                        var rootObject = groupToggleObj.transform.root.gameObject;
                        ModularAvatarParameters maParams = groupToggleObj.GetComponent<ModularAvatarParameters>();
                        if (maParams != null)
                        {
                            foreach (var paramConfig in maParams.parameters)
                            {
                                var toggleGuidParamName = paramConfig.nameOrPrefix;
                                string fullPath = FindFileByGuid(toggleGuidParamName, "Assets/Hirami/Toggle").Replace("_off.anim", "");
                                DeleteAnimationFiles(groupToggleName.Key, "" + rootObject.name + "_group_toggle_fx", "group", fullPath, toggleGuidParamName);
                                DestroyImmediate(groupToggleObj.gameObject);
                                groupTogglesRemoved = true;
                                AssetDatabase.Refresh();
                            }
                        }
                    }
                }
            }

            if (groupTogglesRemoved)
            {
                LoadGroupToggles();
            }
        }
    }

    private void DeleteAnimationFiles(string toggleName, string controllerType, string type, string animePath, string hashName)
    {
        string animationFileOn = animePath + "_on.anim";
        string animationFileOff = animePath + "_off.anim";
        string animatorControllerPath = $"Assets/Hirami/Toggle/{controllerType}.controller";

        DeleteStatesAndParametersFromAnimator(animatorControllerPath, hashName, toggleName, type);
        DeleteAssetIfItExists(animationFileOn);
        DeleteAssetIfItExists(animationFileOff);
    }

    private void DeleteStatesAndParametersFromAnimator(string controllerPath, string hashName, string toggleName, string type)
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
        EditorPrefs.DeleteKey("GroupDeleteToggleObjects_" + key);
    }

    private static string ReadGroupToggleMenuNameSetting()
    {
        string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
        if (File.Exists(jsonFilePath))
        {
            string json = File.ReadAllText(jsonFilePath);
            var settings = JsonUtility.FromJson<ToggleSettings>(json);
            return settings.groupToggleMenuName;
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
        public string groupToggleMenuName;
    }
}
#endif