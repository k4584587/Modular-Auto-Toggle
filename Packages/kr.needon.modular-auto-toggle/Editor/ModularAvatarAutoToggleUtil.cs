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

//v1.0.71
namespace Editor
{
    public abstract class AutoToggleCreator
    {
        private static readonly string folderPath = "Assets/Hirami/Toggle";
        private static readonly string settingPath = "Assets/Hirami/Toggle/setting.json";
        private static bool toggleReverse = false;
        private static string toggleMenuName = "Toggles";

       [MenuItem("GameObject/Create Toggle Items", false, 0)]
        private static void CreateToggleItems()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            GameObject rootObject = null;
            string targetFolder = "";
            
            if (selectedObjects.Length <= 0)
            {
                Debug.LogError("The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 오브젝트들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.");
                EditorUtility.DisplayDialog("Error", "The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 오브젝트들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.", "OK");
                return;
            }

            rootObject = selectedObjects[0].transform.root.gameObject;
            
            if (!rootObject)
            {
                Debug.LogError("The selected GameObject has no parent.\n선택한 오브젝트에 부모 오브젝트가 없습니다.");
                EditorUtility.DisplayDialog("Error", "The selected GameObject has no parent.", "OK");
                return;
            }

            ReadSetting();
            
            targetFolder = folderPath + "/" + rootObject.name;

            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                string[] folders = targetFolder.Split('/');
                string parentFolder = folders[0];

                for (int i = 1; i < folders.Length; i++)
                {
                    string tmpFolder = parentFolder + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(tmpFolder))
                    {
                        AssetDatabase.CreateFolder(parentFolder, folders[i]);
                        Debug.Log(tmpFolder + " folder has been created.");
                    }

