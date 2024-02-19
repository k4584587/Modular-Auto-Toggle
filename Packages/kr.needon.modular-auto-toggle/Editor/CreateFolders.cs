#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 애셋이 임포트될 때 호출되는 클래스
namespace Editor
{
    public class AutoCreateFolder : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            var targetFolderPath = "Assets/Hirami/Toggle";

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
        }
    }
}
#endif