using System.IO;
using UnityEngine;

namespace ToggleTool.Global
{
    public static class Version
    {
        /// <summary>
        /// LATEST VERSION
        /// </summary>
        public static readonly string LATEST_VERSION;

        static Version()
        {
            string jsonPath = FilePaths.PACKAGE_PATH + "package.json";
            if (File.Exists(jsonPath))
            {
                string jsonText = File.ReadAllText(jsonPath);
                PackageInfo packageInfo = JsonUtility.FromJson<PackageInfo>(jsonText);
                LATEST_VERSION = packageInfo.version;
            }
            else
            {
                Debug.LogError("package.json not found.");
                LATEST_VERSION = "Unknown";
            }
        }

        [System.Serializable]
        private class PackageInfo
        {
            public string version;
        }
    }
}