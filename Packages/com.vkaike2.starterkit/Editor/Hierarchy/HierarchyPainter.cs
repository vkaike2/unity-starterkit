using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vkaike2.StarterKit.Editor.Hierarchy
{
    [InitializeOnLoad]
    public static class HierarchyPainter
    {
        private const float SelectionLightenAmount = 0.25f;
        private const float InactiveTextFadeAmount = 0.5f;
        private const float InactiveIconAlpha = 0.5f;
        private const float IconSize = 16f;
        private const float LabelLeftPadding = 2f;

        private static readonly List<HierarchyPaintRule> Rules = new()
        {
            new HierarchyPaintRule("Manager", new Color32(20, 40, 90, 255), Color.white),
            new HierarchyPaintRule("Canvas", new Color32(150, 30, 30, 255), Color.white),
        };

        private static GUIStyle _labelStyle;

        static HierarchyPainter()
        {
#if UNITY_6000_5_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyWindowItemOnGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
#endif
        }

#if UNITY_6000_5_OR_NEWER
        private static void OnHierarchyWindowItemOnGUI(EntityId entityId, Rect selectionRect)
        {
            Paint(EditorUtility.EntityIdToObject(entityId) as GameObject, selectionRect);
        }
#else
        private static void OnHierarchyWindowItemOnGUI(int instanceId, Rect selectionRect)
        {
            Paint(EditorUtility.InstanceIDToObject(instanceId) as GameObject, selectionRect);
        }
#endif

        private static void Paint(GameObject gameObject, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (gameObject == null) return;
            if (!TryFindRule(gameObject.name, out var rule)) return;

            var background = Selection.Contains(gameObject)
                ? Color.Lerp(rule.BackgroundColor, Color.white, SelectionLightenAmount)
                : rule.BackgroundColor;

            EditorGUI.DrawRect(selectionRect, background);

            DrawIcon(gameObject, selectionRect);

            var labelOffset = IconSize + LabelLeftPadding;
            var labelRect = new Rect(selectionRect.x + labelOffset, selectionRect.y, selectionRect.width - labelOffset, selectionRect.height);
            var labelStyle = GetLabelStyle();
            labelStyle.normal.textColor = gameObject.activeInHierarchy
                ? rule.TextColor
                : Color.Lerp(rule.TextColor, background, InactiveTextFadeAmount);
            labelStyle.fontStyle = PrefabUtility.IsAnyPrefabInstanceRoot(gameObject) ? FontStyle.Bold : FontStyle.Normal;

            GUI.Label(labelRect, gameObject.name, labelStyle);
        }

        private static void DrawIcon(GameObject gameObject, Rect selectionRect)
        {
            var icon = EditorGUIUtility.ObjectContent(gameObject, typeof(GameObject)).image;
            if (icon == null) return;

            var iconRect = new Rect(selectionRect.x, selectionRect.y, IconSize, selectionRect.height);
            var previousColor = GUI.color;
            GUI.color = gameObject.activeInHierarchy ? previousColor : new Color(1f, 1f, 1f, InactiveIconAlpha);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            GUI.color = previousColor;
        }

        private static GUIStyle GetLabelStyle()
        {
            return _labelStyle ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
            };
        }

        private static bool TryFindRule(string gameObjectName, out HierarchyPaintRule rule)
        {
            foreach (var candidate in Rules)
            {
                if (!candidate.Matches(gameObjectName)) continue;

                rule = candidate;
                return true;
            }

            rule = default;
            return false;
        }
    }
}
