#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ToggleTool.Runtime;

namespace ToggleTool.Editor
{
    //v1.0.68
    public static class HierarchyGUI
    {
        private const int ICON_SIZE = 16;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
        }

        private static void OnGUI(int instanceID, Rect selectionRect)
        {
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null)
            {
                return;
            }

            // ToggleItem 또는 ToggleConfig 컴포넌트가 있는지 확인
            if (!HasToggleComponents(gameObject))
            {
                return;
            }

            // 최소 두 개 이상의 컴포넌트가 있는지 확인
            Component[] components = gameObject.GetComponents<Component>();
            if (components.Length <= 1)
            {
                return;
            }

            // 아이콘을 오른쪽 끝에 배치
            Rect iconRect = new Rect(selectionRect.xMax - ICON_SIZE, selectionRect.y, ICON_SIZE, ICON_SIZE);

            // ToggleItem 또는 ToggleConfig 컴포넌트에 대해 아이콘 표시
            DrawIcons(components, iconRect);
        }

        private static bool HasToggleComponents(GameObject gameObject)
        {
            return gameObject.GetComponent<ToggleItem>() != null || gameObject.GetComponent<ToggleConfig>() != null;
        }

        private static void DrawIcons(Component[] components, Rect iconRect)
        {
            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                System.Type componentType = component.GetType();
                if (componentType == typeof(ToggleItem) || componentType == typeof(ToggleConfig))
                {
                    Texture2D icon = AssetPreview.GetMiniThumbnail(component);
                    if (icon != null)
                    {
                        GUI.DrawTexture(iconRect, icon);
                        iconRect.x -= ICON_SIZE; // 여러 아이콘이 겹치지 않게 왼쪽으로 이동
                    }
                }
            }
        }
    }
}
#endif