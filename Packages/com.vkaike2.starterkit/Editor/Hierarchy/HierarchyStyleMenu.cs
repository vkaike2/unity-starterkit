using UnityEditor;
using UnityEngine;
using Vkaike2.StarterKit.Hierarchy;

namespace Vkaike2.StarterKit.Editor.Hierarchy
{
    public static class HierarchyStyleMenu
    {
        private const string MenuRoot = "GameObject/Hierarchy Style/";
        private const int PresetPriority = 20;
        private const int ClearPriority = 40;

        [MenuItem(MenuRoot + "Blue", false, PresetPriority)]
        private static void ApplyBlue() => Apply(new Color32(20, 40, 90, 255), Color.white);

        [MenuItem(MenuRoot + "Red", false, PresetPriority)]
        private static void ApplyRed() => Apply(new Color32(150, 30, 30, 255), Color.white);

        [MenuItem(MenuRoot + "Green", false, PresetPriority)]
        private static void ApplyGreen() => Apply(new Color32(25, 80, 45, 255), Color.white);

        [MenuItem(MenuRoot + "Purple", false, PresetPriority)]
        private static void ApplyPurple() => Apply(new Color32(70, 35, 100, 255), Color.white);

        [MenuItem(MenuRoot + "Orange", false, PresetPriority)]
        private static void ApplyOrange() => Apply(new Color32(140, 70, 20, 255), Color.white);

        [MenuItem(MenuRoot + "Grey", false, PresetPriority)]
        private static void ApplyGrey() => Apply(new Color32(60, 60, 60, 255), Color.white);

        [MenuItem(MenuRoot + "Blue", true)]
        [MenuItem(MenuRoot + "Red", true)]
        [MenuItem(MenuRoot + "Green", true)]
        [MenuItem(MenuRoot + "Purple", true)]
        [MenuItem(MenuRoot + "Orange", true)]
        [MenuItem(MenuRoot + "Grey", true)]
        private static bool HasSelection() => Selection.gameObjects.Length > 0;

        [MenuItem(MenuRoot + "Clear", false, ClearPriority)]
        private static void Clear()
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                var style = gameObject.GetComponent<HierarchyStyle>();
                if (style == null) continue;

                Undo.DestroyObjectImmediate(style);
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(MenuRoot + "Clear", true)]
        private static bool HasStyledSelection()
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject.GetComponent<HierarchyStyle>() != null) return true;
            }

            return false;
        }

        private static void Apply(Color backgroundColor, Color textColor)
        {
            foreach (var gameObject in Selection.gameObjects)
            {
                var style = gameObject.GetComponent<HierarchyStyle>();
                if (style == null)
                {
                    style = Undo.AddComponent<HierarchyStyle>(gameObject);
                }
                else
                {
                    Undo.RecordObject(style, "Apply Hierarchy Style");
                }

                style.Style.BackgroundColor = backgroundColor;
                style.Style.TextColor = textColor;

                EditorUtility.SetDirty(style);
            }

            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
