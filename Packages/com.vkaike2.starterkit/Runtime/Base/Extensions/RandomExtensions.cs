using System;
using System.Collections.Generic;

namespace Vkaike2.StarterKit.Base.Extensions
{
    public static class RandomExtensions
    {
        public static T GetRandom<T>(this List<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(
                    nameof(list),
                    $"{nameof(GetRandom)}<{typeof(T).Name}> was called on a null list.");
            }

            if (list.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{nameof(GetRandom)}<{typeof(T).Name}> was called on an empty list, " +
                    $"so there is nothing to pick.");
            }

            return list[UnityEngine.Random.Range(0, list.Count)];
        }
    }
}
