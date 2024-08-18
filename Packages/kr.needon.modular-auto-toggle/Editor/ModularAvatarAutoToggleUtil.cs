#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using nadena.dev.modular_avatar.core;
using ToggleTool.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using ToggleTool.Global;
using ToggleTool.Utils;
using ToggleTool.Models;

//v1.0.71
namespace ToggleTool.Editor
{
    public abstract class AutoToggleCreator
    {
        private static bool toggleSaved = true;
        private static bool toggleReverse = false;
        private static string toggleMenuName;
        private static string componentName = null;

        // componentName 변수를 EditorPrefs에 저장하는 함수
        private static void SaveComponentNameToEditorPrefs(string name)
        {
            EditorPrefs.SetString("AutoToggleCreator_componentName", name);
        }

        // componentName 변수를 EditorPrefs에서 불러오는 함수
        private static void LoadComponentNameFromEditorPrefs()
        {
            if (EditorPrefs.HasKey("AutoToggleCreator_componentName"))
            {
                componentName = EditorPrefs.GetString("AutoToggleCreator_componentName");
            }
        }

        [MenuItem("GameObject/Create Toggle Items", false, 0)]
        private static void CreateToggleItems()
        {
            // 클래스 초기화 시 componentName을 불러옴
            LoadComponentNameFromEditorPrefs();

            GameObject[] selectedObjects = Selection.gameObjects;
            GameObject rootObject = null;
            rootObject = selectedObjects[0].transform.root.gameObject;
            string targetFolder = FilePaths.TARGET_FOLDER_PATH + "/" + rootObject.name;
            Debug.Log("targetFolderPath :: " + targetFolder);

            // targetFolder가 없으면 componentName을 null로 설정하여 다시 입력을 받도록 함
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                componentName = null;
            }

            // componentName이 null이거나 비어있으면 입력 창을 띄움
            if (string.IsNullOrEmpty(componentName))
            {
                componentName = ComponentNameWindow.OpenComponentNameDialog();
                if (string.IsNullOrEmpty(componentName))
                {
                    Debug.LogWarning("No name entered for the toggle item. Operation cancelled.");
                    return; // 사용자가 이름을 입력하지 않으면 중단
                }

                // componentName을 설정할 때마다 저장
                SaveComponentNameToEditorPrefs(componentName);
            }

            // 사용자 입력값을 ReadToggleMenuNameSetting에 설정
            SetToggleMenuNameSetting(componentName);

            if (selectedObjects.Length <= 0)
            {
                Debug.LogError(
                    "The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 오브젝트들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.");
                EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_ERROR,
                    "The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 오브젝트들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.",
                    Messages.DIALOG_BUTTON_OK);
                return;
            }

            if (!rootObject)
            {
                Debug.LogError("The selected GameObject has no parent.\n선택한 오브젝트에 부모 오브젝트가 없습니다.");
                EditorUtility.DisplayDialog(Messages.DIALOG_TITLE_ERROR, "The selected GameObject has no parent.", Messages.DIALOG_BUTTON_OK);
                return;
            }

            ReadSetting();

            targetFolder = FilePaths.TARGET_FOLDER_PATH + "/" + rootObject.name;

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
                EditorUtility.DisplayDialog("Toggle Items Creation",
                    "All toggle items have been created successfully.\n\n모든 토글 아이템이 성공적으로 생성되었습니다.",
                    Messages.DIALOG_BUTTON_OK);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        private static void SetToggleMenuNameSetting(string name)
        {
            ToggleConfigModel settings;
            if (File.Exists(FilePaths.JSON_FILE_PATH))
            {
                string json = File.ReadAllText(FilePaths.JSON_FILE_PATH);
                settings = JsonUtility.FromJson<ToggleConfigModel>(json);
            }
            else
            {
                settings = new ToggleConfigModel();
            }

            settings.toggleMenuName = name;
            string updatedJson = JsonUtility.ToJson(settings, true);
            File.WriteAllText(FilePaths.JSON_FILE_PATH, updatedJson);
            AssetDatabase.Refresh();

            // Save settings to EditorPrefs
            SaveSettingsToEditorPrefs(settings);
        }

