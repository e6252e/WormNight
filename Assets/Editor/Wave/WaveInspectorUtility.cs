using System;
using UnityEditor;
using UnityEngine;

namespace TeamProject01.Gameplay.EditorTools
{
    internal static class WaveInspectorUtility
    {
        public static void DrawScriptField(UnityEngine.Object target)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                MonoScript script = MonoScript.FromMonoBehaviour((MonoBehaviour)target);
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            }
        }

        public static void DrawSection(string title, string helpText = null)
        {
            EditorGUILayout.Space(10.0f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(helpText))
            {
                EditorGUILayout.HelpBox(helpText, MessageType.Info);
            }
        }

        public static void DrawProperty(SerializedObject serializedObject, string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                EditorGUILayout.HelpBox($"{propertyName} 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        public static void DrawPercentProperty(SerializedProperty property, string label)
        {
            if (property == null)
            {
                EditorGUILayout.HelpBox($"{label} 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            int value = Mathf.RoundToInt(property.intValue);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                value = EditorGUILayout.IntSlider(value, 0, 100);
                EditorGUILayout.LabelField("%", GUILayout.Width(16.0f));
            }

            property.intValue = Mathf.Clamp(value, 0, 100);
        }

        public static void DrawArray(
            SerializedProperty array,
            string title,
            Func<SerializedProperty, int, string> getLabel,
            Action<SerializedProperty> drawBody,
            string addButtonText,
            string removeButtonText)
        {
            if (array == null)
            {
                EditorGUILayout.HelpBox($"{title} 항목을 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            array.isExpanded = EditorGUILayout.Foldout(array.isExpanded, $"{title} ({array.arraySize})", true);

            if (array.isExpanded)
            {
                EditorGUI.indentLevel++;

                for (int i = 0; i < array.arraySize; i++)
                {
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    string label = getLabel != null ? getLabel(element, i) : $"항목 {i + 1}";

                    element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, label, true);

                    if (element.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        drawBody?.Invoke(element);
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUI.indentLevel--;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(addButtonText))
                {
                    array.arraySize++;
                }

                using (new EditorGUI.DisabledScope(array.arraySize <= 0))
                {
                    if (GUILayout.Button(removeButtonText))
                    {
                        array.DeleteArrayElementAtIndex(array.arraySize - 1);
                    }
                }
            }
        }

        public static string GetIdNameLabel(SerializedProperty element, string idField, string nameField, int index)
        {
            SerializedProperty id = element.FindPropertyRelative(idField);
            SerializedProperty name = element.FindPropertyRelative(nameField);
            string idValue = id != null ? id.stringValue : string.Empty;
            string nameValue = name != null ? name.stringValue : string.Empty;

            if (!string.IsNullOrWhiteSpace(idValue) && !string.IsNullOrWhiteSpace(nameValue))
            {
                return $"{idValue} - {nameValue}";
            }

            if (!string.IsNullOrWhiteSpace(idValue))
            {
                return idValue;
            }

            if (!string.IsNullOrWhiteSpace(nameValue))
            {
                return nameValue;
            }

            return $"항목 {index + 1}";
        }
    }
}
