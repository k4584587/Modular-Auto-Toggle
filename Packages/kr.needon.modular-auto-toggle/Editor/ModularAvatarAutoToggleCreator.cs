#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using kr.needon.modular_auto_toggle.runtime.ToggleNameChange;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using static Editor.GroupTogglePrefabCreatorGroups;

//v1.0.64
namespace Editor
{
    public abstract class TogglePrefabCreator
    {
        private static AnimatorController _globalFxAnimator;
        private static string _selectGameObejct;
        private static readonly string TogglePrefabName = ReadToggleMenuNameSetting();
        private static readonly string JsonFilePath = "Assets/Hirami/Toggle/NameHashMappings.json";

        [MenuItem("GameObject/Add Toggle Items", false, 0)]
        private static void CreateToggleItemsInHierarchy()
        {
            
            Debug.Log("ToggleName :: " + TogglePrefabName);

            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogError("The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 GameObject들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.");
                EditorUtility.DisplayDialog("Error", "The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n\n선택된 GameObject들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.", "OK");
                return;
            }

            EditorApplication.delayCall = null;
            EditorApplication.delayCall += () =>
            {
                float totalItems = Selection.gameObjects.Length;
                for (var i = 0; i < totalItems; i++)
                {
                    var selectedPrefab = Selection.gameObjects[i];

                    var progress = i / totalItems;
                    EditorUtility.DisplayProgressBar("Creating Toggle Items", $"Processing {selectedPrefab.name}...",
                        progress);

                    CreateToggleItemForGameObject(selectedPrefab);
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Toggle Items Creation", "All toggle items have been created successfully.\n\n모든 토글 아이템이 성공적으로 생성되었습니다.", "OK");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        private static void CreateToggleItemForGameObject(GameObject selectedPrefab)
        {
            if (selectedPrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "No GameObject selected. Please select a GameObject.", "OK");
                return;
            }
       
            if (selectedPrefab.transform.parent == null)
            {
                EditorUtility.DisplayDialog("Error", "The selected GameObject has no parent.", "OK");
                return;
            }


            var parentPath = GetPathForGameObject(selectedPrefab.transform.parent.gameObject);
            Debug.Log(_selectGameObejct = string.IsNullOrEmpty(parentPath)
                ? selectedPrefab.name
                : parentPath + "/" + selectedPrefab.name);
            var rootGameObject = selectedPrefab.transform.root.gameObject;

            var togglesTransform = rootGameObject.transform.Find(string.IsNullOrEmpty(ReadToggleMenuNameSetting()) ? "Toggles" : ReadToggleMenuNameSetting());
            var togglesGameObject = togglesTransform != null ? togglesTransform.gameObject : null;

            if (togglesGameObject == null)
            {
                togglesGameObject = new GameObject(string.IsNullOrEmpty(ReadToggleMenuNameSetting()) ? "Toggles" : ReadToggleMenuNameSetting());
                togglesGameObject.transform.SetParent(rootGameObject.transform, false);
            }

            var toggleName = "Toggle_" + selectedPrefab.name;
            var newToggleGameObject = new GameObject(toggleName);
            newToggleGameObject.transform.SetParent(togglesGameObject.transform, false);

            AddComponentsToToggleItem(newToggleGameObject, selectedPrefab.name);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        private static void WriteHashMappingToJson(string originalName, string hashedName)
        {
            // 기존 데이터 로드 또는 새로운 구조 생성
            NameHashMapping nameHashMapping;
            if (File.Exists(JsonFilePath))
            {
                string json = File.ReadAllText(JsonFilePath);
                nameHashMapping = JsonUtility.FromJson<NameHashMapping>(json);
                if (nameHashMapping == null) // 추가된 검사
                {
                    nameHashMapping = new NameHashMapping();
                }
            }
            else
            {
                nameHashMapping = new NameHashMapping(); // 새 구조 생성
            }

            // 중복 검사 및 새 기록 추가
            if (!nameHashMapping.mappings.Any(pair => pair.originalName == originalName && pair.hashedName == hashedName))
            {
                NameHashPair newPair = new NameHashPair { originalName = originalName, hashedName = hashedName };
                nameHashMapping.mappings.Add(newPair);

                // JSON 파일 쓰기
                string newJson = JsonUtility.ToJson(nameHashMapping, true);
                File.WriteAllText(JsonFilePath, newJson);
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.Log($"Entry for '{originalName}' with hash '{hashedName}' already exists and will not be duplicated.");
            }
        }
        
        private static string Md5Hash(string input)
        { 
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }

            return sb.ToString();
        }

        private static void AddComponentsToToggleItem(GameObject obj, string prefabName)
        {
            
            var togglesGameObject = GameObject.Find(string.IsNullOrEmpty(ReadToggleMenuNameSetting()) ? "Toggles" : ReadToggleMenuNameSetting()) ?? new GameObject(string.IsNullOrEmpty(ReadToggleMenuNameSetting()) ? "Toggles" : ReadToggleMenuNameSetting());
            if (togglesGameObject.transform.parent == null)
            {
                togglesGameObject.transform.SetParent(obj.transform.parent);
            }

            var mergeAnimator = togglesGameObject.GetComponent<ModularAvatarMergeAnimator>();
            if (mergeAnimator == null)
            {
                togglesGameObject.AddComponent<ModularAvatarMenuInstaller>();
                ToggleConfigureSubMenuItem(togglesGameObject);
                mergeAnimator = togglesGameObject.AddComponent<ModularAvatarMergeAnimator>();
            }
            
            RecordState(Md5Hash(prefabName), prefabName, true);
            RecordState(Md5Hash(prefabName), prefabName, false);


            var controllerPath = $"Assets/Hirami/Toggle/toggle_fx.controller";
            
            if (_globalFxAnimator == null)
            {
                var loadedAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (loadedAnimator != null)
                {
                    UpdateToggleAnimatorController(loadedAnimator, prefabName);
                    _globalFxAnimator = loadedAnimator;
                }
                else
                {
                    _globalFxAnimator = CreateToggleAnimatorController(prefabName);
                }

                mergeAnimator.animator = _globalFxAnimator;
            }
            else
            {
                UpdateToggleAnimatorController(_globalFxAnimator, prefabName);
            }
            
            mergeAnimator.animator = _globalFxAnimator;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;

            obj.AddComponent<ToggleNameChange>();


            ConfigureAvatarParameters(obj, Md5Hash(prefabName));
            ConfigureMenuItem(obj, Md5Hash(prefabName));
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
        }
        
        private static void ConfigureAvatarParameters(GameObject obj, string parameterName)
        {
            var avatarParameters = obj.AddComponent<ModularAvatarParameters>();
            var parameterExists = avatarParameters.parameters.Any(p => p.nameOrPrefix == parameterName);

            if (parameterExists) return;
            var newParameter = new ParameterConfig
            {
                nameOrPrefix =  parameterName,
                syncType = ParameterSyncType.Bool,
                defaultValue = 1,
                saved = true
            };
            avatarParameters.parameters.Add(newParameter);
        }

        private static void ToggleConfigureSubMenuItem(GameObject obj)
        {
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.Children;
        }

        private static void ConfigureMenuItem(GameObject obj, string parameterName)
        {
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName };
        }

