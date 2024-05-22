using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

//v1.0.68
namespace Editor
{
    [InitializeOnLoad]
    public class VersionChecker
    {
        private static readonly string url = "https://api01.needon.kr/v1/api/version/check";
        private static readonly string localVersionPath = "Assets/Hirami/Toggle/setting.json";
        private static double nextCheckTime;
        private static readonly string prefsKey = "VersionCheckerDismissed";
        private static readonly string updateUrl = "https://hirami.booth.pm/items/5314930"; // 업데이트 사이트 URL

        static VersionChecker()
        {
            // 에디터가 시작될 때 플래그를 초기화합니다.
            if (!EditorPrefs.HasKey(prefsKey))
            {
                EditorPrefs.SetBool(prefsKey, false);
            }
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup >= nextCheckTime && !EditorPrefs.GetBool(prefsKey, false))
            {
                CheckVersion();
                nextCheckTime = EditorApplication.timeSinceStartup + 86400; // 하루에 한 번만 체크하도록 설정
            }
        }

        private static void CheckVersion()
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SendWebRequest().completed += operation =>
            {
                if (request.isNetworkError || request.isHttpError)
                {
                    Debug.LogError("Error: " + request.error);
                }
                else
                {
                    try
                    {
                        ServerVersion serverVersion = JsonUtility.FromJson<ServerVersion>(request.downloadHandler.text);
                        string localVersionJson = System.IO.File.ReadAllText(localVersionPath);
                        LocalVersion localVersion = JsonUtility.FromJson<LocalVersion>(localVersionJson);

                        if (serverVersion.version != localVersion.version)
                        {
                            int result = EditorUtility.DisplayDialogComplex(
                                "Modular Auto Toggle Tool Need Update",
                                $"Current version: {localVersion.version}\nLatest version: {serverVersion.version}\nPlease update to the latest version.",
                                "Update",
                                "Cancel",
                                "Keep Current Version"
                            );

                            if (result == 0) // Update
                            {
                                EditorPrefs.SetBool(prefsKey, true);
                                Process.Start(new ProcessStartInfo(updateUrl) { UseShellExecute = true });
                            }
                            else if (result == 2) // Keep Current Version
                            {
                                EditorPrefs.SetBool(prefsKey, true);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("Exception: " + ex.Message);
                    }
                }
            };
        }

        /*
        // 버전 체크 테스트용
        [MenuItem("Tools/Reset VersionChecker")]
        public static void ResetVersionChecker()
        {
            EditorPrefs.DeleteKey(prefsKey);
            Debug.Log("VersionChecker prefs reset.");
        }
        */

        [System.Serializable]
        private class ServerVersion
        {
            public int id;
            public string version;
        }

        [System.Serializable]
        private class LocalVersion
        {
            public string version;
        }
    }
}
