using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;

namespace Vkaike2.StarterKit.Editor.Inspectors
{
    public static class InspectorButtons
    {
        private const BindingFlags Lookup = BindingFlags.Instance | BindingFlags.Static |
                                            BindingFlags.Public | BindingFlags.NonPublic |
                                            BindingFlags.DeclaredOnly;

        private const float HeaderLines = 1.5f;

        private class Entry
        {
            public MethodInfo Method;
            public ButtonAttribute Attribute;
            public GUIContent Content;
        }

        private static readonly Dictionary<Type, Entry[]> Cache = new();

        public static void Draw(UnityEditor.Editor editor)
        {
            if (editor == null || editor.target == null) return;

            Entry[] entries = GetEntries(editor.target.GetType());

            if (entries.Length == 0) return;

            foreach (Entry entry in entries)
            {
                DrawEntry(editor, entry);
            }
        }

        private static void DrawEntry(UnityEditor.Editor editor, Entry entry)
        {
            DrawDecoration(entry.Attribute);

            if (entry.Method.GetParameters().Length > 0)
            {
                EditorGUILayout.HelpBox(
                    $"[Button] '{entry.Method.Name}' takes parameters. Only parameterless methods " +
                    "can be exposed as a button.",
                    MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(!IsEnabled(entry.Attribute.Mode)))
            {
                bool clicked = entry.Attribute.Height > 0f
                    ? GUILayout.Button(entry.Content, GUILayout.Height(entry.Attribute.Height))
                    : GUILayout.Button(entry.Content);

                if (clicked)
                {
                    Invoke(editor, entry);
                }
            }
        }

        private static void DrawDecoration(ButtonAttribute attribute)
        {
            if (attribute.SpaceBefore > 0f)
            {
                GUILayout.Space(attribute.SpaceBefore);
            }

            if (!string.IsNullOrEmpty(attribute.Header))
            {
                GUILayout.Label(attribute.Header, EditorStyles.boldLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight * HeaderLines));
            }
        }

        private static void Invoke(UnityEditor.Editor editor, Entry entry)
        {
            editor.serializedObject.ApplyModifiedProperties();

            if (entry.Method.IsStatic)
            {
                Call(entry, null);
                editor.serializedObject.Update();
                return;
            }

            foreach (UnityEngine.Object target in editor.targets)
            {
                if (target == null) continue;

                Undo.RecordObject(target, entry.Content.text);
                Call(entry, target);
                EditorUtility.SetDirty(target);
            }

            editor.serializedObject.Update();
        }

        private static void Call(Entry entry, UnityEngine.Object target)
        {
            try
            {
                object result = entry.Method.Invoke(target, null);

                if (result is null or Awaitable) return;

                Debug.Log($"{entry.Content.text} returned {result}", target);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception, target);
            }
        }

        private static bool IsEnabled(ButtonMode mode)
        {
            return mode switch
            {
                ButtonMode.PlayModeOnly => EditorApplication.isPlaying,
                ButtonMode.EditModeOnly => !EditorApplication.isPlaying,
                _ => true,
            };
        }

        private static Entry[] GetEntries(Type type)
        {
            if (Cache.TryGetValue(type, out Entry[] cached)) return cached;

            var entries = new List<Entry>();
            var claimed = new HashSet<string>();

            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (MethodInfo method in current.GetMethods(Lookup))
                {
                    var attribute = method.GetCustomAttribute<ButtonAttribute>(true);

                    if (attribute == null || !claimed.Add(method.Name)) continue;

                    entries.Add(new Entry
                    {
                        Method = method,
                        Attribute = attribute,
                        Content = new GUIContent(attribute.Label ?? ObjectNames.NicifyVariableName(method.Name)),
                    });
                }
            }

            Entry[] ordered = entries
                .OrderBy(entry => entry.Attribute.Order)
                .ToArray();

            Cache[type] = ordered;
            return ordered;
        }
    }
}
