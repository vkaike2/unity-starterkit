using System;
using UnityEngine;

namespace Vkaike2.StarterKit.Editor.Hierarchy
{
    public readonly struct HierarchyPaintRule
    {
        public HierarchyPaintRule(string keyword, Color backgroundColor, Color textColor)
        {
            Keyword = keyword;
            BackgroundColor = backgroundColor;
            TextColor = textColor;
        }

        public string Keyword { get; }
        public Color BackgroundColor { get; }
        public Color TextColor { get; }

        public bool Matches(string gameObjectName)
        {
            return !string.IsNullOrEmpty(gameObjectName)
                   && gameObjectName.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
