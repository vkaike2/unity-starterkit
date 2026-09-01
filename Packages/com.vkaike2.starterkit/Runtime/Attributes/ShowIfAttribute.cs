using System;
using UnityEngine;

namespace Vkaike2.StarterKit.Attributes
{
    public enum ShowIfMode
    {
        Hide = 0,
        Disable = 1,
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public string ConditionName { get; }

        public object ExpectedValue { get; }

        public bool Invert { get; }

        public ShowIfMode Mode { get; set; } = ShowIfMode.Hide;

        public ShowIfAttribute(string conditionName)
            : this(conditionName, null, false)
        {
        }

        public ShowIfAttribute(string conditionName, object expectedValue)
            : this(conditionName, expectedValue, false)
        {
        }

        private protected ShowIfAttribute(string conditionName, object expectedValue, bool invert)
        {
            ConditionName = conditionName;
            ExpectedValue = expectedValue;
            Invert = invert;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class HideIfAttribute : ShowIfAttribute
    {
        public HideIfAttribute(string conditionName)
            : base(conditionName, null, true)
        {
        }

        public HideIfAttribute(string conditionName, object expectedValue)
            : base(conditionName, expectedValue, true)
        {
        }
    }
}
