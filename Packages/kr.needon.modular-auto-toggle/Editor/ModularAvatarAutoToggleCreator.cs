#if UNITY_EDITOR
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

//v1.0.51
namespace Hirami.Scripts
{
    public abstract class TogglePrefabCreator
    {
        private static AnimatorController _globalFxAnimator;
        private static string _selectGameObejct;



        [MenuItem("GameObject/Add Toggle Items", false, 0)]
        private static void CreateToggleItemsInHierarchy()
        {
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
                EditorUtility.DisplayDialog("Toggle Items Creation", "All toggle items have been created successfully.","OK");

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

            var toggleName = selectedPrefab.name + "_toggle";
            var newToggleGameObject = new GameObject(toggleName);
            newToggleGameObject.transform.SetParent(togglesGameObject.transform, false);

            AddComponentsToToggleItem(newToggleGameObject, selectedPrefab.name);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }


        private static void AddComponentsToToggleItem(GameObject obj, string prefabName)
        {
            var toggleName = prefabName + "_toggle";

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

            RecordState(toggleName, true);
            RecordState(toggleName, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (_globalFxAnimator == null)
            {
                _globalFxAnimator = CreateToggleAnimatorController(toggleName);
                mergeAnimator.animator = _globalFxAnimator;
            }
            else
            {
                UpdateToggleAnimatorController(_globalFxAnimator, toggleName);
            }

            mergeAnimator.animator = _globalFxAnimator;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;
            
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
                nameOrPrefix = parameterName,
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


        private static AnimatorController CreateToggleAnimatorController(string toggleItemName)
        {
            string controllerPath = $"Assets/Hirami/Toggle/toogle_fx.controller";
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
                    name = toggleItemName + " State Machine"
                };
                AssetDatabase.AddObjectToAsset(stateMachine, animatorController);


                var newLayer = new AnimatorControllerLayer
                {
                    name = toggleItemName,
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                };
                animatorController.AddLayer(newLayer);

                var parameter = new AnimatorControllerParameter
                {
                    name = toggleItemName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                };
                animatorController.AddParameter(parameter);
                
                var idleState = stateMachine.AddState("Idle");
                var toggleOnState = stateMachine.AddState(toggleItemName + "_On");
                
                LinkAnimationClipToState(idleState, toggleItemName, "off");
                LinkAnimationClipToState(toggleOnState, toggleItemName, "on");

                CreateStateTransition(stateMachine, toggleOnState, parameter.name, false);
                CreateStateTransition(stateMachine, idleState, parameter.name, true);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return animatorController;
        }

        private static void UpdateToggleAnimatorController(AnimatorController animatorController, string toggleItemName)
        {
            var layerName = toggleItemName;
            var layer = animatorController.layers.FirstOrDefault(l => l.name == layerName);
            if (layer == null)
            {
                var stateMachine = new AnimatorStateMachine
                {
                    name = layerName + " State Machine"
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
                    name = toggleItemName,
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

            
            var onState = FindOrCreateState(layer.stateMachine, toggleItemName + "_on", animatorController);
            var offState = FindOrCreateState(layer.stateMachine, toggleItemName + "_off", animatorController);
            
            LinkAnimationClipToState(onState, toggleItemName, "on");
            LinkAnimationClipToState(offState, toggleItemName, "off");

            CreateStateTransition(layer.stateMachine, onState, parameter.name, false);
            CreateStateTransition(layer.stateMachine, offState, parameter.name, true);

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


        private static void RecordState(string toggleItemName, bool activation)
        {
            var stateName = activation ? "on" : "off";
            var clipName = $"{toggleItemName}_{stateName}";
            var folderPath = "Assets/Hirami/Toggle";
            var fullPath = $"{folderPath}/{clipName}.anim";

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
    }
}

public class HiramiAssetPostprocessor : AssetPostprocessor
{
    // 에셋이 임포트, 삭제, 이동될 때 호출됩니다.
    static void OnPostprocessAllAssets(
        string[] importedAssets, 
        string[] deletedAssets, 
        string[] movedAssets, 
        string[] movedFromAssetPaths)
    {

        string folderPath = "Assets/Hirami";


        if (!AssetDatabase.IsValidFolder(folderPath))
        {

            AssetDatabase.CreateFolder("Assets", "Hirami");
        }
    }
}
#endif