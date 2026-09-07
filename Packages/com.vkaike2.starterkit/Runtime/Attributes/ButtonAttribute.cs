using System;

namespace Vkaike2.StarterKit.Attributes
{
    public enum ButtonMode
    {
        Always = 0,
        PlayModeOnly = 1,
        EditModeOnly = 2,
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ButtonAttribute : Attribute
    {
        public string Label { get; }

        public ButtonMode Mode { get; set; } = ButtonMode.Always;

        public float Height { get; set; }

        public int Order { get; set; }

        public string Header { get; set; }

        public float SpaceBefore { get; set; }

        public ButtonAttribute()
            : this(null)
        {
        }

        public ButtonAttribute(string label)
        {
            Label = label;
        }
    }
}