                    parentFolder = tmpFolder;
                }
            }

            Debug.Log("ToggleName :: " + rootObject.name);

            EditorApplication.delayCall = null;
            EditorApplication.delayCall += () =>
            {
                // GameObject 생성
                CreateToggleObject(selectedObjects, rootObject, targetFolder);

                // 로딩 바 종료
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Toggle Items Creation", "All toggle items have been created successfully.\n\n모든 토글 아이템이 성공적으로 생성되었습니다.",
                    "OK");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }
        
        private static void CreateToggleObject(GameObject[] items, GameObject rootObject, string targetFolder)
        {
            var toggleTransform = rootObject.transform.Find(toggleMenuName);
            var toggleGameObject = !toggleTransform ? null : toggleTransform.gameObject;
            string groupName = string.Join("_", items.Select(obj => obj.name));
            string paramName =  Md5Hash(rootObject.name + "_" + groupName);
            int currentStep = 0, totalSteps = 6;
            
            UpdateProgressBar(currentStep++, totalSteps, "Initializing...");
            if(!toggleGameObject || toggleGameObject.GetComponentsInChildren<ToggleConfig>().Length <= 0)
            {
                toggleGameObject = new GameObject(toggleMenuName);
                toggleTransform = toggleGameObject.transform;
            }
            toggleTransform.SetParent(rootObject.transform, false);

            UpdateProgressBar(currentStep++, totalSteps, "Creating Toggle Object...");
            GameObject newObj = new GameObject("Toggle_" + groupName);
            newObj.transform.SetParent(toggleTransform, false);

            UpdateProgressBar(currentStep++, totalSteps, "Configure Parameter...");
            ConfigureAvatarParameters(newObj, paramName);

            UpdateProgressBar(currentStep++, totalSteps, "Configure Menu...");
            ConfigureMenuItem(newObj, paramName);

            newObj.AddComponent<ToggleItem>();

            var mergeAnimator = toggleGameObject.GetComponent<ModularAvatarMergeAnimator>();

            UpdateProgressBar(currentStep++, totalSteps, "Configure MA Settings...");
            
            if (!mergeAnimator){
                toggleGameObject.AddComponent<ModularAvatarMenuInstaller>();
                ConfigureParentMenuItem(toggleGameObject);
                mergeAnimator = toggleGameObject.AddComponent<ModularAvatarMergeAnimator>();
            }

            UpdateProgressBar(currentStep++, totalSteps, "Configure Animator...");
            mergeAnimator.animator = ConfigureAnimator(items, rootObject, targetFolder, groupName, paramName);
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;
            
            UpdateProgressBar(currentStep++, totalSteps, "Complete!");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        private static AnimatorController ConfigureAnimator(GameObject[] items, GameObject rootObject, string targetFolder, string groupName, string paramName){
            string animatorPath = targetFolder + "/toggle_fx.controller";
            
            AnimatorController toggleAnimator = null;
            
            if((toggleAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>(animatorPath)) == null){
                toggleAnimator = AnimatorController.CreateAnimatorControllerAtPath(animatorPath);         
                toggleAnimator.RemoveLayer(0); 
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = paramName,
                hideFlags = HideFlags.HideInHierarchy
            };

            if(toggleAnimator.layers.All(l => l.name != paramName)){
                AssetDatabase.AddObjectToAsset(stateMachine, toggleAnimator);
                toggleAnimator.AddLayer(new AnimatorControllerLayer
                {
                    name = paramName,
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                });
            }

            if (toggleAnimator.parameters.All(p => p.name != paramName)){
                toggleAnimator.AddParameter(new AnimatorControllerParameter
                {
                    name = paramName, 
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = true
                });
            }

            AnimationClip onClip = RecordState(items, rootObject, targetFolder, groupName, true);
            AnimationClip offClip = RecordState(items, rootObject, targetFolder, groupName, false);

            AnimatorState onState = stateMachine.AddState("on");
            onState.motion = onClip;
            AnimatorState offState = stateMachine.AddState("off");
            offState.motion = offClip;

            stateMachine.defaultState = offState;

            AnimatorConditionMode conditionModeForOn = toggleReverse ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If;
            AnimatorConditionMode conditionModeForOff = toggleReverse ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;

            AnimatorStateTransition transitionToOn = offState.AddTransition(onState);
            transitionToOn.hasExitTime = false;
            transitionToOn.exitTime = 0f;
            transitionToOn.duration = 0f;
            transitionToOn.AddCondition(conditionModeForOn, 0, paramName);

            AnimatorStateTransition transitionToOff = onState.AddTransition(offState);
            transitionToOff.hasExitTime = false;
            transitionToOff.exitTime = 0f;
            transitionToOff.duration = 0f;
            transitionToOff.AddCondition(conditionModeForOff, 0, paramName);

            // 변경 사항 저장
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return toggleAnimator;
        }

        private static AnimationClip RecordState(GameObject[] items, GameObject rootObject, string folderPath, string groupName, bool activation)
        {
            string stateName = activation ? "on" : "off";
            string clipName = $"{groupName}_" + Md5Hash(rootObject.name + "_" + groupName) + $"_{stateName}";
            string fullPath = $"{folderPath}/Toggle_{clipName}.anim";
            
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
                    return existingClip;
                }
            }

            var clip = new AnimationClip { name = clipName };
            var curve = new AnimationCurve();
            curve.AddKey(0f, activation ? 1f : 0f);
            
            foreach (GameObject obj in items)
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(obj.transform, rootObject.transform), typeof(GameObject), "m_IsActive"), curve);
            }

            AssetDatabase.CreateAsset(clip, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return clip;
        }


        // Modular Avatar
        private static void ConfigureAvatarParameters(GameObject obj, string paramName)
        {
            var avatarParameters = obj.AddComponent<ModularAvatarParameters>();

            if (avatarParameters.parameters.Any(p => p.nameOrPrefix == paramName)) return;
            
            avatarParameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = paramName,
                syncType = ParameterSyncType.Bool,
                defaultValue = 1,
                saved = true
            });
        }

        private static void ConfigureMenuItem(GameObject obj, string paramName)
        {
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName }; //모듈러 파라미터 이름 설정
            menuItem.Control.icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png");; // 메뉴 아이콘 설정
        }

        private static void ConfigureParentMenuItem(GameObject obj)
        {
            obj.AddComponent<ToggleConfig>();
            obj.AddComponent<DeleteToggle>();
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.Children;
            menuItem.Control.icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png");; // 메뉴 아이콘 설정
        }


        // Settings

        private static void ReadSetting()
        {
            ToggleSettings settings = File.Exists(settingPath) ? JsonUtility.FromJson<ToggleSettings>(File.ReadAllText(settingPath)) : new ToggleSettings();
            toggleReverse = settings.toggleReverse;
            toggleMenuName = settings.toggleMenuName ?? "Toggles";
            AssetDatabase.Refresh();
        }

        private static string Md5Hash(string input)
        {
            MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(input));

            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes){
                sb.Append(b.ToString("X2"));
            }
            
            return sb.ToString();
        }

        private static void UpdateProgressBar(int currentStep, int totalSteps, string message)
        {
            float progress = (float)currentStep / totalSteps;
            EditorUtility.DisplayProgressBar("Creating Toggle Items", message, progress);
        }
        

    }
}

[System.Serializable]
public class ToggleSettings
{
    public bool toggleReverse;
    public string toggleMenuName;
}

#endif