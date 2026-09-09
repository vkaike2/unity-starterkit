using System;
using System.Collections.Generic;
using Vkaike2.StarterKit.Base.Models;

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

        public static (bool HasValue, T Value) TryGetRandomByProbability<T>(
            this List<ProbabilityModel<T>> models)
        {
            if (models == null)
            {
                throw new ArgumentNullException(
                    nameof(models),
                    $"{nameof(TryGetRandomByProbability)}<{typeof(T).Name}> was called on a null list.");
            }

            var totalWeight = 0f;

            foreach (var model in models)
            {
                if (!model.IsSelectable) continue;

                totalWeight += model.Weight;
            }

            if (totalWeight <= 0f) return (false, default);

            var roll = UnityEngine.Random.value * totalWeight;

            foreach (var model in models)
            {
                if (!model.IsSelectable) continue;

                roll -= model.Weight;

                if (roll > 0f) continue;

                return (true, model.Value);
            }

            foreach (var model in models)
            {
                if (!model.IsSelectable) continue;

                return (true, model.Value);
            }

            return (false, default);
        }
    }
}
