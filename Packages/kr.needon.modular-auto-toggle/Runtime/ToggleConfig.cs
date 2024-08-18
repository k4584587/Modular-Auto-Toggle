#if UNITY_EDITOR
using System;
using System.IO;
using nadena.dev.modular_avatar.core;
using ToggleTool.Global;
using ToggleTool.Models;
using UnityEngine;
using Version = ToggleTool.Global.Version;

namespace ToggleTool.Runtime
{
    //v1.0.71
    [DisallowMultipleComponent]
    public class ToggleConfig : AvatarTagComponent
    {
            
        public Texture2D _icon;
        public ToggleConfigModel toggleConfig;
            
        private void Reset()
        {
            // 기본값 설정
            LoadConfigFromFile();
        }

        public void ApplyConfig(ToggleConfigModel config)
        {
            toggleConfig = config;
            // 필요한 경우 추가 로직 작성
        }

        public void LoadConfigFromFile()
        {
            if (File.Exists(FilePaths.JSON_FILE_PATH))
            {
                string json = File.ReadAllText(FilePaths.JSON_FILE_PATH);
                ToggleConfigModel config = JsonUtility.FromJson<ToggleConfigModel>(json);
                ApplyConfig(config);
                Debug.Log("Settings loaded from JSON file.");
            }
            else
            {
                // 파일이 없는 경우 기본값 설정
                toggleConfig.toggleSaved = true;
                toggleConfig.toggleReverse = false;
                toggleConfig.toggleMenuName = Components.DEFAULT_COMPONENT_NAME;
                Debug.LogWarning("Settings file not found. Default settings applied.");
            }
        }
    }
}
#endif