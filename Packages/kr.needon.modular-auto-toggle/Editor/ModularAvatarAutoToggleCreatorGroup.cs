#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using nadena.dev.modular_avatar.core;
using Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

//v1.0.68
namespace Editor
{
    public abstract class GroupTogglePrefabCreatorGroups
    {
        private static AnimatorController _globalFxAnimator;
        private const string FolderPath = "Assets/Hirami/Toggle";
        private const string DefaultGroupToggleMenuName = "GroupToggles";
        private const string SettingFilePath = "Assets/Hirami/Toggle/setting.json";

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
                        UpdateProgressBar(++currentStep, totalSteps, $"Recording 'on' state for {selectedObject.name}...");
                        recordedSuccessfully = RecordStateForGroup(groupName, selectedObjects, true);
                    } while (!recordedSuccessfully);

                    // 'off' 상태 녹화
                    do
                    { 
                        UpdateProgressBar(++currentStep, totalSteps,
                            $"Recording 'off' state for {selectedObject.name}...");
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

        private static AnimationClip LoadOrCreateAnimationClip(string fullPath, string clipName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath) ?? new AnimationClip { name = clipName };
            if (clip == null)
            {
                AssetDatabase.CreateAsset(clip, fullPath);
            }
            return clip;
        }

        private static void SaveAsset(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateToggleItemForGameObject(GameObject selectedPrefab, string groupName)
        {
            if (selectedPrefab == null)
            {
                Debug.LogError("No GameObject selected. Please select a GameObject.");
                return;
            }

            var rootGameObject = selectedPrefab.transform.root.gameObject;
            var togglesGameObject = FindOrCreateGroupTogglesObject(rootGameObject);

            var toggleName = "Group_" + groupName;
            var existingToggle = togglesGameObject.transform.Find(toggleName);

            if (existingToggle != null)
            {
                Debug.LogWarning($"Toggle item {toggleName} already exists.");
                return;
            }

            var newToggleGameObject = new GameObject(toggleName);
            newToggleGameObject.transform.SetParent(togglesGameObject.transform, false);

            // Add the new toggle game object as the first child
            newToggleGameObject.transform.SetSiblingIndex(0);

            AddComponentsToToggleItem(newToggleGameObject, groupName);
        }

        private static GameObject FindOrCreateGroupTogglesObject(GameObject rootGameObject)
        {
            var groupName = ReadGroupToggleMenuNameSetting();
            var groupNameToUse = string.IsNullOrEmpty(groupName) ? DefaultGroupToggleMenuName : groupName;
            var togglesTransform = rootGameObject.transform.Find(groupNameToUse);

            if (togglesTransform != null)
            {
                return togglesTransform.gameObject;
            }

            var togglesGameObject = new GameObject(groupNameToUse);
            togglesGameObject.transform.SetParent(rootGameObject.transform, false);

            return togglesGameObject;
        }

        private static void AddComponentsToToggleItem(GameObject obj, string groupName)
        {
            var rootObject = Selection.activeGameObject.transform.root.gameObject;
            var togglesGameObject = FindOrCreateGroupTogglesObject(rootObject);
            
            var mergeAnimator = togglesGameObject.GetComponent<ModularAvatarMergeAnimator>() ?? CreateAndConfigureMergeAnimator(togglesGameObject);

            if (_globalFxAnimator == null)
            {
                _globalFxAnimator = LoadOrCreateAnimatorController(rootObject, groupName);
                mergeAnimator.animator = _globalFxAnimator;
            }
            else
            {
                UpdateToggleAnimatorController(rootObject, _globalFxAnimator, groupName);
            }

            ConfigureAvatarParameters(rootObject, obj, Md5Hash(rootObject.name + "_" + groupName));
            
            var toggleIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png");
            
            ConfigureMenuItem(obj, Md5Hash(rootObject.name + "_" + groupName), toggleIcon);

            obj.AddComponent<ToggleItem>();
            SaveAsset(obj);
        }

        private static ModularAvatarMergeAnimator CreateAndConfigureMergeAnimator(GameObject togglesGameObject)
        {  
            var mergeAnimator = togglesGameObject.AddComponent<ModularAvatarMergeAnimator>();
            togglesGameObject.AddComponent<ToggleConfig>();
            togglesGameObject.AddComponent<GroupDeleteToggle>();
            togglesGameObject.AddComponent<ModularAvatarMenuInstaller>();
            var toggleIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-toggle/Resource/toggleOFF.png");
            ConfigureSubMenu(togglesGameObject, toggleIcon);
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;
            return mergeAnimator;
        }

        private static AnimatorController LoadOrCreateAnimatorController(GameObject rootObject, string groupName)
        {
            var controllerPath = $"{FolderPath}/{rootObject.name}_group_toggle_fx.controller";
            var loadedAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (loadedAnimator != null)
            {
                UpdateToggleAnimatorController(rootObject, loadedAnimator, groupName);
                return loadedAnimator;
            }
            return CreateToggleAnimatorController(rootObject, groupName);
        }

        private static void ConfigureAvatarParameters(GameObject rootObject, GameObject obj, string parameterName)
        {
            var avatarParameters = obj.AddComponent<ModularAvatarParameters>();
            var param = "Group_" + rootObject.name + "_" + parameterName;

            if (avatarParameters.parameters.Any(p => p.nameOrPrefix == parameterName)) return;
            avatarParameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = param,
                syncType = ParameterSyncType.Bool,
                defaultValue = 1,
                saved = true
            });
        }

        private static void ConfigureSubMenu(GameObject obj, Texture2D icon)
        {
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();
            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.Children;
            menuItem.Control.icon = icon;
        }

        private static void ConfigureMenuItem(GameObject obj, string parameterName, Texture2D icon)
        {
            var rootObject = Selection.activeGameObject.transform.root.gameObject;
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();
            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = "Group_" + rootObject.name + "_" + parameterName };
            menuItem.Control.icon = icon;
        }

        [CreateAssetMenu(fileName = "AnimatorControllerData", menuName = "ScriptableObjects/AnimatorControllerData", order = 1)]
        public class AnimatorControllerData : ScriptableObject
        {
            public AnimatorController animatorController;
        }

        private static AnimatorController CreateToggleAnimatorController(GameObject rootObject, string groupName)
        {
            var controllerPath = $"{FolderPath}/{rootObject.name}_group_toggle_fx.controller";
            var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            var stateMachine = new AnimatorStateMachine
            {
                name = $"Group_{rootObject.name}_{Md5Hash(rootObject.name + "_" + groupName)}",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animatorController);
            animatorController.layers = new[] { new AnimatorControllerLayer { name = stateMachine.name, stateMachine = stateMachine, defaultWeight = 1f } };

            var parameter = new AnimatorControllerParameter
            {
                name = stateMachine.name,
                type = AnimatorControllerParameterType.Bool,
                defaultBool = true
            };
            animatorController.AddParameter(parameter);

            var offClip = LoadOrCreateAnimationClip($"{FolderPath}/{stateMachine.name}_off.anim", $"{stateMachine.name}_off");
            var onClip = LoadOrCreateAnimationClip($"{FolderPath}/{stateMachine.name}_on.anim", $"{stateMachine.name}_on");

            ConfigureStateAndTransition(stateMachine, onClip, offClip, stateMachine.name);
            SaveAsset(animatorController);

            return animatorController;
        }

        private static void UpdateToggleAnimatorController(GameObject rootObject, AnimatorController animatorController, string groupName)
        {
            var parameterName = $"Group_{rootObject.name}_{Md5Hash(rootObject.name + "_" + groupName)}";
            var onClip = LoadOrCreateAnimationClip($"{FolderPath}/{parameterName}_on.anim", $"{parameterName}_on");
            var offClip = LoadOrCreateAnimationClip($"{FolderPath}/{parameterName}_off.anim", $"{parameterName}_off");

            AddParameterIfNotExists(animatorController, parameterName);
            var newLayer = AddLayerIfNotExists(animatorController, parameterName);

            ConfigureStateAndTransition(newLayer.stateMachine, onClip, offClip, parameterName);
            SaveAsset(animatorController);
        }

        private static void AddParameterIfNotExists(AnimatorController animatorController, string parameterName)
        {
            if (animatorController.parameters.All(p => p.name != parameterName))
            {
                animatorController.AddParameter(new AnimatorControllerParameter
                {
                    name = parameterName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                });
            }
        }

        private static AnimatorControllerLayer AddLayerIfNotExists(AnimatorController animatorController, string parameterName)
        {
            var newLayer = new AnimatorControllerLayer
            {
                name = parameterName,
                stateMachine = new AnimatorStateMachine(),
                defaultWeight = 1f
            };
            if (animatorController.layers.All(l => l.name != parameterName))
            {
                AssetDatabase.AddObjectToAsset(newLayer.stateMachine, animatorController);
                animatorController.AddLayer(newLayer);
            }
            return newLayer;
        }

        private static void ConfigureStateAndTransition(AnimatorStateMachine stateMachine, AnimationClip onClip, AnimationClip offClip, string paramName)
        {
            var onState = stateMachine.AddState("On");
            onState.motion = onClip;
            var offState = stateMachine.AddState("Off");
            offState.motion = offClip;

            ConfigureTransitions(stateMachine, onState, offState, paramName);
        }

        private static void ConfigureTransitions(AnimatorStateMachine stateMachine, AnimatorState onState, AnimatorState offState, string paramName)
        {
            var toggleReverse = ReadToggleReverseSetting();

            if (toggleReverse)
            {
                AddTransition(offState, onState, paramName, AnimatorConditionMode.IfNot);
                AddTransition(onState, offState, paramName, AnimatorConditionMode.If);
            }
            else
            {
                AddTransition(offState, onState, paramName, AnimatorConditionMode.If);
                AddTransition(onState, offState, paramName, AnimatorConditionMode.IfNot);
            }
        }

        private static void AddTransition(AnimatorState fromState, AnimatorState toState, string paramName, AnimatorConditionMode conditionMode)
        {
            var transition = fromState.AddTransition(toState);
            transition.AddCondition(conditionMode, 0, paramName);
            transition.hasExitTime = false;
        }

        private static bool ReadToggleReverseSetting()
        {
            return File.Exists(SettingFilePath) && JsonUtility.FromJson<ToggleSettings>(File.ReadAllText(SettingFilePath)).toggleReverse;
        }



        private static string Md5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var hashBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(input));
                return string.Concat(hashBytes.Select(b => b.ToString("X2")));
            }
        }

        private static bool HasKeyframes(AnimationClip clip)
        {
            return AnimationUtility.GetCurveBindings(clip).Any(binding => AnimationUtility.GetEditorCurve(clip, binding).keys.Length > 0);
        }

        private static string ReadGroupToggleMenuNameSetting()
        {
            return File.Exists(SettingFilePath) ? JsonUtility.FromJson<ToggleSettings>(File.ReadAllText(SettingFilePath)).groupToggleMenuName : string.Empty;
        }

        [System.Serializable]
        public class NameHashMapping
        {
            public List<NameHashPair> mappings = new List<NameHashPair>();
        }

        [System.Serializable]
        public class NameHashPair
        {
            public string originalName;
            public string hashedName;
        }

        [System.Serializable]
        public class ToggleSettings
        {
            public bool toggleReverse;
            public string groupToggleMenuName;
        }
    }
}
#endif
