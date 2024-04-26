#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

//v1.0.67
namespace Editor
{
    public abstract class GroupTogglePrefabCreatorGroups
    {
        private static AnimatorController _globalFxAnimator;

        [MenuItem("GameObject/Add Groups Toggle Items", false, 0)]
        private static void CreateGroupToggleItems()
        {
            var selectedObjects = Selection.gameObjects;
            
            var targetFolderPath = "Assets/Hirami/Toggle";
            if (!AssetDatabase.IsValidFolder(targetFolderPath)) //폴더 생성이 안될때 생성하는 함수
            {
                string[] folders = targetFolderPath.Split('/');
                string parentFolder = folders[0];


                for (int i = 1; i < folders.Length; i++)
                {
                    string folderPath = parentFolder + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(folderPath))
                    {
                        AssetDatabase.CreateFolder(parentFolder, folders[i]);
                        Debug.Log(folderPath + " folder has been created.");
                    }

                    parentFolder = folderPath;
                }
            }
            
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
                        UpdateProgressBar(++currentStep, totalSteps, $"Recording 'on' state for {selectedObject.name}...");
                        recordedSuccessfully = RecordStateForGroup(groupName, selectedObjects, true);
                    } while (!recordedSuccessfully);

                    // 'off' 상태 녹화
                    do
                    {
                        UpdateProgressBar(++currentStep, totalSteps, $"Recording 'off' state for {selectedObject.name}...");
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
            var rootObject = Selection.activeGameObject.transform.root.gameObject;
            
            var hashedGroupName = Md5Hash(rootObject.name + "_" + groupName); // 그룹 이름을 해시하여 사용
            var stateName = activation ? "on" : "off";
            var clipName = $"Group_{rootObject.name}_{hashedGroupName}_{stateName}"; // 해시된 그룹 이름을 파일 이름에 사용
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

            var togglesTransform = rootGameObject.transform.Find(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggles" : ReadGroupToggleMenuNameSetting());
            var togglesGameObject = togglesTransform != null ? togglesTransform.gameObject : null;

            if (togglesGameObject == null)
            {
                togglesGameObject = new GameObject(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggles" : ReadGroupToggleMenuNameSetting());
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
            
            var rootObject = Selection.activeGameObject.transform.root.gameObject;
            
            var togglesGameObject = GameObject.Find(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggles" : ReadGroupToggleMenuNameSetting()) ?? new GameObject(string.IsNullOrEmpty(ReadGroupToggleMenuNameSetting()) ? "GroupToggles" : ReadGroupToggleMenuNameSetting());
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

            Debug.Log("Root Object Name: " + rootObject.name);
            var controllerPath = $"Assets/Hirami/Toggle/" + rootObject.name + "_group_toggle_fx.controller";

            if (_globalFxAnimator == null)
            {
                var loadedAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (loadedAnimator != null)
                {
                    UpdateToggleAnimatorController(rootObject, loadedAnimator, groupName);
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
                UpdateToggleAnimatorController(rootObject, _globalFxAnimator, groupName);
            }


            mergeAnimator.animator = _globalFxAnimator;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;

            ConfigureAvatarParameters(rootObject, obj, Md5Hash(rootObject.name + "_" + groupName));
            ConfigureMenuItem(obj, Md5Hash(rootObject.name + "_" +  groupName));


            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        private static void ConfigureAvatarParameters(GameObject rootObject, GameObject obj, string parameterName)
        {
            var avatarParameters = obj.AddComponent<ModularAvatarParameters>();

            var parameterExists = avatarParameters.parameters.Any(p => p.nameOrPrefix == parameterName);

            var param = "Group_" + rootObject.name + "_"  + parameterName;
            Debug.Log("Group Param Name :: " + param);
            
            if (parameterExists) return;
            var newParameter = new ParameterConfig
            {
                nameOrPrefix =  param,
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
            
            var rootObject = Selection.activeGameObject.transform.root.gameObject;
            
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = "Group_" + rootObject.name + "_" +  parameterName };
        }


        [CreateAssetMenu(fileName = "AnimatorControllerData", menuName = "ScriptableObjects/AnimatorControllerData", order = 1)]
        public class AnimatorControllerData : ScriptableObject
        {
            public AnimatorController animatorController;
        }


        private static AnimatorController CreateToggleAnimatorController(string groupName)
        {
            var rootObject = Selection.activeGameObject.transform.root.gameObject;
            var controllerPath = $"Assets/Hirami/Toggle/" + rootObject.name + "_group_toggle_fx.controller";
            
            string onToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" + groupName) + "_on.anim";
            string offToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" +  groupName) + "_off.anim";
            
            var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            
     
            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = "Group_" + rootObject.name + "_" +  Md5Hash(rootObject.name + "_" + groupName),
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animatorController);
            animatorController.layers = new AnimatorControllerLayer[]
            {
                new AnimatorControllerLayer
                {
                    name = "Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" + groupName),
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                }
            };

            AnimatorControllerParameter parameter = new AnimatorControllerParameter
            {
                name = "Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" +  groupName),
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
            transitionToSecond.AddCondition(conditionModeForSecond, 0, "Group_" + rootObject.name + "_" +  Md5Hash(rootObject.name + "_" + groupName));

            // Create the transition to the first state
            AnimatorStateTransition transitionToFirst = secondState.AddTransition(firstState);
            transitionToFirst.hasExitTime = false;
            transitionToFirst.exitTime = 0f;
            transitionToFirst.duration = 0f;
            transitionToFirst.AddCondition(conditionModeForFirst, 0, "Group_" + rootObject.name + "_" +  Md5Hash(rootObject.name + "_" + groupName));


            // 변경 사항 저장
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            
            return animatorController;
        }

        private static void UpdateToggleAnimatorController(GameObject rootObject, AnimatorController animatorController, string groupName)
        {
            
            string onToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" +groupName) + "_on.anim";
            string offToggleAnimePath = $"Assets/Hirami/Toggle/Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" + groupName) + "_off.anim";
            
            // 애니메이션 클립 로드 또는 생성
            AnimationClip onClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath) ?? new AnimationClip();
            AnimationClip offClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(offToggleAnimePath) ?? new AnimationClip();

            // 새로운 파라미터 생성 및 추가
            AnimatorControllerParameter newParam = new AnimatorControllerParameter
            {
                name = "Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" + groupName),
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            };
            if (animatorController.parameters.All(p => p.name != Md5Hash(rootObject.name + "_" + groupName)))
            {
                animatorController.AddParameter(newParam);
            }

            // 새로운 레이어 생성 및 추가
            AnimatorControllerLayer newLayer = new AnimatorControllerLayer
            {
                name = "Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" + groupName),
                stateMachine = new AnimatorStateMachine(),
                defaultWeight = 1f
            };
            if (animatorController.layers.All(l => l.name != Md5Hash(rootObject.name + "_" + groupName)))
            {
                AssetDatabase.AddObjectToAsset(newLayer.stateMachine, animatorController);
                animatorController.AddLayer(newLayer);
            }

            // 상태 및 트랜지션 구성
            ConfigureStateAndTransition(newLayer.stateMachine, onClip, offClip, "Group_" + rootObject.name + "_" + Md5Hash(rootObject.name + "_" + groupName));

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