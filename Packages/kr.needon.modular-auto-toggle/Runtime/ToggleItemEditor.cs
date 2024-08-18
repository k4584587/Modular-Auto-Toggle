#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using ToggleTool.Global;
using ToggleTool.Utils;

namespace ToggleTool.Runtime
{
    [CustomEditor(typeof(ToggleItem))]
    [InitializeOnLoad]
    public class ToggleItemEditor : UnityEditor.Editor
    {
        static ToggleItemEditor()
        {
            // 유니티 에디터 로드 시 자동으로 호출되는 정적 생성자
            EditorApplication.update += UpdateIcons;
        }

        private static void UpdateIcons()
        {
            // 모든 ToggleItem 인스턴스에 대해 아이콘 설정
            var toggleItems = Resources.FindObjectsOfTypeAll<ToggleItem>();
            foreach (var toggleItem in toggleItems)
            {
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(FilePaths.PACKAGE_RESOURCES_PATH + FilePaths.IMAGE_NAME_TOGGLE_ON);;
                if (icon != null)
                {
                    EditorGUIUtility.SetIconForObject(toggleItem, icon);
                }
            }

            // 설정 완료 후 이벤트 해제
            EditorApplication.update -= UpdateIcons;
        }
        
        private Texture2D _icon;
        private bool _applyToOnAnimation = true; // On 애니메이션에 적용할지 여부
        private bool _applyToOffAnimation = true; // Off 애니메이션에 적용할지 여부

        private const string ApplyToOnAnimationKey = "ToggleItemEditor_ApplyToOnAnimation";
        private const string ApplyToOffAnimationKey = "ToggleItemEditor_ApplyToOffAnimation";

        private void OnEnable()
        {
            UpdateIcons();
            _applyToOnAnimation = EditorPrefs.GetBool(ApplyToOnAnimationKey, true);
            _applyToOffAnimation = EditorPrefs.GetBool(ApplyToOffAnimationKey, true);
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(ApplyToOnAnimationKey, _applyToOnAnimation);
            EditorPrefs.SetBool(ApplyToOffAnimationKey, _applyToOffAnimation);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var blendShapesToChange = serializedObject.FindProperty("_blendShapesToChange");

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
            EditorGUILayout.BeginHorizontal();
            _applyToOnAnimation = EditorGUILayout.Toggle("Apply to On Animation", _applyToOnAnimation);
            _applyToOffAnimation = EditorGUILayout.Toggle("Apply to Off Animation", _applyToOffAnimation);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Apply"))
            {
                applyBlendShape();
            }

            void applyBlendShape()
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
                    string rootName = menuItem.transform.root.name;

                    Debug.Log($"Parameter name: {parameterName}");
                    string fullPath = FindFileByGuid(parameterName, FilePaths.TARGET_FOLDER_PATH + "/" + rootName).Replace("_off.anim", "");

                    string onToggleAnimePath = fullPath + "_on.anim";
                    string offToggleAnimePath = fullPath + "_off.anim";

                    // Check if the files exist
                    bool onToggleExists = File.Exists(onToggleAnimePath);
                    bool offToggleExists = File.Exists(offToggleAnimePath);

                    if (onToggleExists && offToggleExists)
                    {
                        AnimationClip onClip = _applyToOnAnimation
                            ? AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath)
                            : null;
                        AnimationClip offClip = _applyToOffAnimation
                            ? AssetDatabase.LoadAssetAtPath<AnimationClip>(offToggleAnimePath)
                            : null;

                        // Clear all existing blend shape animations
                        if (_applyToOnAnimation)
                        {
                            clearAllBlendShapeAnimations(onClip, toggleItem);
                        }

                        if (_applyToOffAnimation)
                        {
                            clearAllBlendShapeAnimations(offClip, toggleItem);
                        }

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

                            if (onClip != null)
                            {
                                AnimationCurve onCurve = AnimationCurve.Linear(0, blendShapeChange.value, 0,
                                    blendShapeChange.value);
                                onClip.SetCurve(blendShapePath, typeof(SkinnedMeshRenderer),
                                    $"blendShape.{blendShapeChange.name}", onCurve);
                            }

                            if (offClip != null)
                            {
                                AnimationCurve offCurve = AnimationCurve.Linear(0, blendShapeChange.value, 0,
                                    blendShapeChange.value);
                                offClip.SetCurve(blendShapePath, typeof(SkinnedMeshRenderer),
                                    $"blendShape.{blendShapeChange.name}", offCurve);
                            }

                            AssetDatabase.SaveAssets();
                        }

                        // 초기화된 애니메이션 클립에서 블렌드 쉐이프 값을 제거합니다.
                        /* if (!_applyToOnAnimation && onToggleExists)
                        {
                            AnimationClip onClipToClear =
                                AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath);
                            clearAllBlendShapeAnimations(onClipToClear, toggleItem);
                        }

                        if (!_applyToOffAnimation && offToggleExists)
                        {
                            AnimationClip offClipToClear =
                                AssetDatabase.LoadAssetAtPath<AnimationClip>(offToggleAnimePath);
                            clearAllBlendShapeAnimations(offClipToClear, toggleItem);
                        }*/
                    }
                }
            }


            serializedObject.ApplyModifiedProperties();

            void clearAllBlendShapeAnimations(AnimationClip clip, ToggleItem toggleItem)
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
            string FindFileByGuid(string guid, string searchFolder)
            {
                var allFiles = Directory.GetFiles(searchFolder, "*", SearchOption.AllDirectories);
                var fileWithGuid = allFiles.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file).Contains(guid));
                return fileWithGuid;
            }
        }
    }
}
#endif