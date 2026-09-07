using System.Collections.Generic;
using UnityEngine;

namespace Vkaike2.StarterKit.Base.Utils
{
    public static class RayCastUtils
    {
        public static List<T> GetComponentsAtPosition<T>(Vector2 worldPosition) where T : class
        {
            var components = new List<T>();
            var uniqueComponents = new HashSet<T>();

            foreach (var hit in Physics2D.RaycastAll(worldPosition, Vector2.zero))
            {
                if (hit.collider == null) continue;

                Collect(hit.collider.GetComponentsInParent<T>(), components, uniqueComponents);
                Collect(hit.collider.GetComponentsInChildren<T>(), components, uniqueComponents);
            }

            return components;
        }

        private static void Collect<T>(T[] found, List<T> components, HashSet<T> uniqueComponents)
            where T : class
        {
            foreach (var component in found)
            {
                if (component == null) continue;
                if (!uniqueComponents.Add(component)) continue;

                components.Add(component);
            }
        }
    }
}
