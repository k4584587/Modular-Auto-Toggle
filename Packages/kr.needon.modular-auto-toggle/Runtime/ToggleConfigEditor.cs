#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using nadena.dev.modular_avatar.core;
using Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

[CustomEditor(typeof(ToggleConfig))]
[InitializeOnLoad]
public class ToggleConfigEditor : Editor
{
    private bool _toggleSaved;
    private bool _toggleReverse;

    private const string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
    private Texture2D _icon;
    private ToggleConfig _toggleConfig;

    private static string toggleMenuName = "Toggles";

    private const string iconFilePath = "Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png";

    static ToggleConfigEditor()
    {
        EditorApplication.update += UpdateIcons;
    }

    private static void UpdateIcons()
    {
        var toggleConfigs = Resources.FindObjectsOfTypeAll<ToggleConfig>();
        foreach (var toggleConfig in toggleConfigs)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconFilePath);
            SetUnityObjectIcon(toggleConfig, icon);
        }
        EditorApplication.update -= UpdateIcons;
    }

    private void OnEnable()
    {
        _toggleConfig = (ToggleConfig)target;
        _toggleConfig._icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconFilePath);
        EditorApplication.update += SetIconImmediate;
        LoadSettings();
    }

    private void OnDisable()
    {
        EditorApplication.update -= SetIconImmediate;
    }

    private void SetIconImmediate()
    {
        SetUnityObjectIcon(_toggleConfig, _toggleConfig._icon);
        EditorApplication.update -= SetIconImmediate;
    }

    public override void OnInspectorGUI()
    {
        // toggleConfig
        SerializedProperty toggleConfigProp = serializedObject.FindProperty("toggleConfig");
        if (toggleConfigProp != null)
        {
            EditorGUILayout.PropertyField(toggleConfigProp.FindPropertyRelative("toggleSaved"), new GUIContent("Toggle Saved"));
            EditorGUILayout.PropertyField(toggleConfigProp.FindPropertyRelative("toggleReverse"), new GUIContent("Toggle Reverse"));
            EditorGUILayout.PropertyField(toggleConfigProp.FindPropertyRelative("toggleMenuName"), new GUIContent("Toggle Menu Name"));
        }
        else
        {
            EditorGUILayout.HelpBox("Cannot find 'toggleConfig' property.", MessageType.Error);
        }

        EditorGUILayout.Space();

        // 애니메이션 옵션 항목 삭제됨

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply"))
        {
            ApplySettings();
        }
        if (GUILayout.Button("Reset Settings"))
        {
            ResetSettings();
        }
        if (GUILayout.Button("Toggle Refresh"))
        {
            ToggleRefresh();
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 기존 fx 애니메이터를 삭제 후 새로 생성.
    /// </summary>
    private static AnimatorController ConfigureAnimator(GameObject[] items, GameObject rootObject, string targetFolder, string groupName, string paramName)
    {
        string animatorPath = targetFolder + "/toggle_fx.controller";
        if (File.Exists(animatorPath))
        {
            AssetDatabase.DeleteAsset(animatorPath);
            AssetDatabase.Refresh();
        }
        AnimatorController toggleAnimator = AnimatorController.CreateAnimatorControllerAtPath(animatorPath);
        if (toggleAnimator.layers.Length > 0)
        {
            toggleAnimator.RemoveLayer(0);
        }
        if (!toggleAnimator.parameters.Any(p => p.name == paramName))
        {
            AnimatorControllerParameter param = new AnimatorControllerParameter();
            param.name = paramName;
            param.type = AnimatorControllerParameterType.Bool;
            param.defaultBool = true;
            toggleAnimator.AddParameter(param);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return toggleAnimator;
    }

    /// <summary>
    /// Toggle Refresh: 기존 애니메이션/토글을 새로 갱신
    /// on/off 애니메이션 파일은 삭제하지 않고 그대로 둔다.
    /// </summary>
    private void ToggleRefresh()
    {
        const string baseFolder = "Assets/Hirami/Toggle";
        GameObject rootObject = _toggleConfig.gameObject.transform.root.gameObject;

        string menuName = _toggleConfig.toggleConfig.toggleMenuName;
        Transform toggleTransform = rootObject.transform.Find(menuName);
        if (toggleTransform == null)
        {
            EditorUtility.DisplayDialog(
                "Refresh Error / 갱신 오류",
                $"{menuName} group could not be found in the avatar.\n아바타에서 {menuName} 그룹을 찾을 수 없습니다.",
                "OK");
            return;
        }
        GameObject toggleGroup = toggleTransform.gameObject;
        ToggleItem[] toggleItems = toggleGroup.GetComponentsInChildren<ToggleItem>();
        if (toggleItems.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Refresh Error / 갱신 오류",
                "No toggle items found to refresh.\n갱신할 토글 아이템이 없습니다.",
                "OK");
            return;
        }

        string currentAvatarName = rootObject.name;
        string targetFolder = baseFolder + "/" + currentAvatarName;

        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
            AssetDatabase.Refresh();
        }

        // MergeAnimator
        ModularAvatarMergeAnimator mergeAnimator = toggleGroup.GetComponent<ModularAvatarMergeAnimator>();
        if (mergeAnimator == null)
        {
            toggleGroup.AddComponent<ModularAvatarMergeAnimator>();
            ConfigureParentMenuItem(toggleGroup);
            mergeAnimator = toggleGroup.GetComponent<ModularAvatarMergeAnimator>();
        }

        // 첫 번째 아이템 기준으로 fx Animator를 새로 생성
        {
            ToggleItem firstItem = toggleItems[0];
            string groupName = firstItem.gameObject.name;
            string paramName = GetParameterNameFromToggle(firstItem.gameObject);
            if (string.IsNullOrEmpty(paramName))
            {
                paramName = Md5Hash(rootObject.name + "_" + groupName);
            }

            GameObject[] itemsToRecord;
            if (firstItem.targetGameObjects != null && firstItem.targetGameObjects.Count > 0)
                itemsToRecord = firstItem.targetGameObjects.ToArray();
            else
                itemsToRecord = new GameObject[] { firstItem.gameObject };

            AnimatorController fxAnimator = ConfigureAnimator(itemsToRecord, rootObject, targetFolder, groupName, paramName);
            mergeAnimator.animator = fxAnimator;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = true;
            mergeAnimator.deleteAttachedAnimator = true;
        }

        AnimatorController animatorController = mergeAnimator.animator as AnimatorController;
        if (animatorController == null)
        {
            Debug.LogWarning("AnimatorController not found or could not be converted.");
            return;
        }

        foreach (ToggleItem item in toggleItems)
        {
            GameObject toggleObj = item.gameObject;
            string groupName = toggleObj.name;
            string paramName = GetParameterNameFromToggle(toggleObj);
            if (string.IsNullOrEmpty(paramName))
            {
                paramName = Md5Hash(rootObject.name + "_" + groupName);
            }
            GameObject[] itemsToRecord;
            if (item.targetGameObjects != null && item.targetGameObjects.Count > 0)
                itemsToRecord = item.targetGameObjects.ToArray();
            else
                itemsToRecord = new GameObject[] { toggleObj };

            // on/off anim 파일 생성(활성/비활성)
            // ※ 삭제하지 않음
            AnimationClip onClip = ForceRecordState(itemsToRecord, rootObject, targetFolder, groupName, paramName, true);
            AnimationClip offClip = ForceRecordState(itemsToRecord, rootObject, targetFolder, groupName, paramName, false);

            RecreateToggleAnimationForToggle(
                toggleObj,
                rootObject,
                targetFolder,
                groupName,
                paramName,
                _toggleConfig.toggleConfig.toggleReverse,
                animatorController,
                onClip,
                offClip
            );
        }

        // 블렌드 쉐이프 재갱신
        foreach (ToggleItem item in toggleItems)
        {
            ToggleItemEditor.ApplyBlendShapeToItem(item);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Refresh Toggle Items",
            "Existing toggle items have been refreshed.\n기존 토글 아이템이 재갱신되었습니다.",
            "OK");
    }

    /// <summary>
    /// ForceRecordState: 기존 애니메이션 클립을 덮어쓰고 새 클립을 생성(m_IsActive 기록)
    /// </summary>
    private static AnimationClip ForceRecordState(
        GameObject[] items,
        GameObject rootObject,
        string folderPath,
        string groupName,
        string paramName,
        bool activation
    )
    {
        string stateName = activation ? "on" : "off";
        string concatenatedNames = (items != null && items.Length > 0)
            ? string.Join("_", items.Select(obj => obj.name))
            : groupName;
        string hash = Md5Hash(rootObject.name + "_" + concatenatedNames);
        string clipName = $"Toggle_{concatenatedNames}_{hash}_{stateName}";
        string fullPath = $"{folderPath}/{clipName}.anim";

        var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
        if (existingClip != null)
        {
            AssetDatabase.DeleteAsset(fullPath);
            AssetDatabase.Refresh();
        }

        AnimationClip clip = new AnimationClip { name = clipName };
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, activation ? 1f : 0f);

        foreach (GameObject obj in items)
        {
            string path = AnimationUtility.CalculateTransformPath(obj.transform, rootObject.transform);
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(path, typeof(GameObject), "m_IsActive");
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        AssetDatabase.CreateAsset(clip, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return clip;
    }

   /// <summary>
/// RecreateToggleAnimationForToggle: on/off 상태머신 갱신
/// </summary>
private void RecreateToggleAnimationForToggle(
    GameObject toggleObj,
    GameObject rootObject,
    string targetFolder,
    string groupName,
    string paramName,
    bool toggleReverse,
    AnimatorController animatorController,
    AnimationClip onClip,
    AnimationClip offClip
)
{
    if (animatorController == null) return;
    
    // 해당 파라미터가 없으면 생성
    if (!animatorController.parameters.Any(p => p.name == paramName))
    {
        AnimatorControllerParameter newParam = new AnimatorControllerParameter();
        newParam.name = paramName;
        newParam.type = AnimatorControllerParameterType.Bool;
        newParam.defaultBool = true;
        animatorController.AddParameter(newParam);
    }

    AnimatorControllerLayer existingLayer = animatorController.layers.FirstOrDefault(l => l.name == paramName);
    AnimatorStateMachine sm;
    if (existingLayer != null)
    {
        sm = existingLayer.stateMachine;
        if (sm == null)
        {
            sm = new AnimatorStateMachine();
            sm.name = paramName;
            sm.hideFlags = HideFlags.HideInHierarchy;
            existingLayer.stateMachine = sm;
            AssetDatabase.AddObjectToAsset(sm, animatorController);
        }
    }
    else
    {
        sm = new AnimatorStateMachine();
        sm.name = paramName;
        sm.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(sm, animatorController);
        AnimatorControllerLayer newLayer = new AnimatorControllerLayer();
        newLayer.name = paramName;
        newLayer.stateMachine = sm;
        newLayer.defaultWeight = 1f;
        animatorController.AddLayer(newLayer);
    }

    // 기존 on/off 상태 스테이트 생성 또는 할당
    AnimatorState onState = sm.states.FirstOrDefault(s => s.state.name == "on").state;
    AnimatorState offState = sm.states.FirstOrDefault(s => s.state.name == "off").state;

    if (onClip != null)
    {
        if (onState == null) onState = sm.AddState("on");
        onState.motion = onClip;
    }
    if (offClip != null)
    {
        if (offState == null) offState = sm.AddState("off");
        offState.motion = offClip;
    }

    // 기본 상태: offState 우선
    if (offState != null)
    {
        sm.defaultState = offState;
    }
    else if (onState != null)
    {
        sm.defaultState = onState;
    }

    // 기존 트랜지션 제거
    if (onState != null)
    {
        foreach (var t in onState.transitions.ToArray()) onState.RemoveTransition(t);
    }
    if (offState != null)
    {
        foreach (var t in offState.transitions.ToArray()) offState.RemoveTransition(t);
    }

    // 새 트랜지션 추가
    if (onState != null && offState != null)
    {
        AnimatorConditionMode conditionModeForOn = toggleReverse ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If;
        AnimatorConditionMode conditionModeForOff = toggleReverse ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;

        var transitionToOn = offState.AddTransition(onState);
        transitionToOn.hasExitTime = false;
        transitionToOn.duration = 0f;
        transitionToOn.AddCondition(conditionModeForOn, 0, paramName);

        var transitionToOff = onState.AddTransition(offState);
        transitionToOff.hasExitTime = false;
        transitionToOff.duration = 0f;
        transitionToOff.AddCondition(conditionModeForOff, 0, paramName);
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();
}


    private static string GetParameterNameFromToggle(GameObject toggleObj)
    {
        var menuItem = toggleObj.GetComponent<ModularAvatarMenuItem>();
        if (menuItem != null && menuItem.Control != null && menuItem.Control.parameter != null)
        {
            return menuItem.Control.parameter.name;
        }
        return null;
    }

    private static string Md5Hash(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] hashBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(input));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }
    }

    private static void SetUnityObjectIcon(UnityEngine.Object unityObject, Texture2D icon)
    {
        try
        {
            if (unityObject != null && icon != null)
            {
                Type editorGUIUtilityType = typeof(EditorGUIUtility);
                BindingFlags bindingFlags = BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic;
                object[] args = new object[] { unityObject, icon };
                editorGUIUtilityType.InvokeMember("SetIconForObject", bindingFlags, null, null, args);
            }
        }
        catch (MissingMethodException)
        {
#if UNITY_2022_3_OR_NEWER
            if (unityObject != null && icon != null)
            {
                EditorGUIUtility.SetIconForObject(unityObject, icon);
            }
#endif
        }
    }

    private void ApplySettings()
    {
        var targetObject = (ToggleConfig)target;
        if (targetObject == null)
        {
            Debug.LogError("Target object is null.");
            return;
        }
        ToggleSettings data = new ToggleSettings
        {
            toggleSaved = targetObject.toggleConfig.toggleSaved,
            toggleReverse = targetObject.toggleConfig.toggleReverse,
            toggleMenuName = targetObject.toggleConfig.toggleMenuName,
        };
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(jsonFilePath, json);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Confirmation / 확인",
            "Settings have been applied. They will take effect from the next toggle creation.\n설정이 적용되었습니다. 다음 토글 생성부터 반영됩니다.",
            "OK");
    }

    private void ResetSettings()
    {
        if (File.Exists(jsonFilePath))
        {
            ToggleSettings data = new ToggleSettings
            {
                toggleSaved = true,
                toggleReverse = false,
                toggleMenuName = "Toggles",
            };
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(jsonFilePath, json);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var targetObject = (ToggleConfig)target;
            if (targetObject != null)
            {
                targetObject.toggleConfig.toggleSaved = true;
                targetObject.toggleConfig.toggleReverse = false;
                targetObject.toggleConfig.toggleMenuName = "Toggles";
            }
            EditorUtility.DisplayDialog(
                "Reset Settings / 설정 초기화",
                "Settings have been reset to default values.\n설정이 기본값으로 재설정되었습니다.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Warning / 경고",
                "Settings file not found.\n설정 파일을 찾을 수 없습니다.",
                "OK");
        }
    }

    private void LoadSettings()
    {
        if (File.Exists(jsonFilePath))
        {
            string json = File.ReadAllText(jsonFilePath);
            ToggleSettings data = JsonUtility.FromJson<ToggleSettings>(json);
            var targetObject = (ToggleConfig)target;
            if (targetObject != null)
            {
                targetObject.toggleConfig.toggleSaved = data.toggleSaved;
                targetObject.toggleConfig.toggleReverse = data.toggleReverse;
                targetObject.toggleConfig.toggleMenuName = data.toggleMenuName;
            }
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Warning / 경고",
                "Settings file not found.\n설정 파일을 찾을 수 없습니다.",
                "OK");
        }
    }

    private static void ConfigureParentMenuItem(GameObject obj)
    {
        obj.AddComponent<ToggleConfig>();
        obj.AddComponent<DeleteToggle>();
        var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
        menuItem.Control = menuItem.Control ?? new VRCExpressionsMenu.Control();
        menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
        menuItem.MenuSource = SubmenuSource.Children;
        menuItem.Control.icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png");
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
