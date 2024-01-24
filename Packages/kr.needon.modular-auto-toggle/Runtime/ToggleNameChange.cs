using UnityEngine;
using System.Collections.Generic;
using System.IO;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace kr.needon.modular_auto_toggle.runtime.ToggleNameChange
{
    public class ToggleNameChange : MonoBehaviour
    {

        [HideInInspector] public string PreviousName = "";
        void Reset()
        {

            PreviousName = "Toggle_" + gameObject.name;
        }

        void OnValidate()
        {
            if (PreviousName != gameObject.name)
            {
                Debug.Log(gameObject.name);
                GameObjectNameChangeLogger.UpdateNameInJson(PreviousName, gameObject.name);
                PreviousName = gameObject.name;
            }
        }

    }



#if UNITY_EDITOR

    [InitializeOnLoad]
    [CustomEditor(typeof(GameObject))]
    public class GameObjectNameChangeLogger : Editor
    {

        static GameObjectNameChangeLogger()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        internal static void UpdateNameInJson(string oldName, string newName)
        {
            Debug.Log("UpdateNameInJson oldName :: " + oldName);
            Debug.Log("UpdateNameInJson newName :: " + newName);

            string filePath = "Assets/Hirami/Toggle/NameHashMappings.json";
            NameHashMappings mappings = JsonHelper.LoadNameHashMappings(filePath);

            // 새 이름의 중복 횟수를 확인합니다.
            int duplicateCount = 0;
            foreach (var mapping in mappings.mappings)
            {
                if ("Toggle_" + newName == mapping.originalName)
                {
                    duplicateCount++;
                }
            }

            // 중복 횟수가 3개 이상이면 이름 변경을 취소합니다.
            if (duplicateCount >= 3)
            {
                EditorUtility.DisplayDialog("중복확인", "중복된 이름이 있습니다. 다른이름을 사용해주세요.", "OK");
                return; // 메서드 종료
            }

            bool isUpdated = false;
            for (int i = 0; i < mappings.mappings.Count; i++)
            {
                if (mappings.mappings[i].originalName == oldName)
                {
                    mappings.mappings[i].originalName = "Toggle_" + newName;
                    isUpdated = true;
                }
            }

            if (isUpdated)
            {
                JsonHelper.SaveNameHashMappings(mappings, filePath);
            }
        }



        private static void OnHierarchyChanged()
        {
            GameObject[] allGameObjects = Object.FindObjectsOfType<GameObject>();
            foreach (GameObject go in allGameObjects)
            {
                ToggleNameChange tracker = go.GetComponent<ToggleNameChange>();
                if (tracker != null && go.name != tracker.PreviousName)
                {
                    UpdateNameInJson(tracker.PreviousName, go.name);
                    tracker.PreviousName = "Toggle_" + go.name; // 이름을 업데이트합니다.
                }
            }
        }


    }

    public static class JsonHelper
    {
        public static NameHashMappings LoadNameHashMappings(string filePath)
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<NameHashMappings>(json);
            }
            return new NameHashMappings();
        }

        public static void SaveNameHashMappings(NameHashMappings mappings, string filePath)
        {
            string json = JsonUtility.ToJson(mappings, true);
            File.WriteAllText(filePath, json);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }


    [System.Serializable]
    public class NameHashMapping
    {
        public string originalName;
        public string hashedName;
    }

    [System.Serializable]
    public class NameHashMappings
    {
        public List<NameHashMapping> mappings;
    }

#endif
}
