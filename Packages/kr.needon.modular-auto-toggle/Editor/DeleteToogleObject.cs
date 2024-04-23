#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

//v1.0.66
namespace Editor
{
    public class DeleteToggleObjects : EditorWindow
    {
        private VRCAvatarDescriptor _avatarDescriptor;
        private Vector2 _scrollPosToggle;
        private Vector2 _scrollPosGroupToggle;
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
            _avatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", _avatarDescriptor, typeof(VRCAvatarDescriptor), true);

            if (_avatarDescriptor != null)
            {
                LoadToggles();
                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.MaxWidth(295), GUILayout.ExpandHeight(true));
                EditorGUILayout.LabelField(ReadToggleMenuNameSetting(), EditorStyles.boldLabel);
                _scrollPosToggle = EditorGUILayout.BeginScrollView(_scrollPosToggle, GUILayout.ExpandHeight(true));
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
                _scrollPosGroupToggle = EditorGUILayout.BeginScrollView(_scrollPosGroupToggle, GUILayout.ExpandHeight(true));
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
        

        private void AllDeleteToggle()
        {
            if (EditorUtility.DisplayDialog("Delete All Confirmation",
                                            "Are you sure you want to delete all toggles, group toggles, and their animations?",
                                            "Yes",
                                            "No"))
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
                
                Transform groupTogglesParent = _avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting());
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
                                "OK"))
                        {
                            warnedNames.Add(child.name);
                        }
                    }
                }
            }

            // GroupToggle 부분에 대한 처리도 동일하게 적용합니다.
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


        private void DeleteAllFilesInFolder(string folderPath)
        {
            if (System.IO.Directory.Exists(folderPath))
            {
                string[] files = System.IO.Directory.GetFiles(folderPath, "*", System.IO.SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    if (fileName != "setting.json" && System.IO.Path.GetExtension(file) != ".meta") //setting.json 파일은 남겨두고 삭제하기
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
                        
                        Debug.Log("toggleName.Value :: " + toggleName.Key);
                        var toggleObj = _avatarDescriptor.transform.Find(ReadToggleMenuNameSetting() + "/" + toggleName.Key);
                        var rootObject = toggleObj.transform.root.gameObject;

                        if (toggleObj != null)
                        {
                            
                            ModularAvatarParameters maParams = toggleObj.GetComponent<ModularAvatarParameters>();
                            if (maParams != null)
                            {
                                
                                // 컴포넌트에서 정보를 가져오는 로직
                                var paramName = maParams.parameters; 

                                foreach (var paramConfig in maParams.parameters)
                                {
                                    
                                    var toggleGuidParamName = paramConfig.nameOrPrefix;
                                    
                                    string fullPath = FindFileByGuid(toggleGuidParamName, "Assets/Hirami/Toggle").Replace("_off.anim", "");
                                    
                                    if (!string.IsNullOrEmpty(fullPath))
                                    {
                                        Debug.Log("File found: " + fullPath);
                                        DeleteAnimationFiles(toggleName.Key, "" + rootObject.name + "_toggle_fx", "toggle", fullPath, toggleGuidParamName);
                                        DestroyImmediate(toggleObj.gameObject);
                                        DeleteToggleState(toggleName.Key);
                                        togglesRemoved = true;
                                        AssetDatabase.Refresh();
                                    }
                                    else
                                    {
                                        Debug.Log("File not found.");
                                    }
                                    
                                }

                            }

                        }
                    }
                }

                foreach (var groupToggleName in groupTogglesToDelete)
                {
                    
                    if (groupToggleName.Value)
                    {
                        var groupToggleObj = _avatarDescriptor.transform.Find(ReadGroupToggleMenuNameSetting() + "/" + groupToggleName.Key);
                        if (groupToggleObj != null)
                        {
                           
                            var rootObject = groupToggleObj.transform.root.gameObject;      
                            
                            ModularAvatarParameters maParams = groupToggleObj.GetComponent<ModularAvatarParameters>(); //group Toggle 오브젝트 모듈러 컴포넌트 정보 가져오기
                            if (maParams != null)
                            {
                                
                                var paramName = maParams.parameters;
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

                if (togglesRemoved || groupTogglesRemoved)
                {
                    LoadToggles();
                }
            }
        }

        //애니메이션 파일 삭제
        private void DeleteAnimationFiles(string toggleName, string controllerType, string type, string animePath, string hashName)
        {
            
            Debug.Log("DeleteAnimationFiles toggleName :: " + toggleName);
            Debug.Log("DeleteAnimationFiles controllerType :: " + controllerType);
            Debug.Log("DeleteAnimationFiles type :: " + type);
            Debug.Log("DeleteAnimationFiles toggleName :: " + animePath);
            
            if (type.Equals("group"))
            {
               
                    string animationFileOn = $"" + animePath + "_on.anim";
                    string animationFileOff = $"" + animePath + "_off.anim";
                    
              
                    string animatorControllerPath = $"Assets/Hirami/Toggle/{controllerType}.controller";
                    
                    
                    DeleteStatesAndParametersFromAnimator(animatorControllerPath, hashName, toggleName, "group");
                    DeleteAssetIfItExists(animationFileOn);
                    DeleteAssetIfItExists(animationFileOff);
                    
            }
            else
            {
                if (!toggleName.StartsWith("Toggle_"))
                {
                    toggleName = "Toggle_" + toggleName;
                }

                Debug.Log("Delete toggleName :: " + toggleName);

                //string pathToAnimations = $"Assets/Hirami/Toggle/";
                string animationFileOn = $"" + animePath + "_on.anim";
                string animationFileOff = $"" + animePath + "_off.anim";
                

                Debug.Log("Delete animationFileOn :: " + animationFileOn);

                string animatorControllerPath = $"Assets/Hirami/Toggle/{controllerType}.controller";
                DeleteStatesAndParametersFromAnimator(animatorControllerPath, hashName, toggleName, "toggle");
                DeleteAssetIfItExists(animationFileOn);
                DeleteAssetIfItExists(animationFileOff);

               
            }
        }


        //fx 개별로 지우는 함수
        private void DeleteStatesAndParametersFromAnimator(string controllerPath, string toggleName, string originalToggleName, string type)
        {

            Debug.Log("DeleteStatesAndParametersFromAnimator :: " + originalToggleName);
            Debug.Log("DeleteStatesAndParametersFromAnimator originalToggleName :: " + originalToggleName);
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

        
        public static string FindFileByGuid(string guid, string searchFolder)
        {
            // Assets 폴더 내에서 모든 파일의 경로를 가져옵니다.
            var allFiles = Directory.GetFiles(searchFolder, "*", SearchOption.AllDirectories);

            // 해당 GUID를 포함하는 파일을 찾습니다.
            var fileWithGuid = allFiles.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file).Contains(guid));

            // 전체 파일 이름을 반환합니다 (경로 포함).
            // 파일을 찾지 못한 경우 null을 반환합니다.
            return fileWithGuid;
        }


    }



}
#endif