        private static void CreateToggleObject(GameObject[] items, GameObject rootObject, string targetFolder)
        {
            var toggleTransform = rootObject.transform.Find(toggleMenuName);
            var toggleGameObject = !toggleTransform ? null : toggleTransform.gameObject;
            string groupName = string.Join("_", items.Select(obj => obj.name));
            string paramName = Md5Hash(rootObject.name + "_" + groupName);
            int currentStep = 0, totalSteps = 6;

            UpdateProgressBar(currentStep++, totalSteps, "Initializing...");
            if (!toggleGameObject || toggleGameObject.GetComponentsInChildren<ToggleConfig>().Length <= 0)
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

            if (!mergeAnimator)
            {
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

        private static AnimatorController ConfigureAnimator(GameObject[] items, GameObject rootObject,
            string targetFolder, string groupName, string paramName)
        {
            string animatorPath = targetFolder + "/" + FilePaths.ANIMATOR_FILE_NAME;

            AnimatorController toggleAnimator = null;

            if ((toggleAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>(animatorPath)) == null)
            {
                toggleAnimator = AnimatorController.CreateAnimatorControllerAtPath(animatorPath);
                toggleAnimator.RemoveLayer(0);
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = paramName,
                hideFlags = HideFlags.HideInHierarchy
            };

            if (toggleAnimator.layers.All(l => l.name != paramName))
            {
                AssetDatabase.AddObjectToAsset(stateMachine, toggleAnimator);
                toggleAnimator.AddLayer(new AnimatorControllerLayer
                {
                    name = paramName,
                    stateMachine = stateMachine,
                    defaultWeight = 1f
                });
            }

            if (toggleAnimator.parameters.All(p => p.name != paramName))
            {
                toggleAnimator.AddParameter(new AnimatorControllerParameter
                {
                    name = paramName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = toggleSaved
                });
            }

            AnimationClip onClip = RecordState(items, rootObject, targetFolder, groupName, true);
            AnimationClip offClip = RecordState(items, rootObject, targetFolder, groupName, false);

            AnimatorState onState = stateMachine.AddState(Messages.STATE_NAME_ON);
            onState.motion = onClip;
            AnimatorState offState = stateMachine.AddState(Messages.STATE_NAME_OFF);
            offState.motion = offClip;

            stateMachine.defaultState = offState;

            AnimatorConditionMode conditionModeForOn =
                toggleReverse ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If;
            AnimatorConditionMode conditionModeForOff =
                toggleReverse ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;

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


        private static AnimationClip RecordState(GameObject[] items, GameObject rootObject, string folderPath,
            string groupName, bool activation)
        {
            string stateName = activation ? Messages.STATE_NAME_ON : Messages.STATE_NAME_OFF;
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
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(
                        AnimationUtility.CalculateTransformPath(obj.transform, rootObject.transform),
                        typeof(GameObject), "m_IsActive"), curve);
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
                saved = toggleSaved
            });
        }

        private static void ConfigureMenuItem(GameObject obj, string paramName)
        {
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName }; //모듈러 파라미터 이름 설정
            menuItem.Control.icon = ImageLoader.instance["ToggleON"].iconTexture; // 메뉴 아이콘 설정
        }
        
        private static void ConfigureParentMenuItem(GameObject obj)
        {
            obj.AddComponent<ToggleConfig>();
            obj.AddComponent<DeleteToggle>();
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.Children;
            menuItem.Control.icon = ImageLoader.instance["ToggleON"].iconTexture; // 메뉴 아이콘 설정
        }

        // Settings

        private static void ReadSetting()
        {
            ToggleConfigModel settings = File.Exists(FilePaths.JSON_FILE_PATH)
                ? JsonUtility.FromJson<ToggleConfigModel>(File.ReadAllText(FilePaths.JSON_FILE_PATH))
                : new ToggleConfigModel();
            toggleSaved = settings.toggleSaved;
            toggleReverse = settings.toggleReverse;
            toggleMenuName = settings.toggleMenuName ?? Components.DEFAULT_COMPONENT_NAME;
            AssetDatabase.Refresh();

            // Load settings from EditorPrefs
            LoadSettingsFromEditorPrefs();
        }

        private static string Md5Hash(string input)
        {
            MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(input));

            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }

        private static void UpdateProgressBar(int currentStep, int totalSteps, string message)
        {
            float progress = (float)currentStep / totalSteps;
            EditorUtility.DisplayProgressBar("Creating Toggle Items", message, progress);
        }

        private static void SaveSettingsToEditorPrefs(ToggleConfigModel settings)
        {
            EditorPrefs.SetBool("AutoToggleCreator_toggleSaved", settings.toggleSaved);
            EditorPrefs.SetBool("AutoToggleCreator_toggleReverse", settings.toggleReverse);
            EditorPrefs.SetString("AutoToggleCreator_toggleMenuName", settings.toggleMenuName);
        }

        private static void LoadSettingsFromEditorPrefs()
        {
            if (EditorPrefs.HasKey("AutoToggleCreator_toggleSaved"))
            {
                toggleSaved = EditorPrefs.GetBool("AutoToggleCreator_toggleSaved");
            }

            if (EditorPrefs.HasKey("AutoToggleCreator_toggleReverse"))
            {
                toggleReverse = EditorPrefs.GetBool("AutoToggleCreator_toggleReverse");
            }

            if (EditorPrefs.HasKey("AutoToggleCreator_toggleMenuName"))
            {
                toggleMenuName = EditorPrefs.GetString("AutoToggleCreator_toggleMenuName");
            }
        }
    }
}
#endif