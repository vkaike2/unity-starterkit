using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;

namespace Vkaike2.StarterKit.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute), true)]
    public class ShowIfDrawer : PropertyDrawer
    {
        private const float HelpBoxLines = 2f;
        private const float HeaderLines = 1.5f;

        private enum Visibility
        {
            Visible = 0,
            Disabled = 1,
            Hidden = 2,
        }

        private ShowIfAttribute[] _attributes;
        private GUIContent _headerContent;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Visibility visibility = Evaluate(property, out string missingCondition);

            if (missingCondition != null)
            {
                return DecorationHeight() + HelpBoxHeight() + EditorGUIUtility.standardVerticalSpacing +
                       EditorGUI.GetPropertyHeight(property, label, true);
            }

            if (visibility == Visibility.Hidden)
            {
                return -EditorGUIUtility.standardVerticalSpacing;
            }

            return DecorationHeight() + EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Visibility visibility = Evaluate(property, out string missingCondition);

            if (missingCondition == null && visibility == Visibility.Hidden) return;

            label = new GUIContent(label);
            position.yMin += DrawDecoration(position);

            if (missingCondition != null)
            {
                var helpRect = new Rect(position.x, position.y, position.width, HelpBoxHeight());
                EditorGUI.HelpBox(
                    helpRect,
                    $"[{attribute.GetType().Name}] No serialized field named '{missingCondition}' " +
                    $"was found next to '{property.name}'. Only serialized fields can be a condition.",
                    MessageType.Warning);

                position.yMin = helpRect.yMax + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            switch (visibility)
            {
                case Visibility.Visible:
                    EditorGUI.PropertyField(position, property, label, true);
                    break;

                case Visibility.Disabled:
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.PropertyField(position, property, label, true);
                    }

                    break;
            }
        }

        private Visibility Evaluate(SerializedProperty property, out string missingCondition)
        {
            missingCondition = null;
            var visibility = Visibility.Visible;

            foreach (ShowIfAttribute showIf in GetAttributes())
            {
                SerializedProperty condition = FindConditionProperty(property, showIf.ConditionName);

                if (condition == null)
                {
                    missingCondition ??= showIf.ConditionName;
                    continue;
                }

                if (IsSatisfied(condition, showIf)) continue;

                if (showIf.Mode == ShowIfMode.Hide) return Visibility.Hidden;

                visibility = Visibility.Disabled;
            }

            return visibility;
        }

        private ShowIfAttribute[] GetAttributes()
        {
            if (_attributes != null) return _attributes;

            ShowIfAttribute[] declared = fieldInfo?
                .GetCustomAttributes<ShowIfAttribute>(true)
                .ToArray();

            _attributes = declared is { Length: > 0 }
                ? declared
                : new[] { (ShowIfAttribute)attribute };

            return _attributes;
        }

        private ShowIfAttribute GetDecoration()
        {
            return GetAttributes()
                .FirstOrDefault(showIf => !string.IsNullOrEmpty(showIf.Header) || showIf.SpaceBefore > 0f);
        }

        private float DecorationHeight()
        {
            ShowIfAttribute decoration = GetDecoration();

            if (decoration == null) return 0f;

            return decoration.SpaceBefore + (string.IsNullOrEmpty(decoration.Header)
                ? 0f
                : EditorGUIUtility.singleLineHeight * HeaderLines);
        }

        private float DrawDecoration(Rect position)
        {
            ShowIfAttribute decoration = GetDecoration();
            float height = DecorationHeight();

            if (decoration == null || string.IsNullOrEmpty(decoration.Header)) return height;

            var headerRect = new Rect(
                position.x,
                position.y + decoration.SpaceBefore + EditorGUIUtility.singleLineHeight * (HeaderLines - 1f),
                position.width,
                EditorGUIUtility.singleLineHeight);

            _headerContent ??= new GUIContent(decoration.Header);
            EditorGUI.LabelField(headerRect, _headerContent, EditorStyles.boldLabel);

            return height;
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
