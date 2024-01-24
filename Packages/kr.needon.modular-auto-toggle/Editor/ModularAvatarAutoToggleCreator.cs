#if UNITY_EDITOR
using System;
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

//v1.0.7
namespace Hirami.Scripts
{
    public abstract class TogglePrefabCreator
    {
        private static AnimatorController _globalFxAnimator;
        private static string _selectGameObejct;
        private static string TogglePrefabName = ReadToggleMenuNameSetting();

        [MenuItem("GameObject/Add Toggle Items", false, 0)]
        private static void CreateToggleItemsInHierarchy()
        {
            
            Debug.Log("ToggleName :: " + TogglePrefabName);
            
            string settingJsonFilePath = "Assets/Hirami/Toggle/setting.json"; // JSON 파일 경로

            // JSON 파일이 존재하는지 확인
            if (!AssetDatabase.LoadAssetAtPath<TextAsset>(settingJsonFilePath))
            {
                string jsonData = "{\"toggleReverse\":false}"; // 생성할 JSON 데이터
                System.IO.File.WriteAllText(settingJsonFilePath, jsonData); // 파일 생성
                AssetDatabase.ImportAsset(settingJsonFilePath); // Unity 에셋 데이터베이스에 파일 추가
                Debug.Log("setting.json 파일이 생성되었습니다.");
            }
            
            // JSON 파일 경로
            string jsonFilePath = "Assets/Hirami/Toggle/NameHashMappings.json";

            // JSON 파일이 없으면 생성
            if (!File.Exists(jsonFilePath))
            {
                File.WriteAllText(jsonFilePath, "{\"mappings\":[]}");
            }

            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogError("No GameObjects selected. Please select one or more GameObjects.");
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
                    EditorUtility.DisplayProgressBar("Creating Toggle Items", $"Processing {selectedPrefab.name}...", progress);

                    CreateToggleItemForGameObject(selectedPrefab);
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Toggle Items Creation", "All toggle items have been created successfully.", "OK");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        private static void CreateToggleItemForGameObject(GameObject selectedPrefab)
        {
            if (selectedPrefab == null)
            {
                Debug.LogError("No GameObject selected. Please select a GameObject.");
                return;
            }


            var parentPath = GetPathForGameObject(selectedPrefab.transform.parent.gameObject);
            Debug.Log(_selectGameObejct = string.IsNullOrEmpty(parentPath)
                ? selectedPrefab.name
                : parentPath + "/" + selectedPrefab.name);
            var rootGameObject = selectedPrefab.transform.root.gameObject;

            var togglesTransform = rootGameObject.transform.Find("Toggles");
            var togglesGameObject = togglesTransform != null ? togglesTransform.gameObject : null;

            if (togglesGameObject == null)
            {
                togglesGameObject = new GameObject("Toggles");
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
            // JSON 파일 경로
            string jsonFilePath = "Assets/Hirami/Toggle/NameHashMappings.json";

            // 기존 데이터 로드 또는 새로운 구조 생성
            NameHashMapping nameHashMapping;
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
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

            // 새 기록 추가
            NameHashPair newPair = new NameHashPair { originalName = originalName, hashedName = hashedName };
            nameHashMapping.mappings.Add(newPair);

            // JSON 파일 쓰기
            string newJson = JsonUtility.ToJson(nameHashMapping, true);
            File.WriteAllText(jsonFilePath, newJson);
            AssetDatabase.Refresh();
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
            var toggleName = "Toggle_" + prefabName;

            var togglesGameObject = GameObject.Find("Toggles") ?? new GameObject("Toggles");
            if (togglesGameObject.transform.parent == null)
            {
                togglesGameObject.transform.SetParent(obj.transform.parent);
            }

            var mergeAnimator = togglesGameObject.GetComponent<ModularAvatarMergeAnimator>();
            if (mergeAnimator == null)
            {
                togglesGameObject.AddComponent<ModularAvatarMenuInstaller>();
                ToglesConfigureSubMenuItem(togglesGameObject);
                mergeAnimator = togglesGameObject.AddComponent<ModularAvatarMergeAnimator>();
            }

            // MD5 해시된 이름 생성
            string hashedToggleName = Md5Hash(toggleName);

            RecordState(hashedToggleName, toggleName, true);
            RecordState(hashedToggleName, toggleName, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_globalFxAnimator == null)
            {
                _globalFxAnimator = CreateToggleAnimatorController(hashedToggleName, toggleName);
                mergeAnimator.animator = _globalFxAnimator;
            }
            else
            {
                UpdateToggleAnimatorController(_globalFxAnimator, hashedToggleName, toggleName);
            }

            mergeAnimator.animator = _globalFxAnimator;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;

            obj.AddComponent<ToggleNameChange>();

            ConfigureAvatarParameters(obj, toggleName);
            ConfigureMenuItem(obj, toggleName);

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
                nameOrPrefix = "Toggle_" + parameterName,
                syncType = ParameterSyncType.Bool,
                defaultValue = 1,
                saved = true
            };
            avatarParameters.parameters.Add(newParameter);
        }

        private static void ToglesConfigureSubMenuItem(GameObject obj)
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


        [CreateAssetMenu(fileName = "AnimatorControllerData", menuName = "ScriptableObjects/AnimatorControllerData", order = 1)]
        public class AnimatorControllerData : ScriptableObject
        {
            public AnimatorController animatorController;
        }


        private static AnimatorController CreateToggleAnimatorController(string toggleItemName, string originalName)
        {
            string controllerPath = $"Assets/Hirami/Toggle/toggle_fx.controller";
            AnimatorController animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (animatorController == null)
            {
                animatorController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

                if (animatorController.layers.Any(l => l.name == "Base Layer"))
                {
                    var layersList = animatorController.layers.ToList();
                    layersList.RemoveAll(l => l.name == "Base Layer");
                    animatorController.layers = layersList.ToArray();
                }
            }


            if (animatorController == null)
            {
                animatorController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            var existingLayer = animatorController.layers.FirstOrDefault(l => l.name == toggleItemName);
            if (existingLayer == null)
            {

                var stateMachine = new AnimatorStateMachine
                {
                    name = ""
                };
                AssetDatabase.AddObjectToAsset(stateMachine, animatorController);


                var newLayer = new AnimatorControllerLayer
                {
                    name = originalName,
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                };
                animatorController.AddLayer(newLayer);

                var parameter = new AnimatorControllerParameter
                {
                    name = originalName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                };
                
                animatorController.AddParameter(parameter);

                var idleState = stateMachine.AddState(toggleItemName + "_off");
                var toggleOnState = stateMachine.AddState(toggleItemName + "_on");

                bool toggleReverse = ReadToggleReverseSetting();
                Debug.Log("toggleReverse :: " + toggleReverse);

                LinkAnimationClipToState(idleState, "Toggle_" + toggleItemName, "off");
                LinkAnimationClipToState(toggleOnState, "Toggle_" + toggleItemName, "on");


                if (toggleReverse)
                {
                    Debug.Log("toggleReverse true 왔음 (반전)");

                    CreateStateTransition(stateMachine, toggleOnState, parameter.name, true);
                    CreateStateTransition(stateMachine, idleState, parameter.name, false);
                }
                else
                {
                    Debug.Log("toggleReverse false 왔음 (기본)");
           
                    CreateStateTransition(stateMachine, toggleOnState, parameter.name, false);
                    CreateStateTransition(stateMachine, idleState, parameter.name, true);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return animatorController;
        }

        private static void UpdateToggleAnimatorController(AnimatorController animatorController, string toggleItemName, string originalName)
        {
            var layerName = originalName;
            var layer = animatorController.layers.FirstOrDefault(l => l.name == layerName);
            if (layer == null)
            {
                var stateMachine = new AnimatorStateMachine
                {
                    name = ""
                };
                AssetDatabase.AddObjectToAsset(stateMachine, animatorController);
                AssetDatabase.SaveAssets();

                layer = new AnimatorControllerLayer
                {
                    name = layerName,
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                };
                animatorController.AddLayer(layer);
            }

            var parameter = animatorController.parameters.FirstOrDefault(p => p.name == toggleItemName);
            if (parameter == null)
            {
                parameter = new AnimatorControllerParameter
                {
                    name = originalName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                };
                animatorController.AddParameter(parameter);
            }


            else if (parameter.type != AnimatorControllerParameterType.Bool || parameter.defaultBool != true)
            {
                parameter.type = AnimatorControllerParameterType.Bool;
                parameter.defaultBool = true;
            }


            var onState = FindOrCreateState(layer.stateMachine, toggleItemName + "_off", animatorController);
            var offState = FindOrCreateState(layer.stateMachine, toggleItemName + "_on", animatorController);

            LinkAnimationClipToState(onState, "Toggle_" + toggleItemName, "off");
            LinkAnimationClipToState(offState, "Toggle_" + toggleItemName, "on");

            bool toggleReverse = ReadToggleReverseSetting();

            if (toggleReverse)
            {
            CreateStateTransition(layer.stateMachine, offState, parameter.name, true);
            CreateStateTransition(layer.stateMachine, onState, parameter.name, false);

            }
            else 
            {
            CreateStateTransition(layer.stateMachine, offState, parameter.name, false);
            CreateStateTransition(layer.stateMachine, onState, parameter.name, true);
            }


            EditorUtility.SetDirty(animatorController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine, string stateName, AnimatorController animatorController)
        {
            var state = stateMachine.states.FirstOrDefault(s => s.state.name == stateName).state;
            if (state != null) return state;
            state = stateMachine.AddState(stateName);

            var clip = new AnimationClip { name = stateName };
            AssetDatabase.AddObjectToAsset(clip, animatorController);
            state.motion = clip;
            return state;
        }


        private static void LinkAnimationClipToState(AnimatorState state, string itemName, string clipSuffix)
        {
            var clipPath = $"Assets/Hirami/Toggle/{itemName}_{clipSuffix}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);

                var curveBinding = EditorCurveBinding.FloatCurve("", typeof(GameObject), "m_IsActive");
                var curve = new AnimationCurve(new Keyframe(0f, clipSuffix == "on" ? 1f : 0f));
                AnimationUtility.SetEditorCurve(clip, curveBinding, curve);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            state.motion = clip;
        }


        private static void CreateStateTransition(AnimatorStateMachine stateMachine, AnimatorState targetState,
            string parameterName, bool toIdle)
        {
            var transition = stateMachine.AddAnyStateTransition(targetState);
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.canTransitionToSelf = false;
            transition.AddCondition(toIdle ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 0, parameterName);
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

            // 애니메이션 클립이 실제로 생성되었을 때만 JSON 파일에 기록을 남깁니다.
            if (clip != null)
            {
                WriteHashMappingToJson(originalName, toggleItemName);
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


    }
}

[System.Serializable]
public class ToggleSettings
{
    public bool toggleReverse;
    public string toggleMenuName;
}


[System.Serializable]
public class NameHashMapping
{
    public List<NameHashPair> mappings;
}

public class HiramiAssetPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        string controllerPath = "Assets/Hirami/Toggle/toogle_fx.controller"; // "toogle" -> "toggle"로 오타 수정


        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(controllerPath) != null)
        {

            string newControllerName = "toggle_fx.controller";
            AssetDatabase.RenameAsset(controllerPath, newControllerName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}

#endif