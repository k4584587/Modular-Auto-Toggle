#if UNITY_EDITOR
using System;
using System.IO;
using nadena.dev.modular_avatar.core;
using UnityEngine;

//v1.0.68
[DisallowMultipleComponent]
[AddComponentMenu("Hirami/Toggle/ToggleConfig")]
public class ToggleConfig : AvatarTagComponent
{
        
    public Texture2D _icon;
        
    [Serializable]
    public struct SetToggleConfig
    {
        public string version;
        public bool toggleReverse;
        public string toggleMenuName;
        public string groupToggleMenuName;
    }

    public SetToggleConfig toggleConfig;
        
    private void Reset()
    {
        // 기본값 설정
        LoadConfigFromFile();
    }

    public void ApplyConfig(SetToggleConfig config)
    {
        toggleConfig = config;
        // 필요한 경우 추가 로직 작성
    }

    public void LoadConfigFromFile()
    {
        string jsonFilePath = "Assets/Hirami/Toggle/setting.json";
        if (File.Exists(jsonFilePath))
        {
            string json = File.ReadAllText(jsonFilePath);
            SetToggleConfig config = JsonUtility.FromJson<SetToggleConfig>(json);
            ApplyConfig(config);
            Debug.Log("Settings loaded from JSON file.");
        }
        else
        {
            // 파일이 없는 경우 기본값 설정
            toggleConfig.version = "1.0.70";
            toggleConfig.toggleReverse = false;
            toggleConfig.toggleMenuName = "Toggles";
            toggleConfig.groupToggleMenuName = "GroupToggles";
            Debug.LogWarning("Settings file not found. Default settings applied.");
        }
    }
}
#endif