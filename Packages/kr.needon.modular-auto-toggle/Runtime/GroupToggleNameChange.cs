#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEditor;


namespace kr.needon.modular_auto_toggle.runtime.GroupToggleNameChange
{
    public class GroupToggleNameChange : MonoBehaviour
    {

        [HideInInspector] public string PreviousName = "";
        void Reset()
        {

            PreviousName = gameObject.name;
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
    

    [InitializeOnLoad]
    public class GameObjectNameChangeLogger : Editor
    {

        static GameObjectNameChangeLogger()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

internal static void UpdateNameInJson(string oldName, string newName)
{
    string filePath = "Assets/Hirami/Toggle/NameHashMappings.json";
    NameHashMappings mappings = JsonHelper.LoadNameHashMappings(filePath);

    bool isUpdated = false;
    for (int i = 0; i < mappings.mappings.Count; i++)
    {
        if (mappings.mappings[i].originalName == oldName)
        {
            // "Group_" 접두사를 추가하고, 새로운 이름도 함께 설정합니다.
            mappings.mappings[i].originalName = "Group_" + newName;
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
                GroupToggleNameChange tracker = go.GetComponent<GroupToggleNameChange>();
                if (tracker != null && go.name != tracker.PreviousName)
                {
                    Debug.Log("GameObject 이름이 변경됨: " + tracker.PreviousName + " -> " + go.name);
                    UpdateNameInJson(tracker.PreviousName, go.name);
                    tracker.PreviousName = "Group_" + go.name; // 이름을 업데이트합니다.
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
}
#endif
