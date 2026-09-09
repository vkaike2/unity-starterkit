using UnityEditor;
using UnityEngine;
using Vkaike2.StarterKit.Hierarchy;

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
            if (!TryResolveColors(gameObject, out var colors)) return;
            if (colors.BackgroundColor.a <= 0f) return;

            var background = Selection.Contains(gameObject)
                ? Color.Lerp(colors.BackgroundColor, Color.white, SelectionLightenAmount)
                : colors.BackgroundColor;

            EditorGUI.DrawRect(selectionRect, background);

            DrawIcon(gameObject, selectionRect);

            var labelOffset = IconSize + LabelLeftPadding;
            var labelRect = new Rect(selectionRect.x + labelOffset, selectionRect.y, selectionRect.width - labelOffset, selectionRect.height);
            var labelStyle = GetLabelStyle();
            labelStyle.normal.textColor = gameObject.activeInHierarchy
                ? colors.TextColor
                : Color.Lerp(colors.TextColor, background, InactiveTextFadeAmount);
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

        private static bool TryResolveColors(GameObject gameObject, out HierarchyStyle.Colors colors)
        {
            var own = gameObject.GetComponent<HierarchyStyle>();
            if (own != null)
            {
                colors = own.Style;
                return true;
            }

            for (var parent = gameObject.transform.parent; parent != null; parent = parent.parent)
            {
                var inherited = parent.GetComponent<HierarchyStyle>();
                if (inherited == null || !inherited.ApplyToChildren) continue;

                colors = inherited.ChildrenStyle;
                return true;
            }

            colors = null;
            return false;
        }
    }
}
