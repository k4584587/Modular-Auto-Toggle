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
        private bool _applyToOnAnimation = true;  // On 애니메이션에 적용 여부
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
            if(spTargetObjects != null)
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
                ApplyBlendShape();
            }

            // 블렌드 쉐입을 실제 애니메이션 클립에 적용하는 함수
            void ApplyBlendShape()
            {
                // ModularAvatarMenuItem 가져오기
                var menuItem = toggleItem.GetComponent<ModularAvatarMenuItem>();
                if (menuItem != null)
                {
                    Debug.Log($"MAMenuItem found: {menuItem.name}");

                    // Control 프로퍼티 접근
                    var control = menuItem.Control;

                    // 파라미터 이름 및 루트 이름
                    string parameterName = control.parameter?.name ?? "None";
                    string rootName = menuItem.transform.root.name;
                    Debug.Log($"Parameter name: {parameterName}");

                    // *.anim 파일 찾기
                    string fullPath = FindFileByGuid(parameterName, "Assets/Hirami/Toggle/" + rootName)
                        .Replace("_off.anim", "");

                    string onToggleAnimePath = fullPath + "_on.anim";
                    string offToggleAnimePath = fullPath + "_off.anim";

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

                        // 기존 블렌드 쉐입 애니메이션 클리어
                        if (_applyToOnAnimation)
                        {
                            ClearAllBlendShapeAnimations(onClip, toggleItem);
                        }
                        if (_applyToOffAnimation)
                        {
                            ClearAllBlendShapeAnimations(offClip, toggleItem);
                        }

                        // 새로 추가
                        foreach (var blendShapeChange in toggleItem.BlendShapesToChange)
                        {
                            var skinnedMeshRenderer = blendShapeChange.SkinnedMesh;
                            if (skinnedMeshRenderer == null) continue;

                            // targetGameObjects가 할당되어 있다면
                            if (toggleItem.targetGameObjects != null && toggleItem.targetGameObjects.Count > 0)
                            {
                                foreach (GameObject targetObj in toggleItem.targetGameObjects)
                                {
                                    var blendShapePath = AnimationUtility.CalculateTransformPath(skinnedMeshRenderer.transform, targetObj.transform);
                                    // onClip 설정
                                    if (onClip != null)
                                    {
                                        AnimationCurve onCurve = AnimationCurve.Linear(
                                            0, blendShapeChange.value,
                                            0, blendShapeChange.value
                                        );
                                        onClip.SetCurve(
                                            blendShapePath,
                                            typeof(SkinnedMeshRenderer),
                                            $"blendShape.{blendShapeChange.name}",
                                            onCurve
                                        );
                                    }
                                    // offClip 설정
                                    if (offClip != null)
                                    {
                                        AnimationCurve offCurve = AnimationCurve.Linear(
                                            0, blendShapeChange.value,
                                            0, blendShapeChange.value
                                        );
                                        offClip.SetCurve(
                                            blendShapePath,
                                            typeof(SkinnedMeshRenderer),
                                            $"blendShape.{blendShapeChange.name}",
                                            offCurve
                                        );
                                    }
                                }
                            }
                            else
                            {
                                // targetGameObjects가 없으면 스킨 메시의 루트를 기준으로 함
                                var referenceRoot = skinnedMeshRenderer.transform.root;
                                var blendShapePath = AnimationUtility.CalculateTransformPath(skinnedMeshRenderer.transform, referenceRoot);
                                // onClip 설정
                                if (onClip != null)
                                {
                                    AnimationCurve onCurve = AnimationCurve.Linear(
                                        0, blendShapeChange.value,
                                        0, blendShapeChange.value
                                    );
                                    onClip.SetCurve(
                                        blendShapePath,
                                        typeof(SkinnedMeshRenderer),
                                        $"blendShape.{blendShapeChange.name}",
                                        onCurve
                                    );
                                }
                                // offClip 설정
                                if (offClip != null)
                                {
                                    AnimationCurve offCurve = AnimationCurve.Linear(
                                        0, blendShapeChange.value,
                                        0, blendShapeChange.value
                                    );
                                    offClip.SetCurve(
                                        blendShapePath,
                                        typeof(SkinnedMeshRenderer),
                                        $"blendShape.{blendShapeChange.name}",
                                        offCurve
                                    );
                                }
                            }
                            AssetDatabase.SaveAssets();
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            // 특정 AnimationClip에서 블렌드 쉐입에 해당하는 커브를 모두 제거
            void ClearAllBlendShapeAnimations(AnimationClip clip, ToggleItem tItem)
            {
                if (clip == null) return;
                var editorCurveBindings = AnimationUtility.GetCurveBindings(clip);

                foreach (var binding in editorCurveBindings)
                {
                    // m_IsActive는 오브젝트 활성/비활성 처리이므로 건드리지 않음
                    if (!binding.propertyName.Equals("m_IsActive"))
                    {
                        Debug.Log("path :: " + binding.path);
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    }
                }
                AssetDatabase.SaveAssets();
            }

            // GUID로 애니메이션 파일 경로 찾기
            string FindFileByGuid(string guid, string searchFolder)
            {
                var allFiles = Directory.GetFiles(searchFolder, "*", System.IO.SearchOption.AllDirectories);
                var fileWithGuid = allFiles.FirstOrDefault(file =>
                    System.IO.Path.GetFileNameWithoutExtension(file).Contains(guid));
                return fileWithGuid;
            }

            serializedObject.Update();
        }
    }
}
#endif
