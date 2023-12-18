#if UNITY_EDITOR
using System;
using System.Linq;
using JetBrains.Annotations;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Hirami.Scripts
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
                    // 'on' 상태 녹화
                    UpdateProgressBar(++currentStep, totalSteps, $"Recording 'on' state for {selectedObject.name}...");
                    RecordStateForGroup(groupName, selectedObjects, true);

                    // 'off' 상태 녹화
                    UpdateProgressBar(++currentStep, totalSteps, $"Recording 'off' state for {selectedObject.name}...");
                    RecordStateForGroup(groupName, selectedObjects, false);
                }

                // 토글 아이템 생성
                UpdateProgressBar(++currentStep, totalSteps, "Creating toggle items...");
                CreateToggleItemForGameObject(selectedObjects[0], groupName);

                // 로딩 바 종료
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Toggle Items Creation", "All toggle items have been created successfully.", "OK");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        private static void UpdateProgressBar(int currentStep, int totalSteps, string message)
        {
            var progress = (float)currentStep / totalSteps;
            EditorUtility.DisplayProgressBar("Processing", message, progress);
        }

        private static void RecordStateForGroup(string groupName, GameObject[] selectedObjects, bool activation)
        {
            var stateName = activation ? "on" : "off";
            var clipName = $"Group_{groupName}_{stateName}";
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
                return;
            }

            foreach (var selectedObject in selectedObjects)
            {
                var path = AnimationUtility.CalculateTransformPath(selectedObject.transform, selectedObject.transform.root);
                var curveBinding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
                var curve = new AnimationCurve(new Keyframe(0f, activation ? 1f : 0f));
                AnimationUtility.SetEditorCurve(clip, curveBinding, curve);
            }

            EditorUtility.SetDirty(clip);
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


        [CreateAssetMenu(fileName = "AnimatorControllerData", menuName = "ScriptableObjects/AnimatorControllerData", order = 1)]
        public class AnimatorControllerData : ScriptableObject
        {
            public AnimatorController animatorController;
        }


        private static AnimatorController CreateToggleAnimatorController(string groupName)
        {
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
            
            var offState = FindOrCreateState(layer.stateMachine, $"Group_{groupName}_off", animatorController);
            var onState = FindOrCreateState(layer.stateMachine, $"Group_{groupName}_on", animatorController);
            
            LinkAnimationClipToState(offState, groupName, "off");
            LinkAnimationClipToState(onState, groupName, "on");
            
            var transitionToOff = layer.stateMachine.AddAnyStateTransition(offState);
            transitionToOff.hasExitTime = false;
            transitionToOff.duration = 0;
            transitionToOff.AddCondition(AnimatorConditionMode.IfNot, 0, parameterName);
            
            var transitionToOn = layer.stateMachine.AddAnyStateTransition(onState);
            transitionToOn.hasExitTime = false;
            transitionToOn.duration = 0;
            transitionToOn.AddCondition(AnimatorConditionMode.If, 0, parameterName);
            
            EditorUtility.SetDirty(animatorController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return animatorController;
        }

        private static void UpdateToggleAnimatorController(AnimatorController animatorController, string groupName)
        {
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

            var offState = FindOrCreateState(layer.stateMachine, $"Group_{groupName}_off", animatorController);
            var onState = FindOrCreateState(layer.stateMachine, $"Group_{groupName}_on", animatorController);

            LinkAnimationClipToState(offState, groupName, "off");
            LinkAnimationClipToState(onState, groupName, "on");

            CreateOrUpdateTransition(layer.stateMachine, offState, layerName, false);
            CreateOrUpdateTransition(layer.stateMachine, onState, layerName, true);
            
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
                    mode = conditionValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
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
            var clipName = $"Group_{groupName}_{clipSuffix}";
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
    }
}
#endif