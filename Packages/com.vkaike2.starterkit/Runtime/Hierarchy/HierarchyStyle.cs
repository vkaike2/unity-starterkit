using System;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;

namespace Vkaike2.StarterKit.Hierarchy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("StarterKit/Hierarchy Style")]
    public class HierarchyStyle : MonoBehaviour
    {
        [SerializeField] private Colors _style = new();
        [SerializeField] private bool _applyToChildren;
        [SerializeField, HideIf(nameof(_applyToChildren), false)] private Colors _childrenStyle = new();

        public Colors Style => _style;

        public Colors ChildrenStyle => _childrenStyle;

        public bool ApplyToChildren
        {
            get => _applyToChildren;
            set => _applyToChildren = value;
        }

        [Serializable]
        public class Colors
        {
            [SerializeField] private Color _backgroundColor = new Color32(20, 40, 90, 255);
            [SerializeField] private Color _textColor = Color.white;

            public Color BackgroundColor
            {
                get => _backgroundColor;
                set => _backgroundColor = value;
            }

            public Color TextColor
            {
                get => _textColor;
                set => _textColor = value;
            }
        }
    }
}
