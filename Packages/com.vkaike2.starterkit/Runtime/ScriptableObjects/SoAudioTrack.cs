using System;
using System.Collections.Generic;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;
using Vkaike2.StarterKit.Base.Extensions;
using Vkaike2.StarterKit.Base.Models;

namespace Vkaike2.StarterKit.ScriptableObjects
{
    [CreateAssetMenu(fileName = "So Audio Track", menuName = "StarterKit/Audio Track")]
    public class SoAudioTrack : ScriptableObject
    {
        public const float DefaultPitch = 1f;

        [SerializeField] private SelectionMode _clipMode;

        [SerializeField, ShowIf(nameof(_clipMode), SelectionMode.Unique)]
        private AudioClip _clip;

        [SerializeField, ShowIf(nameof(_clipMode), SelectionMode.Random)]
        private RandomClips _randomClips;

        [SerializeField, Header("Pitch")] private SelectionMode _pitchMode;

        [SerializeField, ShowIf(nameof(_pitchMode), SelectionMode.Unique)]
        private UniquePitch _pitch;

        [SerializeField, ShowIf(nameof(_pitchMode), SelectionMode.Random)]
        private PitchRange _pitchRange;

        [field: SerializeField, Range(0f, 1f), Header("Output")] public float Volume { get; private set; } = 1f;
        [field: SerializeField] public bool Loop { get; private set; }

        private const float MinimumPitch = -3f;
        private const float MaximumPitch = 3f;

        public AudioClip GetClip()
        {
            return _clipMode == SelectionMode.Unique ? _clip : _randomClips.GetRandomClip();
        }

        public float GetPitch()
        {
            return _pitchMode == SelectionMode.Unique ? _pitch.Value : _pitchRange.GetRandomPitch();
        }

        private void OnValidate()
        {
            _randomClips.RefreshNames();
            _pitchRange.KeepRangeOrdered();
        }

        public enum SelectionMode
        {
            Unique,

            Random,
        }

        [Serializable]
        public class RandomClips
        {
            [field: SerializeField] public List<ProbabilityModel<AudioClip>> Clips { get; private set; } = new();

            public AudioClip GetRandomClip()
            {
                var (hasClip, clip) = Clips.TryGetRandomByProbability();

                return hasClip ? clip : null;
            }

            public void RefreshNames()
            {
                foreach (var clip in Clips)
                {
                    clip.RefreshName();
                }
            }
        }

        [Serializable]
        public class UniquePitch
        {
            [field: SerializeField, Range(MinimumPitch, MaximumPitch)]
            public float Value { get; private set; } = DefaultPitch;
        }

        [Serializable]
        public class PitchRange
        {
            [field: SerializeField, Range(MinimumPitch, MaximumPitch)]
            public float Minimum { get; private set; } = DefaultPitch;

            [field: SerializeField, Range(MinimumPitch, MaximumPitch)]
            public float Maximum { get; private set; } = DefaultPitch;

            public float GetRandomPitch()
            {
                return UnityEngine.Random.Range(Minimum, Maximum);
            }

            public void KeepRangeOrdered()
            {
                if (Maximum >= Minimum) return;

                Maximum = Minimum;
            }
        }
    }
}
