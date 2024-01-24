#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static Editor.GroupTogglePrefabCreatorGroups;

//v1.0.7
namespace Editor
{
    public class DeleteToggleObjects : EditorWindow
    {
        private VRCAvatarDescriptor avatarDescriptor;
        private Vector2 scrollPosToggle;
        private Vector2 scrollPosGroupToggle;
        private Dictionary<string, bool> togglesToDelete = new Dictionary<string, bool>();
        private Dictionary<string, bool> groupTogglesToDelete = new Dictionary<string, bool>();
        private List<string> warnedNames = new List<string>(); 

        [MenuItem("Hirami/Auto Toggle/Delete Toggle Objects", false, 0)]
        private static void Init()
        {
            var window = GetWindowWithRect<DeleteToggleObjects>(new Rect(0, 0, 600, 400), false, "Delete Toggle Objects");

            // 초기화 로직 추가
            window.togglesToDelete.Clear();
            window.groupTogglesToDelete.Clear();
            window.warnedNames.Clear();

            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.window);
            avatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", avatarDescriptor, typeof(VRCAvatarDescriptor), true);

            if (avatarDescriptor != null)
            {
                LoadToggles();
                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.MaxWidth(295), GUILayout.ExpandHeight(true));
                EditorGUILayout.LabelField(ReadToggleMenuNameSetting(), EditorStyles.boldLabel);
                scrollPosToggle = EditorGUILayout.BeginScrollView(scrollPosToggle, GUILayout.ExpandHeight(true));
                foreach (var toggleName in new List<string>(togglesToDelete.Keys))
                {
                    bool currentState = EditorGUILayout.ToggleLeft(toggleName, togglesToDelete[toggleName]);
                    if (currentState != togglesToDelete[toggleName])
                    {
                        togglesToDelete[toggleName] = currentState;
                        SaveToggleState(toggleName, currentState);
                    }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.MaxWidth(295), GUILayout.ExpandHeight(true));
                EditorGUILayout.LabelField(ReadGroupToggleMenuNameSetting(), EditorStyles.boldLabel);
                scrollPosGroupToggle = EditorGUILayout.BeginScrollView(scrollPosGroupToggle, GUILayout.ExpandHeight(true));
                foreach (var groupToggleName in new List<string>(groupTogglesToDelete.Keys))
                {EditorGUILayout.BeginHorizontal();
                    bool currentState = EditorGUILayout.ToggleLeft(groupToggleName, groupTogglesToDelete[groupToggleName]);
                    if (currentState != groupTogglesToDelete[groupToggleName])
                    {
                        groupTogglesToDelete[groupToggleName] = currentState;
                        SaveToggleState(groupToggleName, currentState);
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Delete Toggle"))
                {
                    DeleteToggle();
                }

                if (GUILayout.Button("All Delete Toggle"))
                {
                    AllDeleteToggle();
                }
            }

            EditorGUILayout.EndVertical();
            Repaint();
        }

        private void TestBtn(object toggleName)
        {
            throw new NotImplementedException();
        }

        private void AllDeleteToggle()
        {
            if (EditorUtility.DisplayDialog("Delete All Confirmation",
                                            "Are you sure you want to delete all toggles, group toggles, and their animations?",
                                            "Yes",
                                            "No"))
            {
                
                Transform togglesParent = avatarDescriptor.transform.Find(ReadToggleMenuNameSetting());
                if (togglesParent != null)
                {
                    foreach (Transform child in togglesParent)
                    {
                        DeleteToggleState(child.name);
                    }
                    DestroyImmediate(togglesParent.gameObject);
                }
                
                Transform groupTogglesParent = avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting());
                if (groupTogglesParent != null)
                {
                    foreach (Transform child in groupTogglesParent)
                    {
                        DeleteToggleState(child.name);
                    }
                    DestroyImmediate(groupTogglesParent.gameObject);
                }
                
                DeleteAllFilesInFolder("Assets/Hirami/Toggle");

                togglesToDelete.Clear();
                groupTogglesToDelete.Clear();
            }
        }


