using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vkaike2.StarterKit.Base.Models
{
    [Serializable]
    public class ProbabilityModel<T>
    {
        [HideInInspector] public string name;

        [field: SerializeField] public T Value { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float Probability { get; private set; } = 1f;

        public float Weight => Mathf.Max(0f, Probability);

        public bool HasValue => Value switch
        {
            null => false,
            Object unityObject => unityObject != null,
            _ => true,
        };

        public bool IsSelectable => HasValue && Weight > 0f;

        public void RefreshName()
        {
            name = HasValue
                ? $"{(Value is Object unityObject ? unityObject.name : Value.ToString())} ({Probability:P0})"
                : "Empty";
        }
    }
}
