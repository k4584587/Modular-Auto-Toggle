#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using kr.needon.modular_auto_toggle.runtime.GroupToggleNameChange;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

//v1.0.7
namespace Editor
{
    public abstract class GroupTogglePrefabCreatorGroups
    {
        private static AnimatorController _globalFxAnimator;

        [MenuItem("GameObject/Add Groups Toggle Items", false, 0)]
        private static void CreateGroupToggleItems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length < 2)
            {
                Debug.LogError("Please select at least two GameObjects.");
                return;
            }

            EditorApplication.delayCall = null;
            EditorApplication.delayCall += () =>
            {
                var groupName = string.Join("_", selectedObjects.Select(obj => obj.name));
                var totalSteps = selectedObjects.Length * 2 + 1;
                var currentStep = 0;
                UpdateProgressBar(currentStep, totalSteps, "Initializing...");

                foreach (var selectedObject in selectedObjects)
                {
                    bool recordedSuccessfully;

                    // 'on' 상태 녹화
                    do
                    {
                        UpdateProgressBar(++currentStep, totalSteps,
                            $"Recording 'on' state for {selectedObject.name}...");
                        recordedSuccessfully = RecordStateForGroup(groupName, selectedObjects, true);
                    } while (!recordedSuccessfully);

                    // 'off' 상태 녹화
                    do
                    {
                        UpdateProgressBar(++currentStep, totalSteps,
                            $"Recording 'off' state for {selectedObject.name}...");
                        recordedSuccessfully = RecordStateForGroup(groupName, selectedObjects, false);
                    } while (!recordedSuccessfully);
                }

                // 토글 아이템 생성
                UpdateProgressBar(++currentStep, totalSteps, "Creating toggle items...");
                CreateToggleItemForGameObject(selectedObjects[0], groupName);

                // 로딩 바 종료
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Toggle Items Creation", "All toggle items have been created successfully.",
                    "OK");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }


        private static void UpdateProgressBar(int currentStep, int totalSteps, string message)
        {
            var progress = (float)currentStep / totalSteps;
            EditorUtility.DisplayProgressBar("Processing", message, progress);
        }

        private static bool RecordStateForGroup(string groupName, GameObject[] selectedObjects, bool activation)
        {
            var hashedGroupName = Md5Hash(groupName); // 그룹 이름을 해시하여 사용
            var stateName = activation ? "on" : "off";
            var clipName = $"Group_{hashedGroupName}_{stateName}"; // 해시된 그룹 이름을 파일 이름에 사용
            var folderPath = "Assets/Hirami/Toggle";
            var fullPath = $"{folderPath}/{clipName}.anim";

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Hirami", "Toggle");
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
                AssetDatabase.CreateAsset(clip, fullPath);
            }
            else
            {
                // 클립이 이미 존재하는 경우 키 프레임을 확인합니다.
                if (HasKeyframes(clip))
                {
                    return true; // 키 프레임이 있는 경우 true 반환
                }
            }

            foreach (var selectedObject in selectedObjects)
            {
                var path = AnimationUtility.CalculateTransformPath(selectedObject.transform,
                    selectedObject.transform.root);
                var curveBinding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
                var curve = new AnimationCurve(new Keyframe(0f, activation ? 1f : 0f));
                AnimationUtility.SetEditorCurve(clip, curveBinding, curve);
            }

            // JSON 파일에 기록을 남깁니다.
            WriteHashMappingToJson(groupName, hashedGroupName);

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return true; // 성공적으로 녹화 및 저장된 경우 true 반환
        }


        private static void CreateToggleItemForGameObject(GameObject selectedPrefab, string groupName)
        {
            if (selectedPrefab == null)
            {
                Debug.LogError("No GameObject selected. Please select a GameObject.");
                return;
            }

            var rootGameObject = selectedPrefab.transform.root.gameObject;

            var togglesTransform = rootGameObject.transform.Find("GroupToggle");
            var togglesGameObject = togglesTransform != null ? togglesTransform.gameObject : null;

            if (togglesGameObject == null)
            {
                togglesGameObject = new GameObject("GroupToggle");
                togglesGameObject.transform.SetParent(rootGameObject.transform, false);
            }

            var toggleName = "Group_" + groupName;
            var newToggleGameObject = new GameObject(toggleName);
            newToggleGameObject.transform.SetParent(togglesGameObject.transform, false);

            AddComponentsToToggleItem(newToggleGameObject, groupName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        private static void AddComponentsToToggleItem(GameObject obj, string groupName)
        {
            var togglesGameObject = GameObject.Find("GroupToggle") ?? new GameObject("GroupToggle");
            if (togglesGameObject.transform.parent == null)
            {
                togglesGameObject.transform.SetParent(obj.transform.parent);
            }

            var mergeAnimator = togglesGameObject.GetComponent<ModularAvatarMergeAnimator>();
            if (mergeAnimator == null)
            {
                togglesGameObject.AddComponent<ModularAvatarMenuInstaller>();
                TogglesConfigureSubMenuItem(togglesGameObject);
                mergeAnimator = togglesGameObject.AddComponent<ModularAvatarMergeAnimator>();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var controllerPath = $"Assets/Hirami/Toggle/group_toggle_fx.controller";

            if (_globalFxAnimator == null)
            {
                var loadedAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (loadedAnimator != null)
                {
                    UpdateToggleAnimatorController(loadedAnimator, groupName);
                    _globalFxAnimator = loadedAnimator;
                }
                else
                {
                    _globalFxAnimator = CreateToggleAnimatorController(groupName);
                }

                mergeAnimator.animator = _globalFxAnimator;
            }
            else
            {
                UpdateToggleAnimatorController(_globalFxAnimator, groupName);
            }


            mergeAnimator.animator = _globalFxAnimator;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;

            obj.AddComponent<GroupToggleNameChange>();


            ConfigureAvatarParameters(obj, "Group_" + groupName);
            ConfigureMenuItem(obj, "Group_" + groupName);


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
                nameOrPrefix = parameterName,
                syncType = ParameterSyncType.Bool,
                defaultValue = 1,
                saved = true
            };
            avatarParameters.parameters.Add(newParameter);
        }

        private static void TogglesConfigureSubMenuItem(GameObject obj)
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


        [CreateAssetMenu(fileName = "AnimatorControllerData", menuName = "ScriptableObjects/AnimatorControllerData",
            order = 1)]
        public class AnimatorControllerData : ScriptableObject
        {
            public AnimatorController animatorController;
        }


        private static AnimatorController CreateToggleAnimatorController(string groupName)
        {
            Debug.Log("CreateToggleAnimatorController 함수 시작");
            var layerName = $"Group_{groupName}";
            var controllerPath = $"Assets/Hirami/Toggle/group_toggle_fx.controller";
            var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);


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


            var parameterName = layerName;
            if (animatorController.parameters.All(p => p.name != parameterName))
            {
                var parameter = new AnimatorControllerParameter
                {
                    name = parameterName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                };
                animatorController.AddParameter(parameter);
            }

            var layer = animatorController.layers.FirstOrDefault(l => l.name == layerName);
            if (layer == null)
            {
                layer = new AnimatorControllerLayer
                {
                    name = layerName,
                    stateMachine = new AnimatorStateMachine(),
                    defaultWeight = 1f
                };
                AssetDatabase.AddObjectToAsset(layer.stateMachine, animatorController);
                animatorController.AddLayer(layer);
            }

            var hashedGroupName = Md5Hash(groupName); // 그룹 이름을 해시하여 사용

            var offState = FindOrCreateState(layer.stateMachine, $"Group_{hashedGroupName}_off", animatorController);
            var onState = FindOrCreateState(layer.stateMachine, $"Group_{hashedGroupName}_on", animatorController);

            bool toggleReverse = ReadToggleReverseSetting();

            LinkAnimationClipToState(offState, groupName, "off");
            LinkAnimationClipToState(onState, groupName, "on");

            Debug.Log("toggleReverse ::" + toggleReverse);

            if (toggleReverse)
            {
                Debug.Log(
                    "UpdateToggleAnimatorController CreateToggleAnimatorController Group Toggle toggleReverse 반전 상태 왔음");
                CreateOrUpdateTransition(layer.stateMachine, offState, layerName, false);
                CreateOrUpdateTransition(layer.stateMachine, onState, layerName, true);
            }
            else
            {
                // 기본값 일때
                Debug.Log("UpdateToggleAnimatorController Group Toggle toggleReverse 반전 아닌상태 왔음 (기본값)");
                CreateOrUpdateTransition(layer.stateMachine, onState, layerName, false);
                CreateOrUpdateTransition(layer.stateMachine, offState, layerName, true);
            }


            return animatorController;
        }

        private static void UpdateToggleAnimatorController(AnimatorController animatorController, string groupName)
        {
            Debug.Log("UpdateToggleAnimatorController 함수 탔음");
            var layerName = $"Group_{groupName}";

            if (animatorController.parameters.All(p => p.name != layerName))
            {
                var parameter = new AnimatorControllerParameter
                {
                    name = layerName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                };
                animatorController.AddParameter(parameter);
            }

            var layer = animatorController.layers.FirstOrDefault(l => l.name == layerName);
            if (layer == null)
            {
                layer = new AnimatorControllerLayer
                {
                    name = layerName,
                    stateMachine = new AnimatorStateMachine(),
                    defaultWeight = 1f
                };
                AssetDatabase.AddObjectToAsset(layer.stateMachine, animatorController);
                animatorController.AddLayer(layer);
            }

            var hashedGroupName = Md5Hash(groupName);

            var offState = FindOrCreateState(layer.stateMachine, $"Group_{hashedGroupName}_off", animatorController);
            var onState = FindOrCreateState(layer.stateMachine, $"Group_{hashedGroupName}_on", animatorController);

            LinkAnimationClipToState(offState, groupName, "off");
            LinkAnimationClipToState(onState, groupName, "on");

            bool toggleReverse = ReadToggleReverseSetting();
            Debug.Log("toggleReverse ::" + toggleReverse);

            if (toggleReverse)
            {
                Debug.Log("UpdateToggleAnimatorController Group Toggle toggleReverse 반전 상태 왔음");

                CreateOrUpdateTransition(layer.stateMachine, offState, layerName, false);
                CreateOrUpdateTransition(layer.stateMachine, onState, layerName, true);
            }
            else
            {
                Debug.Log("UpdateToggleAnimatorController Group Toggle toggleReverse 반전 아닌상태 왔음 (기본값)");
                CreateOrUpdateTransition(layer.stateMachine, onState, layerName, false);
                CreateOrUpdateTransition(layer.stateMachine, offState, layerName, true);
            }


            EditorUtility.SetDirty(animatorController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        private static void CreateOrUpdateTransition(AnimatorStateMachine stateMachine, AnimatorState targetState,
            string parameterName, bool conditionValue)
        {
            var transition = stateMachine.anyStateTransitions.FirstOrDefault(t => t.destinationState == targetState);
            if (transition == null)
            {
                transition = stateMachine.AddAnyStateTransition(targetState);
            }

            transition.hasExitTime = false;
            transition.duration = 0;
            transition.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = conditionValue
                        ? AnimatorConditionMode.IfNot
                        : AnimatorConditionMode.If, // 조건의 참/거짓 값을 반대로 설정
                    parameter = parameterName,
                    threshold = 0
                }
            };
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine, string stateName,
            [NotNull] AnimatorController animatorController)
        {
            if (animatorController == null) throw new ArgumentNullException(nameof(animatorController));
            var state = stateMachine.states.FirstOrDefault(s => s.state.name == stateName).state;
            if (state != null) return state;
            state = stateMachine.AddState(stateName);

            var clip = new AnimationClip { name = stateName };
            AssetDatabase.AddObjectToAsset(clip, animatorController);
            state.motion = clip;

            return state;
        }

        private static void LinkAnimationClipToState(AnimatorState state, string groupName, string clipSuffix)
        {
            var hashedGroupName = Md5Hash(groupName);

            var clipName = $"Group_{hashedGroupName}_{clipSuffix}";
            var clipPath = $"Assets/Hirami/Toggle/{clipName}.anim";

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
                AssetDatabase.CreateAsset(clip, clipPath);

                var curveBinding = EditorCurveBinding.FloatCurve("", typeof(GameObject), "m_IsActive");
                var curve = new AnimationCurve(new Keyframe(0f, clipSuffix == "on" ? 1f : 0f));
                AnimationUtility.SetEditorCurve(clip, curveBinding, curve);

                EditorUtility.SetDirty(clip);
            }

            state.motion = clip;
        }

        private static bool ReadToggleReverseSetting()
        {
            string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                var settings = JsonUtility.FromJson<ToggleSettings>(json);
                return settings.toggleReverse;
            }

            return false;
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


        private static bool HasKeyframes(AnimationClip clip)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve.keys.Length > 0)
                    return true;
            }

            return false;
        }


        // JSON 파일에 기록을 남기는 메소드
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
            NameHashPair newPair = new NameHashPair { originalName = "Group_" + originalName, hashedName = hashedName };
            nameHashMapping.mappings.Add(newPair);

            // JSON 파일 쓰기
            string newJson = JsonUtility.ToJson(nameHashMapping, true);
            File.WriteAllText(jsonFilePath, newJson);
            AssetDatabase.Refresh();
        }


        [System.Serializable]
        public class NameHashMapping
        {
            public List<NameHashPair> mappings;

            // 기본 생성자 추가
            public NameHashMapping()
            {
                mappings = new List<NameHashPair>();
            }
        }

        [System.Serializable]
        public class NameHashPair
        {
            public string originalName;
            public string hashedName;
        }
    }
}
#endif