        private static AnimatorController CreateToggleAnimatorController(string originalName) {
        

            string onToggleAnimePath = $"Assets/Hirami/Toggle/Toggle_" + Md5Hash(originalName) + "_on.anim";
            string offToggleAnimePath = $"Assets/Hirami/Toggle/Toggle_" + Md5Hash(originalName) + "_off.anim";

            Debug.Log("onToggleAnimePath :: " + onToggleAnimePath);
            Debug.Log("offToggleAnimePath :: " + offToggleAnimePath);

            string controllerPath = $"Assets/Hirami/Toggle/toggle_fx.controller";
            AnimatorController animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = Md5Hash(originalName),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animatorController);
            animatorController.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    name = Md5Hash(originalName),
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                }
            };

            AnimatorControllerParameter parameter = new AnimatorControllerParameter
            {
                name = Md5Hash(originalName),
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            };
            animatorController.AddParameter(parameter);

            AnimationClip offClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(offToggleAnimePath);
            AnimationClip onClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath);

            // toggleReverse 설정 값에 따라 상태 추가
            bool toggleReverse = ReadToggleReverseSetting();
            AnimatorState firstState, secondState;
            
            firstState = stateMachine.AddState("Off");
            firstState.motion = offClip;
            secondState = stateMachine.AddState("On");
            secondState.motion = onClip;

            // 트랜지션 설정
            AnimatorConditionMode conditionModeForSecond = toggleReverse ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If;
            AnimatorConditionMode conditionModeForFirst = toggleReverse ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;

            AnimatorStateTransition transitionToSecond = firstState.AddTransition(secondState);
            transitionToSecond.hasExitTime = false;
            transitionToSecond.exitTime = 0f;
            transitionToSecond.duration = 0f;
            transitionToSecond.AddCondition(conditionModeForSecond, 0, Md5Hash(originalName));

            AnimatorStateTransition transitionToFirst = secondState.AddTransition(firstState);
            transitionToFirst.hasExitTime = false;
            transitionToFirst.exitTime = 0f;
            transitionToFirst.duration = 0f;
            transitionToFirst.AddCondition(conditionModeForFirst, 0, Md5Hash(originalName));


            // 변경 사항 저장
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return animatorController;
        }

        private static void UpdateToggleAnimatorController(AnimatorController animatorController, string toggleItemName)
        {
            string onToggleAnimePath = $"Assets/Hirami/Toggle/Toggle_" + Md5Hash(toggleItemName) + "_on.anim";
            string offToggleAnimePath = $"Assets/Hirami/Toggle/Toggle_" + Md5Hash(toggleItemName) + "_off.anim";
            
            
            // 애니메이션 클립 로드 또는 생성
            AnimationClip onClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath) ?? new AnimationClip();
            AnimationClip offClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(offToggleAnimePath) ?? new AnimationClip();

            // 새로운 파라미터 생성 및 추가
            AnimatorControllerParameter newParam = new AnimatorControllerParameter
            {
                name = Md5Hash(toggleItemName),
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            };
            if (animatorController.parameters.All(p => p.name != Md5Hash(toggleItemName)))
            {
                animatorController.AddParameter(newParam);
            }

            // 새로운 레이어 생성 및 추가
            AnimatorControllerLayer newLayer = new AnimatorControllerLayer
            {
                name = Md5Hash(toggleItemName),
                stateMachine = new AnimatorStateMachine(),
                defaultWeight = 1f
            };
            if (animatorController.layers.All(l => l.name != Md5Hash(toggleItemName)))
            {
                AssetDatabase.AddObjectToAsset(newLayer.stateMachine, animatorController);
                animatorController.AddLayer(newLayer);
            }

            // 상태 및 트랜지션 구성
            ConfigureStateAndTransition(newLayer.stateMachine, onClip, offClip, Md5Hash(toggleItemName));

            // 변경 사항 저장
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
        }

        private static void ConfigureStateAndTransition(AnimatorStateMachine stateMachine, AnimationClip onClip, AnimationClip offClip, string paramName)
        {
            
            bool toggleReverse = ReadToggleReverseSetting();
            AnimatorState onState, offState;


            offState = stateMachine.AddState("Off");
            offState.motion = offClip;
            onState = stateMachine.AddState("On");
            onState.motion = onClip;
            

            
            // 트랜지션 생성 및 설정
            AnimatorStateTransition toOnTransition;
            AnimatorStateTransition toOffTransition;

            if (toggleReverse)
            {
                // If toggleReverse is true, reverse the transitions
                toOnTransition = offState.AddTransition(onState);
                toOnTransition.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
                toOnTransition.hasExitTime = false;

                toOffTransition = onState.AddTransition(offState);
                toOffTransition.AddCondition(AnimatorConditionMode.If, 0, paramName);
                toOffTransition.hasExitTime = false;
            }
            else
            {
                // If toggleReverse is false, use the normal transitions
                toOnTransition = offState.AddTransition(onState);
                toOnTransition.AddCondition(AnimatorConditionMode.If, 0, paramName);
                toOnTransition.hasExitTime = false;

                toOffTransition = onState.AddTransition(offState);
                toOffTransition.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
                toOffTransition.hasExitTime = false;
            }
            
            
        }

        
        private static void RecordState(string toggleItemName, string originalName, bool activation)
        {
            var stateName = activation ? "on" : "off";
            var clipName = $"{toggleItemName}_{stateName}";
            var folderPath = "Assets/Hirami/Toggle";
            var fullPath = $"{folderPath}/Toggle_{clipName}.anim";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Hirami", "Toggle");
            }


            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
            if (existingClip != null)
            {
                var overwrite = EditorUtility.DisplayDialog(
                    "Animation Clip Exists",
                    $"An animation clip already exists at '{fullPath}'. Do you want to overwrite it?",
                    "Overwrite",
                    "Cancel"
                );

                if (!overwrite)
                {
                    return;
                }
            }

            var clip = new AnimationClip { name = clipName };
            var curveBinding = EditorCurveBinding.FloatCurve(_selectGameObejct, typeof(GameObject), "m_IsActive");

            var curve = new AnimationCurve();
            curve.AddKey(0f, activation ? 1f : 0f);

            AnimationUtility.SetEditorCurve(clip, curveBinding, curve);

          
            if (clip != null)
            {
                WriteHashMappingToJson("Toggle_" + originalName, toggleItemName);
            }


            AssetDatabase.CreateAsset(clip, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        private static string GetPathForGameObject(GameObject obj)
        {
            if (obj.transform.parent == null || obj.transform == obj.transform.root)
            {
                return "";
            }

            var parentPath = GetPathForGameObject(obj.transform.parent.gameObject);
            return string.IsNullOrEmpty(parentPath) ? obj.name : parentPath + "/" + obj.name;
        }

        private static bool ReadToggleReverseSetting()
        {
            string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                var settings = JsonUtility.FromJson<ToggleSettings>(json);
                AssetDatabase.Refresh();
                return settings.toggleReverse;
            }

            return false;
        }

        private static string ReadToggleMenuNameSetting()
        {
            string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                var settings = JsonUtility.FromJson<ToggleSettings>(json);
                AssetDatabase.Refresh();
                return settings.toggleMenuName;
            }

            return "";
        }
        
        // JSON 파일에서 매핑 로드하는 함수
        private static NameHashMapping LoadNameHashMapping(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                return JsonUtility.FromJson<NameHashMapping>(jsonContent);
            }
            return null; // 파일이 없으면 null 반환
        }

    }
}

