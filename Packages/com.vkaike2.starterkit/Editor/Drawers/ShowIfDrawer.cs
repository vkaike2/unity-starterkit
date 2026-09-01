using System;
using UnityEditor;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;

namespace Vkaike2.StarterKit.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute), true)]
    public class ShowIfDrawer : PropertyDrawer
    {
        private const float HelpBoxLines = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var showIf = (ShowIfAttribute)attribute;
            SerializedProperty condition = FindConditionProperty(property, showIf.ConditionName);

            if (condition == null)
            {
                return HelpBoxHeight() + EditorGUIUtility.standardVerticalSpacing +
                       EditorGUI.GetPropertyHeight(property, label, true);
            }

            if (IsSatisfied(condition, showIf) || showIf.Mode == ShowIfMode.Disable)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            return -EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var showIf = (ShowIfAttribute)attribute;
            SerializedProperty condition = FindConditionProperty(property, showIf.ConditionName);

            if (condition == null)
            {
                var helpRect = new Rect(position.x, position.y, position.width, HelpBoxHeight());
                EditorGUI.HelpBox(
                    helpRect,
                    $"[{showIf.GetType().Name}] No serialized field named '{showIf.ConditionName}' " +
                    $"was found next to '{property.name}'. Only serialized fields can be a condition.",
                    MessageType.Warning);

                position.yMin = helpRect.yMax + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            if (IsSatisfied(condition, showIf))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            if (showIf.Mode == ShowIfMode.Disable)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.PropertyField(position, property, label, true);
                }
            }
        }

        private static float HelpBoxHeight() => EditorGUIUtility.singleLineHeight * HelpBoxLines;

        private static bool IsSatisfied(SerializedProperty condition, ShowIfAttribute showIf)
        {
            if (condition.hasMultipleDifferentValues)
            {
                return true;
            }

            bool matches = Matches(condition, showIf.ExpectedValue);
            return showIf.Invert ? !matches : matches;
        }

        private static bool Matches(SerializedProperty condition, object expected)
        {
            switch (condition.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return condition.boolValue == AsBool(expected);

                case SerializedPropertyType.ObjectReference:
                    return (condition.objectReferenceValue != null) == AsBool(expected);

                case SerializedPropertyType.Enum:
                    return MatchesEnum(condition, expected);

                case SerializedPropertyType.Integer:
                    return expected == null
                        ? condition.longValue != 0L
                        : condition.longValue == Convert.ToInt64(expected);

                case SerializedPropertyType.Float:
                    return expected == null
                        ? Mathf.Abs(condition.floatValue) > Mathf.Epsilon
                        : Mathf.Approximately(condition.floatValue, Convert.ToSingle(expected));

                case SerializedPropertyType.String:
                    return expected == null
                        ? !string.IsNullOrEmpty(condition.stringValue)
                        : string.Equals(condition.stringValue, expected as string, StringComparison.Ordinal);

                default:
                    return true;
            }
        }

        private static bool MatchesEnum(SerializedProperty condition, object expected)
        {
            if (expected == null)
            {
                return condition.enumValueIndex != 0;
            }

            if (expected is string expectedName)
            {
                int index = condition.enumValueIndex;
                return index >= 0
                       && index < condition.enumNames.Length
                       && string.Equals(condition.enumNames[index], expectedName, StringComparison.Ordinal);
            }

            return condition.longValue == Convert.ToInt64(expected);
        }

        private static SerializedProperty FindConditionProperty(SerializedProperty property, string conditionName)
        {
            if (string.IsNullOrEmpty(conditionName))
            {
                return null;
            }

            string path = property.propertyPath;
            int lastSeparator = path.LastIndexOf('.');

            if (lastSeparator < 0)
            {
                return property.serializedObject.FindProperty(conditionName);
            }

            string parentPath = path.Substring(0, lastSeparator);

            if (parentPath.EndsWith(".Array", StringComparison.Ordinal) ||
                parentPath.Equals("Array", StringComparison.Ordinal))
            {
                return property.serializedObject.FindProperty(conditionName);
            }

            SerializedProperty parent = property.serializedObject.FindProperty(parentPath);

            return parent?.FindPropertyRelative(conditionName)
                   ?? property.serializedObject.FindProperty(conditionName);
        }

        private static bool AsBool(object expected) => expected is not bool value || value;
    }
}
