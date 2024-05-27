#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using nadena.dev.modular_avatar.core;
using Runtime;
using UnityEditor;
using UnityEngine;

//v1.0.68
[CustomEditor(typeof(ToggleItem))]
[InitializeOnLoad]
public class ToggleItemEditor : Editor
{
    private const string iconFilePath = "Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png";
    private ToggleItem _toggleItem;

    static ToggleItemEditor()
    {
        EditorApplication.update += UpdateIcons;
    }
        
    private static void UpdateIcons()
    {
        // 모든 ToggleItem 인스턴스에 대해 아이콘 설정
        var toggleItems = Resources.FindObjectsOfTypeAll<ToggleItem>();
        foreach (var toggleItem in toggleItems)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconFilePath);
            SetUnityObjectIcon(toggleItem, icon);
        }
        
        // 설정 완료 후 이벤트 해제
        EditorApplication.update -= UpdateIcons;
    }

    private void OnEnable()
    {
        this._toggleItem = (ToggleItem)target;
        this._toggleItem._icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconFilePath);
        SetUnityObjectIcon(this._toggleItem, this._toggleItem._icon);
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
        
    public override void OnInspectorGUI()
    {
        var blendShapesToChange = serializedObject.FindProperty("_blendShapesToChange");

        if (blendShapesToChange == null)
        {
            EditorGUILayout.HelpBox("_blendShapesToChange property not found.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("Blend Shapes");

        EditorGUI.indentLevel++;

        int listSize = blendShapesToChange.arraySize;
        EditorGUILayout.LabelField("Size", listSize.ToString());

        EditorGUILayout.Space();

        for (int i = 0; i < blendShapesToChange.arraySize; i++)
        {
            if (i > 0) EditorGUILayout.Space(); // Add space between elements

            EditorGUILayout.BeginVertical(GUI.skin.box); // Start box

            var element = blendShapesToChange.GetArrayElementAtIndex(i);
            var skinnedMesh = element.FindPropertyRelative("SkinnedMesh");
            var blendShapeName = element.FindPropertyRelative("name");
            var blendShapeValue = element.FindPropertyRelative("value");

            EditorGUILayout.PropertyField(skinnedMesh, new GUIContent("Skinned Mesh"));

            if (skinnedMesh.objectReferenceValue is SkinnedMeshRenderer renderer)
            {
                List<string> blendShapeNames = new List<string> { "Please select" };
                blendShapeNames.AddRange(Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                    .Select(index => renderer.sharedMesh.GetBlendShapeName(index)));

                int currentIndex = blendShapeNames.IndexOf(blendShapeName.stringValue);
                if (currentIndex == -1) currentIndex = 0; // 기본값으로 "Please select" 설정

                currentIndex = EditorGUILayout.Popup("Name", currentIndex, blendShapeNames.ToArray());

                blendShapeName.stringValue = blendShapeNames[currentIndex];

                if (blendShapeName.stringValue != "Please select")
                {
                    blendShapeValue.intValue = EditorGUILayout.IntSlider("Value", blendShapeValue.intValue, 0, 100);
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.IntSlider("Value", 0, 0, 100);
                    EditorGUI.EndDisabledGroup();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Name", "Please select a Skinned Mesh");
                blendShapeName.stringValue = "Please select";
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntSlider("Value", 0, 0, 100);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical(); // End box
        }

        EditorGUILayout.Space();

        // Add and remove buttons
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // Push buttons to the right
        if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(20)))
        {
            blendShapesToChange.InsertArrayElementAtIndex(blendShapesToChange.arraySize);
            var newElement = blendShapesToChange.GetArrayElementAtIndex(blendShapesToChange.arraySize - 1);
            newElement.FindPropertyRelative("SkinnedMesh").objectReferenceValue = null;
            newElement.FindPropertyRelative("name").stringValue = "Please select";
            newElement.FindPropertyRelative("value").intValue = 0;
        }

        if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(20)))
        {
            if (blendShapesToChange.arraySize > 0)
            {
                blendShapesToChange.DeleteArrayElementAtIndex(blendShapesToChange.arraySize - 1);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;

        EditorGUILayout.Space();

        // Apply 버튼
        if (GUILayout.Button("Apply"))
        {
            ApplyBlendShape();
        }

        void ApplyBlendShape()
        {
            // Cast target to ToggleItem
            ToggleItem toggleItem = (ToggleItem)target;

            // Get the other components
            var menuItem = toggleItem.GetComponent<ModularAvatarMenuItem>();

            if (menuItem != null)
            {
                Debug.Log($"MAMenuItem found: {menuItem.name}");

                // Access the Control property
                var control = menuItem.Control;

                // Get the parameter name
                string parameterName = control.parameter?.name ?? "None";
                Debug.Log($"Parameter name: {parameterName}");

                Debug.Log("토글로왔음");
                string onToggleAnimePath = $"Assets/Hirami/Toggle/{parameterName}_on.anim";
                string offToggleAnimePath = $"Assets/Hirami/Toggle/{parameterName}_off.anim";

                // Check if the files exist
                bool onToggleExists = File.Exists(onToggleAnimePath);
                bool offToggleExists = File.Exists(offToggleAnimePath);

                if (onToggleExists && offToggleExists)
                {
                    Debug.Log("on off 파일이 같이 있음");

                    AnimationClip onClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath);

                    // Clear all existing blend shape animations
                    ClearAllBlendShapeAnimations(onClip, toggleItem);

                    foreach (var blendShapeChange in toggleItem.BlendShapesToChange)
                    {
                        // Get the SkinnedMeshRenderer
                        var skinnedMeshRenderer = blendShapeChange.SkinnedMesh;
                        if (skinnedMeshRenderer == null) continue;

                        // Get the blend shape index
                        int blendShapeIndex =
                            skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeChange.name);
                        if (blendShapeIndex < 0) continue;

                        // Apply blend shape changes to onClip and offClip
                        var transform = skinnedMeshRenderer.transform;
                        var blendShapePath = AnimationUtility.CalculateTransformPath(transform, transform.root);

                        AnimationCurve onCurve = AnimationCurve.Constant(0, 1, blendShapeChange.value);
                        AnimationUtility.SetEditorCurve(onClip, blendShapePath, typeof(SkinnedMeshRenderer),
                            $"blendShape.{blendShapeChange.name}", onCurve);

                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        serializedObject.ApplyModifiedProperties();

        void ClearAllBlendShapeAnimations(AnimationClip clip, ToggleItem toggleItem)
        {
            if (clip == null) return;

            // Remove all blend shape animations from the clip
            var editorCurveBindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in editorCurveBindings)
            {
                if (!binding.propertyName.Equals("m_IsActive"))
                {
                    Debug.Log("path :: " + binding.path);
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif