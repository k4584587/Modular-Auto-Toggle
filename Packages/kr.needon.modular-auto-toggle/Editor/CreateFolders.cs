#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class AutoCreateFolder : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            var targetFolderPath = "Assets/Hirami/Toggle";
            var specificFilePath = "Assets/Hirami/Toggle/toggle_fx.controller";
            var warningKey = "ToggleFxControllerWarningShown";

            // 폴더 생성 로직
            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                string[] folders = targetFolderPath.Split('/');
                string parentFolder = folders[0];

                for (int i = 1; i < folders.Length; i++)
                {
                    string folderPath = parentFolder + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(folderPath))
                    {
                        AssetDatabase.CreateFolder(parentFolder, folders[i]);
                        Debug.Log(folderPath + " folder has been created.");
                    }

                    parentFolder = folderPath;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
 
            // 특정 파일 존재 확인 및 경고창 표시 로직
            if (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(specificFilePath) != null)
            {
                if (!EditorPrefs.GetBool(warningKey, false))
                {
                    EditorUtility.DisplayDialog("Warning!", specificFilePath + " 파일이 존재합니다. 이 파일은 구버전이므로 1.0.66 버전에서는 토글 생성 기능이 제대로 작동하지 않을 수 있습니다.\n\n문제를 해결하려면, 'Hirami -> Delete Toggle Objects' 메뉴에서 'All Delete Toggle'을 선택하여 모든 토글 오브젝트를 삭제한 후, 새로 만들어 주시기 바랍니다.\n\nfile exists. This file is considered outdated, and the toggle creation feature may not work properly in version 1.0.66.\n\nTo resolve this issue, please go to 'Hirami -> Delete Toggle Objects' and select 'All Delete Toggle' to remove all toggle objects, and then recreate them.", "Ok");
                    EditorPrefs.SetBool(warningKey, true);
                }
            }
            else
            {
                EditorPrefs.SetBool(warningKey, false); // 파일이 없으면 상태 리셋
            }
        }
    }
}
#endif