[System.Serializable]
public class ToggleSettings
{
    public bool toggleReverse;
    public string toggleMenuName;
    public string groupToggleMenuName;
}


[System.Serializable]
public class NameHashMapping
{
    public List<NameHashPair> mappings;
}

public class HiramiAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        const string settingJsonFilePath = "Assets/Hirami/Toggle/setting.json"; 
        const string JsonFilePath = "Assets/Hirami/Toggle/NameHashMappings.json";
        
        
        // JSON 파일이 존재하는지 확인
        if (!AssetDatabase.LoadAssetAtPath<TextAsset>(settingJsonFilePath))
        {
            var jsonData =
                "{\"toggleReverse\":false, \"toggleMenuName\":\"Toggles\", \"groupToggleMenuName\":\"GroupToggles\"}"; // 생성할 JSON 데이터
            File.WriteAllText(settingJsonFilePath, jsonData); 
            AssetDatabase.ImportAsset(settingJsonFilePath);
        }

        // JSON 파일 경로
        

        // JSON 파일이 없으면 생성
        if (!File.Exists(JsonFilePath))
        {
            File.WriteAllText(JsonFilePath, "{\"mappings\":[]}");
        }

        var controllerPath = "Assets/Hirami/Toggle/toogle_fx.controller"; // "toogle" -> "toggle"로 오타 수정


        if (AssetDatabase.LoadAssetAtPath<Object>(controllerPath) == null) return;
        const string newControllerName = "toggle_fx.controller";
        AssetDatabase.RenameAsset(controllerPath, newControllerName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}

#endif