#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace Runtime
{
    [CustomEditor(typeof(ToggleItem))]
    public class ToggleItemEditor : Editor
    {
        private Texture2D _icon;
        private bool _applyToOnAnimation = true; // On 애니메이션에 적용 여부
        private bool _applyToOffAnimation = true; // Off 애니메이션에 적용 여부

        private const string ApplyToOnAnimationKey = "ToggleItemEditor_ApplyToOnAnimation";
        private const string ApplyToOffAnimationKey = "ToggleItemEditor_ApplyToOffAnimation";

        private void OnEnable()
        {
            // 아이콘 로드
            _icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-toggle/Resource/toggleON.png");
            if (_icon != null)
            {
                EditorGUIUtility.SetIconForObject(target, _icon);
            }

            // 에디터 프리팹스에서 이전 설정 불러오기
            _applyToOnAnimation = EditorPrefs.GetBool(ApplyToOnAnimationKey, true);
            _applyToOffAnimation = EditorPrefs.GetBool(ApplyToOffAnimationKey, true);
        }

        private void OnDisable()
        {
            // 에디터 프리팹스에 현재 설정 저장
            EditorPrefs.SetBool(ApplyToOnAnimationKey, _applyToOnAnimation);
            EditorPrefs.SetBool(ApplyToOffAnimationKey, _applyToOffAnimation);
        }

        public override void OnInspectorGUI()
        {
            // 직렬화 업데이트
            serializedObject.Update();

            // targetGameObjects 프로퍼티를 찾습니다.
            var spTargetObjects = serializedObject.FindProperty("targetGameObjects");
            if (spTargetObjects != null)
            {
                EditorGUILayout.PropertyField(spTargetObjects, new GUIContent("Target Toggle Objects"), true);
            }
            else
            {
                EditorGUILayout.HelpBox("ToggleItem에 targetGameObjects 필드가 존재하지 않습니다.", MessageType.Error);
            }

            serializedObject.ApplyModifiedProperties();

            // ToggleItem 대상
            ToggleItem toggleItem = (ToggleItem)target;

            // Blend Shape 리스트
            var blendShapesToChange = serializedObject.FindProperty("_blendShapesToChange");
            EditorGUILayout.LabelField("Blend Shapes");

            EditorGUI.indentLevel++;
            int listSize = blendShapesToChange.arraySize;
            EditorGUILayout.LabelField("Size", listSize.ToString());
            EditorGUILayout.Space();

            for (int i = 0; i < blendShapesToChange.arraySize; i++)
            {
                if (i > 0) EditorGUILayout.Space(); // 요소 간 구분

                EditorGUILayout.BeginVertical(GUI.skin.box);
                var element = blendShapesToChange.GetArrayElementAtIndex(i);
                var skinnedMesh = element.FindPropertyRelative("SkinnedMesh");
                var blendShapeName = element.FindPropertyRelative("name");
                var blendShapeValue = element.FindPropertyRelative("value");

                EditorGUILayout.PropertyField(skinnedMesh, new GUIContent("Skinned Mesh"));

                if (skinnedMesh.objectReferenceValue is SkinnedMeshRenderer renderer)
                {
                    // 블렌드 쉐입 목록 구성
                    List<string> blendShapeNames = new List<string> { "Please select" };
                    blendShapeNames.AddRange(
                        Enumerable.Range(0, renderer.sharedMesh.blendShapeCount)
                            .Select(index => renderer.sharedMesh.GetBlendShapeName(index))
                    );

                    // 현재 인덱스 가져오기
                    int currentIndex = blendShapeNames.IndexOf(blendShapeName.stringValue);
                    if (currentIndex == -1) currentIndex = 0; // 잘못된 값이면 0으로

                    // 팝업으로 선택
                    currentIndex = EditorGUILayout.Popup("Name", currentIndex, blendShapeNames.ToArray());
                    blendShapeName.stringValue = blendShapeNames[currentIndex];

                    // Value 슬라이더
                    if (blendShapeName.stringValue != "Please select")
                    {
                        blendShapeValue.intValue = EditorGUILayout.IntSlider(
                            "Value",
                            blendShapeValue.intValue,
                            0,
                            100
                        );
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

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();

            // +, - 버튼으로 Blend Shape 추가/삭제
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
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

            // On/Off 애니메이션 적용 여부
            EditorGUILayout.BeginHorizontal();
            _applyToOnAnimation = EditorGUILayout.Toggle("Apply to On Animation", _applyToOnAnimation);
            _applyToOffAnimation = EditorGUILayout.Toggle("Apply to Off Animation", _applyToOffAnimation);
            EditorGUILayout.EndHorizontal();

            // Apply 버튼
            if (GUILayout.Button("Apply"))
            {
                // EditorPrefs를 현재 토글 상태로 업데이트
                EditorPrefs.SetBool(ApplyToOnAnimationKey, _applyToOnAnimation);
                EditorPrefs.SetBool(ApplyToOffAnimationKey, _applyToOffAnimation);

                // 정적 메서드 호출
                ApplyBlendShapeToItem(toggleItem);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// ToggleItem에 설정된 블렌드 쉐입 애니메이션을 실제 애니메이션 클립에 적용합니다.
        /// </summary>
        public static void ApplyBlendShapeToItem(ToggleItem toggleItem)
        {
            bool applyToOnAnimation = EditorPrefs.GetBool(ApplyToOnAnimationKey, true);
            bool applyToOffAnimation = EditorPrefs.GetBool(ApplyToOffAnimationKey, true);

            // ModularAvatarMenuItem 가져오기
            var menuItem = toggleItem.GetComponent<ModularAvatarMenuItem>();
            if (menuItem != null)
            {
                Debug.Log($"MAMenuItem found: {menuItem.name}");

                // Control 프로퍼티 접근
                var control = menuItem.Control;
                string parameterName = control.parameter?.name ?? "None";
                string rootName = menuItem.transform.root.name;
                Debug.Log($"Parameter name: {parameterName}");

                // *.anim 파일 찾기
                string fullPath = FindFileByGuidStatic(parameterName, "Assets/Hirami/Toggle/" + rootName)
                    .Replace("_off.anim", "");
                string onToggleAnimePath = fullPath + "_on.anim";
                string offToggleAnimePath = fullPath + "_off.anim";

                bool onToggleExists = File.Exists(onToggleAnimePath);
                bool offToggleExists = File.Exists(offToggleAnimePath);

                if (onToggleExists && offToggleExists)
                {
                    // 두 애니메이션 클립을 무조건 로드하여 기존 커브를 클리어합니다.
                    AnimationClip onClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(onToggleAnimePath);
                    AnimationClip offClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(offToggleAnimePath);

                    if (onClip != null)
                    {
                        ClearAllBlendShapeAnimationsStatic(onClip);
                    }

                    if (offClip != null)
                    {
                        ClearAllBlendShapeAnimationsStatic(offClip);
                    }
                    
                    foreach (var blendShapeChange in toggleItem.BlendShapesToChange)
                    {
                        var skinnedMeshRenderer = blendShapeChange.SkinnedMesh;
                        if (skinnedMeshRenderer == null) continue;

                        // 스킨매쉬에 할당된 경로로 커브를 적용
                        string blendShapePath = AnimationUtility.CalculateTransformPath(skinnedMeshRenderer.transform, skinnedMeshRenderer.transform.root);
                        if (applyToOnAnimation && onClip != null)
                        {
                            AnimationCurve onCurve = AnimationCurve.Linear(0f, blendShapeChange.value, 0f, blendShapeChange.value);
                            onClip.SetCurve(blendShapePath, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeChange.name}", onCurve);
                        }

                        if (applyToOffAnimation && offClip != null)
                        {
                            AnimationCurve offCurve = AnimationCurve.Linear(0f, blendShapeChange.value, 0f, blendShapeChange.value);
                            offClip.SetCurve(blendShapePath, typeof(SkinnedMeshRenderer), $"blendShape.{blendShapeChange.name}", offCurve);
                        }
                    }

                    AssetDatabase.SaveAssets();
                }
            }
        }


        // GUID로 애니메이션 파일 경로 찾기 (정적 버전)
        private static string FindFileByGuidStatic(string guid, string searchFolder)
        {
            var allFiles = Directory.GetFiles(searchFolder, "*", System.IO.SearchOption.AllDirectories);
            var fileWithGuid = allFiles.FirstOrDefault(file =>
                System.IO.Path.GetFileNameWithoutExtension(file).Contains(guid));
            return fileWithGuid;
        }

        // AnimationClip에서 블렌드 쉐입 관련 커브를 모두 제거 (정적 버전)
        private static void ClearAllBlendShapeAnimationsStatic(AnimationClip clip)
        {
            if (clip == null) return;
            var editorCurveBindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in editorCurveBindings)
            {
                if (!binding.propertyName.Equals("m_IsActive"))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif