#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ToggleTool.Global;

namespace ToggleTool.Editor
{
    public class ComponentNameWindow : EditorWindow
    {
        private string componentName;

        public static string OpenComponentNameDialog()
        {
            var window = CreateInstance<ComponentNameWindow>();
            window.titleContent = new GUIContent(Messages.COMPONENT_NAME_WINDOW_DIALOG_TITLE);
            window.minSize = new Vector2(300, 100); // Adjust window size to fit content
            window.ShowModal();
            return window.componentName;
        }

        private void OnGUI()
        {
            // Custom GUIStyle for labels
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.fontSize = 12;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            // Custom GUIStyle for text fields
            GUIStyle textFieldStyle = new GUIStyle(EditorStyles.textField);
            textFieldStyle.fontSize = 12;

            GUILayout.Space(10); // Add space at the top

            // Centered label
            GUILayout.Label(Messages.COMPONENT_NAME_WINDOW_DIALOG_CONTENT, labelStyle);

            GUILayout.Space(10); // Add space between label and text field

            // Centered text field with padding
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // Center the text field horizontally
            componentName = EditorGUILayout.TextField(Components.DEFAULT_COMPONENT_NAME, textFieldStyle, GUILayout.Width(200)); // Adjust the width
            GUILayout.FlexibleSpace(); // Center the text field horizontally
            GUILayout.EndHorizontal();

            GUILayout.Space(20); // Add space before buttons

            // Centered button with custom width
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // Push button to the center
            if (GUILayout.Button(Messages.DIALOG_BUTTON_OK, GUILayout.Width(100)))
            {
                Close(); // Close the window
            }
            GUILayout.FlexibleSpace(); // Push button to the center
            GUILayout.EndHorizontal();
        }
    }
}
#endif