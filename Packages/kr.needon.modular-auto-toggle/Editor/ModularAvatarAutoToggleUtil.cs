#if UNITY_EDITOR
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

namespace Editor
{
    public abstract class AutoToggleCreator
    {
        private const string FolderPath = "Assets/Hirami/Toggle";
        private const string SettingPath = "Assets/Hirami/Toggle/setting.json";
        private static bool _toggleSaved = true;
        private static bool _toggleReverse;
        private static string _toggleMenuName = "Toggles";
        private static string _componentName;

        // 덮어쓰기 관련 플래그 (알럿이 중복되지 않도록)
        private static bool _overwritePrompted;
        private static bool _shouldOverwriteAll;

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
                _componentName = EditorPrefs.GetString("AutoToggleCreator_componentName");
            }
        }

        [MenuItem("GameObject/Create Toggle Items", false, 0)]
        private static void CreateToggleItems()
        {
            // 덮어쓰기 관련 플래그 초기화
            _overwritePrompted = false;
            _shouldOverwriteAll = false;

            // 클래스 초기화 시 componentName을 불러옴
            LoadComponentNameFromEditorPrefs();

            GameObject[] selectedObjects = Selection.gameObjects;
            GameObject rootObject = selectedObjects[0].transform.root.gameObject;
            string targetFolder = FolderPath + "/" + rootObject.name;
            Debug.Log("targetFolderPath :: " + targetFolder);

            // targetFolder가 없으면 componentName을 null로 설정하여 다시 입력을 받도록 함
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                _componentName = null;
            }

            // componentName이 null이거나 비어있으면 입력 창을 띄움
            if (string.IsNullOrEmpty(_componentName))
            {
                _componentName = ComponentNameWindow.OpenComponentNameDialog();
                if (string.IsNullOrEmpty(_componentName))
                {
                    Debug.LogWarning("No name entered for the toggle item. Operation cancelled.");
                    return; // 사용자가 이름을 입력하지 않으면 중단
                }

                // componentName을 설정할 때마다 저장
                SaveComponentNameToEditorPrefs(_componentName);
            }

            // 사용자 입력값을 ReadToggleMenuNameSetting에 설정
            SetToggleMenuNameSetting(_componentName);

            if (selectedObjects.Length <= 0)
            {
                Debug.LogError(
                    "The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 오브젝트들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.");
                EditorUtility.DisplayDialog("Error",
                    "The selected GameObjects must be part of an avatar with a VRC Avatar Descriptor.\n선택된 오브젝트들은 VRC 아바타 디스크립터를 가진 아바타의 일부여야 합니다.",
                    "OK");
                return;
            }

            if (rootObject == null)
            {
                Debug.LogError("The selected GameObject has no parent.\n선택한 오브젝트에 부모 오브젝트가 없습니다.");
                EditorUtility.DisplayDialog("Error", "The selected GameObject has no parent.", "OK");
                return;
            }

            ReadSetting();

            targetFolder = FolderPath + "/" + rootObject.name;

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
                // CreateToggleObject의 반환값이 true일 때만 성공 메시지 출력
                bool created = CreateToggleObject(selectedObjects, rootObject, targetFolder);

                EditorUtility.ClearProgressBar();
                if (created)
                {
                    EditorUtility.DisplayDialog("Toggle Items Creation",
                        "All toggle items have been created successfully.\n\n모든 토글 아이템이 성공적으로 생성되었습니다.",
                        "OK");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            };
        }

        private static void SetToggleMenuNameSetting(string name)
        {
            string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
            ToggleSettings settings;
            if (File.Exists(jsonFilePath))
            {
                string json = File.ReadAllText(jsonFilePath);
                settings = JsonUtility.FromJson<ToggleSettings>(json);
            }
            else
            {
                settings = new ToggleSettings();
            }

            settings.toggleMenuName = name;
            string updatedJson = JsonUtility.ToJson(settings, true);
            File.WriteAllText(jsonFilePath, updatedJson);
            AssetDatabase.Refresh();

            // Save settings to EditorPrefs
            SaveSettingsToEditorPrefs(settings);
        }

        private static bool CreateToggleObject(GameObject[] items, GameObject rootObject, string targetFolder)
        {
            var toggleTransform = rootObject.transform.Find(_toggleMenuName);
            var toggleGameObject = toggleTransform ? toggleTransform.gameObject : null;
            string groupName = string.Join("_", items.Select(obj => obj.name));
            string paramName = Md5Hash(rootObject.name + "_" + groupName);
            int currentStep = 0, totalSteps = 6;

            UpdateProgressBar(currentStep++, totalSteps, "Initializing... / 초기화 중...");
            if (toggleGameObject == null || toggleGameObject.GetComponentsInChildren<ToggleConfig>().Length <= 0)
            {
                toggleGameObject = new GameObject(_toggleMenuName);
                toggleTransform = toggleGameObject.transform;
                Debug.Log($"[DEBUG] 새 토글 부모 오브젝트 '{toggleGameObject.name}' 생성됨. / New toggle parent object '{toggleGameObject.name}' created.");
            }
            else
            {
                Debug.Log($"[DEBUG] 기존 토글 부모 오브젝트 '{toggleGameObject.name}' 발견됨. / Existing toggle parent object '{toggleGameObject.name}' found.");
            }

            toggleTransform.SetParent(rootObject.transform, false);

            UpdateProgressBar(currentStep++, totalSteps, "Creating Toggle Object... / 토글 오브젝트 생성 중...");

            // 기존 토글 오브젝트가 존재하면 경고 메시지만 띄우고 반환 (추가 Alert은 뜨지 않음)
            Transform existingChild = toggleTransform.Find("Toggle_" + groupName);
            if (existingChild != null)
            {
                EditorUtility.DisplayDialog(
                    "토글 존재함 / Toggle Exists",
                    "기존 토글 오브젝트가 이미 존재합니다. 삭제한 후 다시 생성해주세요.\n\nAn existing toggle object already exists. Please delete it manually and then create a new one.",
                    "확인 / OK"
                );
                return false;
            }

            GameObject newObj = new GameObject("Toggle_" + groupName);
            newObj.transform.SetParent(toggleTransform, false);
            Undo.RegisterCreatedObjectUndo(newObj, $"Create {newObj.name}");
            Debug.Log($"[DEBUG] 새 토글 오브젝트 생성됨: '{newObj.name}' / New toggle object created: '{newObj.name}'");

            // ToggleItem 컴포넌트 추가 및 targetGameObjects 설정
            ToggleItem toggleItem = newObj.AddComponent<ToggleItem>();
            if (toggleItem == null)
            {
                Debug.LogError("[DEBUG] ToggleItem 컴포넌트 추가에 실패했습니다. / Failed to add ToggleItem component.");
            }
            else
            {
                Debug.Log("[DEBUG] ToggleItem 컴포넌트가 성공적으로 추가되었습니다. / ToggleItem component added successfully.");
            }

            // targetGameObjects 할당 전 변경 기록
            Undo.RecordObject(toggleItem, "Set targetGameObjects");
            if (toggleItem.targetGameObjects == null)
            {
                toggleItem.targetGameObjects = new System.Collections.Generic.List<GameObject>();
            }
            else
            {
                toggleItem.targetGameObjects.Clear();
            }

            toggleItem.targetGameObjects.AddRange(items);
            Debug.Log($"[DEBUG] ToggleItem의 targetGameObjects가 다음 오브젝트들로 할당되었습니다: {string.Join(", ", items.Select(obj => obj.name))} / ToggleItem's targetGameObjects assigned with: {string.Join(", ", items.Select(obj => obj.name))}");

            EditorUtility.SetDirty(toggleItem);

            UpdateProgressBar(currentStep++, totalSteps, "Configure Parameter... / 파라미터 구성 중...");
            ConfigureAvatarParameters(newObj, paramName);

            UpdateProgressBar(currentStep++, totalSteps, "Configure Menu... / 메뉴 구성 중...");
            ConfigureMenuItem(newObj, paramName);

            var mergeAnimator = toggleGameObject.GetComponent<ModularAvatarMergeAnimator>();

            UpdateProgressBar(currentStep++, totalSteps, "Configure MA Settings... / MA 설정 구성 중...");
            if (mergeAnimator == null)
            {
                toggleGameObject.AddComponent<ModularAvatarMenuInstaller>();
                ConfigureParentMenuItem(toggleGameObject);
                mergeAnimator = toggleGameObject.AddComponent<ModularAvatarMergeAnimator>();
                Debug.Log("[DEBUG] 토글 부모에 ModularAvatarMergeAnimator 컴포넌트가 추가되었습니다. / ModularAvatarMergeAnimator component added to toggle parent.");
            }
            else
            {
                Debug.Log("[DEBUG] 기존 ModularAvatarMergeAnimator 컴포넌트를 토글 부모에서 찾았습니다. / Existing ModularAvatarMergeAnimator component found on toggle parent.");
            }

            UpdateProgressBar(currentStep++, totalSteps, "Configure Animator... / 애니메이터 구성 중...");
            mergeAnimator.animator = ConfigureAnimator(items, rootObject, targetFolder, groupName, paramName);
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;

            UpdateProgressBar(currentStep, totalSteps, "Complete! / 완료!");
            return true;
        }

        private static AnimationClip RecordState(GameObject[] items, GameObject rootObject, string folderPath,
            string groupName, bool activation)
        {
            string stateName = activation ? "on" : "off";
            string clipName = $"{groupName}_" + Md5Hash(rootObject.name + "_" + groupName) + $"_{stateName}";
            string fullPath = $"{folderPath}/Toggle_{clipName}.anim";

            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
            if (existingClip != null)
            {
                if (!_overwritePrompted)
                {
                    bool overwrite = EditorUtility.DisplayDialog(
                        "Animation Clip Exists",
                        $"An animation clip already exists at '{fullPath}'. Do you want to overwrite it?",
                        "Overwrite",
                        "Cancel"
                    );
                    _shouldOverwriteAll = overwrite;
                    _overwritePrompted = true;
                }

                if (!_shouldOverwriteAll)
                {
                    return existingClip;
                }
                else
                {
                    // 기존 애니메이션 클립 삭제 후 덮어쓰기
                    AssetDatabase.DeleteAsset(fullPath);
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

        // 나머지 기존 함수들...
        private static AnimatorController ConfigureAnimator(GameObject[] items, GameObject rootObject,
            string targetFolder, string groupName, string paramName)
        {
            string animatorPath = targetFolder + "/toggle_fx.controller";

            AnimatorController toggleAnimator = AssetDatabase.LoadAssetAtPath<AnimatorController>(animatorPath);
            if (toggleAnimator == null)
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
                    defaultBool = _toggleSaved
                });
            }

            AnimationClip onClip = RecordState(items, rootObject, targetFolder, groupName, true);
            AnimationClip offClip = RecordState(items, rootObject, targetFolder, groupName, false);

            AnimatorState onState = stateMachine.AddState("on");
            onState.motion = onClip;
            AnimatorState offState = stateMachine.AddState("off");
            offState.motion = offClip;

            stateMachine.defaultState = offState;

            AnimatorConditionMode conditionModeForOn =
                _toggleReverse ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If;
            AnimatorConditionMode conditionModeForOff =
                _toggleReverse ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;

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

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return toggleAnimator;
        }

        private static void ConfigureAvatarParameters(GameObject obj, string paramName)
        {
            var avatarParameters = obj.AddComponent<ModularAvatarParameters>();

            avatarParameters.parameters ??= new System.Collections.Generic.List<ParameterConfig>();

            if (avatarParameters.parameters.Any(p => p.nameOrPrefix == paramName)) return;

            avatarParameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = paramName,
                syncType = ParameterSyncType.Bool,
                defaultValue = 1,
                saved = _toggleSaved
            });
        }

        private static void ConfigureMenuItem(GameObject obj, string paramName)
        {
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName };
            menuItem.Control.icon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png");
        }

        private static void ConfigureParentMenuItem(GameObject obj)
        {
            obj.AddComponent<ToggleConfig>();
            obj.AddComponent<DeleteToggle>();
            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();

            menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            menuItem.MenuSource = SubmenuSource.Children;
            menuItem.Control.icon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png");
        }

        private static void ReadSetting()
        {
            ToggleSettings settings = File.Exists(SettingPath)
                ? JsonUtility.FromJson<ToggleSettings>(File.ReadAllText(SettingPath))
                : new ToggleSettings();
            _toggleSaved = settings.toggleSaved;
            _toggleReverse = settings.toggleReverse;
            _toggleMenuName = settings.toggleMenuName ?? "Toggles";
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

        private static void SaveSettingsToEditorPrefs(ToggleSettings settings)
        {
            EditorPrefs.SetBool("AutoToggleCreator_toggleSaved", settings.toggleSaved);
            EditorPrefs.SetBool("AutoToggleCreator_toggleReverse", settings.toggleReverse);
            EditorPrefs.SetString("AutoToggleCreator_toggleMenuName", settings.toggleMenuName);
        }

        private static void LoadSettingsFromEditorPrefs()
        {
            if (EditorPrefs.HasKey("AutoToggleCreator_toggleSaved"))
            {
                _toggleSaved = EditorPrefs.GetBool("AutoToggleCreator_toggleSaved");
            }

            if (EditorPrefs.HasKey("AutoToggleCreator_toggleReverse"))
            {
                _toggleReverse = EditorPrefs.GetBool("AutoToggleCreator_toggleReverse");
            }

            if (EditorPrefs.HasKey("AutoToggleCreator_toggleMenuName"))
            {
                _toggleMenuName = EditorPrefs.GetString("AutoToggleCreator_toggleMenuName");
            }
        }
    }
}

[System.Serializable]
public class ToggleSettings
{
    public bool toggleSaved = true;
    public bool toggleReverse;
    public string toggleMenuName;
}

#endif