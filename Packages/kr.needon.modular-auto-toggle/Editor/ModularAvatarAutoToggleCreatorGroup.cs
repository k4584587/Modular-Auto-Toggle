#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            
            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogError("The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 GameObject들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.");
                EditorUtility.DisplayDialog("Error", "The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 GameObject들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.", "OK");
                return;
            }
            
            if (selectedObjects.Length < 2)
            {
                Debug.LogError("Please select at least two GameObjects.");
                EditorUtility.DisplayDialog(
                    "Warning",   
                    "Please select at least two GameObjects.\n적어도 두 개의 오브젝트를 선택해주세요.", 
                    "OK"
                );

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
                EditorUtility.DisplayDialog("Toggle Items Creation", "All toggle items have been created successfully.\n\n모든 토글 아이템이 성공적으로 생성되었습니다.",
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

            var togglesTransform = rootGameObject.transform.Find(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggle" : ReadGroupToggleMenuNameSetting());
            var togglesGameObject = togglesTransform != null ? togglesTransform.gameObject : null;

            if (togglesGameObject == null)
            {
                togglesGameObject = new GameObject(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggle" : ReadGroupToggleMenuNameSetting());
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
            var togglesGameObject = GameObject.Find(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggle" : ReadGroupToggleMenuNameSetting()) ?? new GameObject(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggle" : ReadGroupToggleMenuNameSetting());
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


            ConfigureAvatarParameters(obj, Md5Hash(groupName));
            ConfigureMenuItem(obj, Md5Hash(groupName));


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
            var layerName = $"Group_{groupName}";
            var controllerPath = $"Assets/Hirami/Toggle/group_toggle_fx.controller";
            
            string onToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + Md5Hash(groupName) + "_on.anim";
            string offToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + Md5Hash(groupName) + "_off.anim";
            
            var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            
     
            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = Md5Hash(groupName),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animatorController);
            animatorController.layers = new AnimatorControllerLayer[]
            {
                new AnimatorControllerLayer
                {
                    name = Md5Hash(groupName),
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                }
            };

            AnimatorControllerParameter parameter = new AnimatorControllerParameter
            {
                name = Md5Hash(groupName),
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
           
            
            // Determine the condition modes based on the toggleReverse flag
            AnimatorConditionMode conditionModeForSecond = toggleReverse ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If;
            AnimatorConditionMode conditionModeForFirst = toggleReverse ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;

            // Create the transition to the second state
            AnimatorStateTransition transitionToSecond = firstState.AddTransition(secondState);
            transitionToSecond.hasExitTime = false;
            transitionToSecond.exitTime = 0f;
            transitionToSecond.duration = 0f;
            transitionToSecond.AddCondition(conditionModeForSecond, 0, Md5Hash(groupName));

            // Create the transition to the first state
            AnimatorStateTransition transitionToFirst = secondState.AddTransition(firstState);
            transitionToFirst.hasExitTime = false;
            transitionToFirst.exitTime = 0f;
            transitionToFirst.duration = 0f;
            transitionToFirst.AddCondition(conditionModeForFirst, 0, Md5Hash(groupName));


            // 변경 사항 저장
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            
            return animatorController;
        }

        private static void UpdateToggleAnimatorController(AnimatorController animatorController, string groupName)
        {
            string onToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + Md5Hash(groupName) + "_on.anim";
            string offToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + Md5Hash(groupName) + "_off.anim";
            
            // 애니메이션 클립 로드 또는 생성
            AnimationClip onClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath) ?? new AnimationClip();
            AnimationClip offClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(offToggleAnimePath) ?? new AnimationClip();

            // 새로운 파라미터 생성 및 추가
            AnimatorControllerParameter newParam = new AnimatorControllerParameter
            {
                name = Md5Hash(groupName),
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            };
            if (!animatorController.parameters.Any(p => p.name == Md5Hash(groupName)))
            {
                animatorController.AddParameter(newParam);
            }

            // 새로운 레이어 생성 및 추가
            AnimatorControllerLayer newLayer = new AnimatorControllerLayer
            {
                name = Md5Hash(groupName),
                stateMachine = new AnimatorStateMachine(),
                defaultWeight = 1f
            };
            if (!animatorController.layers.Any(l => l.name == Md5Hash(groupName)))
            {
                AssetDatabase.AddObjectToAsset(newLayer.stateMachine, animatorController);
                animatorController.AddLayer(newLayer);
            }

            // 상태 및 트랜지션 구성
            ConfigureStateAndTransition(newLayer.stateMachine, onClip, offClip, Md5Hash(groupName));

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

        
        private static string ReadGroupToggleMenuNameSetting()
        {
            string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                var settings = JsonUtility.FromJson<ToggleSettings>(json);
                AssetDatabase.Refresh();
                return settings.groupToggleMenuName;
            }

            return "";
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