        private void LoadToggles()
        {
            togglesToDelete.Clear();
            groupTogglesToDelete.Clear();

            Transform togglesParent = avatarDescriptor.transform.Find(ReadToggleMenuNameSetting());
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
                                "OK"))
                        {
                            warnedNames.Add(child.name);
                        }
                    }
                }
            }

            // GroupToggle 부분에 대한 처리도 동일하게 적용합니다.
            Transform groupTogglesParent = avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting());
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


        private void DeleteAllFilesInFolder(string folderPath)
        {
            if (System.IO.Directory.Exists(folderPath))
            {
                string[] files = System.IO.Directory.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
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


        private void SaveToggleState(string key, bool state)
        {
            EditorPrefs.SetBool("DeleteToggleObjects_" + key, state);
        }

        private bool LoadToggleState(string key)
        {
            return EditorPrefs.GetBool("DeleteToggleObjects_" + key, false);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void DeleteToggle()
        {
            bool togglesRemoved = false;
            bool groupTogglesRemoved = false;

            if (EditorUtility.DisplayDialog("Delete Confirmation",
                                            "Are you sure you want to delete the selected toggles and their animations?",
                                            "Yes",
                                            "No"))
            {
                foreach (var toggleName in togglesToDelete)
                {
                    if (toggleName.Value)
                    {
                        var toggleObj = avatarDescriptor.transform.Find(ReadToggleMenuNameSetting() + "/" + toggleName.Key);
                        if (toggleObj != null)
                        {
                            if (!toggleObj.name.StartsWith("Toggle_"))
                            {
                                toggleObj.name = "Toggle_" + toggleObj.name;
                            }
                            Debug.Log("toggleObj.gameObject.name :: " + toggleObj.gameObject.name);
                            GetHashedNameFromOriginalName(toggleObj.gameObject.name);
                            DestroyImmediate(toggleObj.gameObject);
                            togglesRemoved = true;
                            DeleteAnimationFiles(toggleName.Key, "toggle_fx", "toggle");
                            DeleteToggleState(toggleName.Key);
                            AssetDatabase.Refresh();
                        }
                    }
                }

                foreach (var groupToggleName in groupTogglesToDelete)
                {
                    if (groupToggleName.Value)
                    {
                        var groupToggleObj = avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting() + "/" + groupToggleName.Key);
                        if (groupToggleObj != null)
                        {
                            if (!groupToggleObj.name.StartsWith("Group_"))
                            {
                                groupToggleObj.name = "Group_" + groupToggleObj.name;
                            }
                            DestroyImmediate(groupToggleObj.gameObject);
                            groupTogglesRemoved = true;
                            DeleteAnimationFiles(groupToggleName.Key, "group_toggle_fx", "Group");
                            AssetDatabase.Refresh();
                        }
                    }
                }

                if (togglesRemoved || groupTogglesRemoved)
                {
                    LoadToggles();
                }
            }
        }

        private void DeleteAnimationFiles(string toggleName, string controllerType, string type)
        {
            if (type.Equals("Group"))
            {

                if (!toggleName.StartsWith("Group_"))
                {
                    toggleName = "Group_" + toggleName;
                }
                NameHashMapping nameHashMapping = ReadHashMappingFromJson();

                string hashedToggleName = nameHashMapping.mappings
                    .FirstOrDefault(pair => pair.originalName.Equals(toggleName, StringComparison.OrdinalIgnoreCase))?.hashedName;

                if (!string.IsNullOrEmpty(hashedToggleName))
                {
                    string pathToAnimations = $"Assets/Hirami/Toggle/";
                    string animationFileOn = $"Group_{hashedToggleName}_on.anim";
                    string animationFileOff = $"Group_{hashedToggleName}_off.anim";
                    string animatorControllerPath = $"Assets/Hirami/Toggle/{controllerType}.controller";

                    DeleteAssetIfItExists(pathToAnimations + animationFileOn);
                    DeleteAssetIfItExists(pathToAnimations + animationFileOff);

                    DeleteStatesAndParametersFromAnimator(animatorControllerPath, GetHashedNameFromOriginalName(toggleName));
                    RemoveMappingFromJson(toggleName);

                }
                else
                {
                    Debug.LogWarning("Hashed name for toggle '" + toggleName + "' not found in JSON.");
                }
            }
            else
            {
                if (!toggleName.StartsWith("Toggle_"))
                {
                    toggleName = "Toggle_" + toggleName;
                }

                NameHashMapping nameHashMapping = ReadHashMappingFromJson();

                string hashedToggleName = nameHashMapping.mappings
                    .FirstOrDefault(pair => pair.originalName.Equals(toggleName, StringComparison.OrdinalIgnoreCase))?.hashedName;

   

                if (!string.IsNullOrEmpty(hashedToggleName))
                {
                    string pathToAnimations = $"Assets/Hirami/Toggle/";
                    string animationFileOn = $"Toggle_{hashedToggleName}_on.anim";
                    string animationFileOff = $"Toggle_{hashedToggleName}_off.anim";
                    string animatorControllerPath = $"Assets/Hirami/Toggle/{controllerType}.controller";

                    DeleteAssetIfItExists(pathToAnimations + animationFileOn);
                    DeleteAssetIfItExists(pathToAnimations + animationFileOff);

                    DeleteStatesAndParametersFromAnimator(animatorControllerPath, GetHashedNameFromOriginalName(toggleName));
                    RemoveMappingFromJson(toggleName);

                }
                else
                {
                    Debug.LogWarning("Hashed name for toggle '" + toggleName + "' not found in JSON.");
                }
            }
        }


        private void DeleteStatesAndParametersFromAnimator(string controllerPath, string toggleName)
        {

            Debug.Log("DeleteStatesAndParametersFromAnimator :: " + toggleName);
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

        private NameHashMapping ReadHashMappingFromJson()
        {
            string jsonFilePath = "Assets/Hirami/Toggle/NameHashMappings.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                NameHashMapping nameHashMapping = JsonUtility.FromJson<NameHashMapping>(json);

                foreach (var mapping in nameHashMapping.mappings)
                {
                    Debug.Log($"Original: {mapping.originalName}, Hashed: {mapping.hashedName}");
                }

                return nameHashMapping;
            }
            return new NameHashMapping { mappings = new List<NameHashPair>() };
        }

        private void RemoveMappingFromJson(string toggleName)
        {

            string jsonFilePath = "Assets/Hirami/Toggle/NameHashMappings.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                NameHashMapping nameHashMapping = JsonUtility.FromJson<NameHashMapping>(json);

                nameHashMapping.mappings.RemoveAll(mapping => mapping.originalName == toggleName);

                // prettyPrint를 true로 설정하여 JSON을 정돈된 형식으로 저장
                string updatedJson = JsonUtility.ToJson(nameHashMapping, true);
                File.WriteAllText(jsonFilePath, updatedJson);
                AssetDatabase.Refresh();

                foreach (var mapping in nameHashMapping.mappings)
                {
                    Debug.Log($"Original: {mapping.originalName}, Hashed: {mapping.hashedName}");
                }
            }
            else
            {
                Debug.LogWarning("JSON file not found: " + jsonFilePath);
            }
        }
        
        private static string ReadToggleMenuNameSetting()
        {
            string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                var settings = JsonUtility.FromJson<ToggleSettings>(json);
                return settings.toggleMenuName;
            }

            return "";
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

        private static string GetHashedNameFromOriginalName(string originalName)
        {
            string jsonFilePath = "Assets/Hirami/Toggle/NameHashMappings.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                NameHashMapping nameHashMapping = JsonUtility.FromJson<NameHashMapping>(json);

                foreach (var mapping in nameHashMapping.mappings)
                {
                    if (mapping.originalName.Equals(originalName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log("hashedName :: " + mapping.hashedName);
                        return mapping.hashedName;
                    }
                }
            }
            else
            {
                Debug.LogWarning("JSON file not found: " + jsonFilePath);
            }

            return null;
        }


    }



}
#endif