using nadena.dev.modular_avatar.core;
using UnityEngine;
using ToggleTool.Utils;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToggleTool.Editor
{
    public static class ImageReloader
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void OnInitialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Play 모드 종료 후 아이콘 재설정
                ResetIcons();
            }
        }

        private static void ResetIcons()
        {
            foreach (var loader in ImageLoader.instance.Values)
            {
                loader.LoadImage(loader.Filename); // 다시 아이콘 로드
            }

            // 여기서 다시 아이콘을 설정
            foreach (var menuItem in GameObject.FindObjectsOfType<ModularAvatarMenuItem>())
            {
                string paramName = menuItem.Control.parameter.name;
                if (ImageLoader.instance.TryGetValue("ToggleON", out ImageLoader loader))
                {
                    menuItem.Control.icon = loader.iconTexture;
                }
            }
        }
#endif
    }
}