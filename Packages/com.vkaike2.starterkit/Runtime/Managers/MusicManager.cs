using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Vkaike2.StarterKit.Base.Abstracts;
using Vkaike2.StarterKit.Enums;
using Vkaike2.StarterKit.ScriptableObjects;

namespace Vkaike2.StarterKit.Managers
{
    public class MusicManager : MySingleton<MusicManager>
    {
        [SerializeField] private Configurations _configurations;
        [SerializeField] private Components _components;

        private const float MinimumVolume = 0.0001f;
        private const float MinimumDecibels = -80f;

        private readonly List<AudioSource> _soundEffectSources = new();
        private readonly Dictionary<AudioSource, SoAudioTrack> _playingSoundEffects = new();

        private SoAudioTrack _playingMusic;

        private int _nextSoundEffectSource;

        private void OnValidate()
        {
            _configurations.ValidateFields(this);
            _components.ValidateFields(this);
        }

        protected override async Awaitable OnLoad()
        {
            CreateSoundEffectSources();
        }

        public void Play(SoAudioTrack track, AudioChannel channel)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));

            var clip = track.GetClip();

            if (clip == null)
            {
                Debug.LogWarning($"[{nameof(MusicManager)}] '{track.name}' has no clip to play.", track);
                return;
            }

            switch (channel)
            {
                case AudioChannel.Music:
                    PlayMusic(track, clip);
                    break;
                case AudioChannel.SoundEffect:
                    PlaySoundEffect(track, clip);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(channel), channel, $"Unhandled {nameof(AudioChannel)}.");
            }
        }

        public void ChangeVolume(VolumeChannel channel, float volume)
        {
            switch (channel)
            {
                case VolumeChannel.Master:
                case VolumeChannel.Music:
                case VolumeChannel.SoundEffect:
                    SetVolume(_configurations.GetVolumeParameter(channel), volume);
                    break;
                case VolumeChannel.All:
                    SetVolume(_configurations.MasterVolumeParameter, volume);
                    SetVolume(_configurations.MusicVolumeParameter, volume);
                    SetVolume(_configurations.SoundEffectVolumeParameter, volume);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(channel), channel, $"Unhandled {nameof(VolumeChannel)}.");
            }
        }

        private void SetVolume(string parameter, float volume)
        {
            if (_components.Mixer.SetFloat(parameter, ToDecibels(volume))) return;

            Debug.LogWarning(
                $"[{nameof(MusicManager)}] '{parameter}' is not exposed on {_components.Mixer.name}.",
                _components.Mixer);
        }

        private static float ToDecibels(float volume)
        {
            volume = Mathf.Clamp01(volume);

            return volume <= MinimumVolume ? MinimumDecibels : Mathf.Log10(volume) * 20f;
        }

        public void Stop(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music:
                    StopMusic();
                    break;
                case AudioChannel.SoundEffect:
                    foreach (var source in _soundEffectSources) StopSoundEffect(source);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(channel), channel, $"Unhandled {nameof(AudioChannel)}.");
            }
        }

        public void Stop(SoAudioTrack track)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));

            if (_playingMusic == track) StopMusic();

            foreach (var source in _soundEffectSources)
            {
                if (!_playingSoundEffects.TryGetValue(source, out var playing) || playing != track) continue;

                StopSoundEffect(source);
            }
        }

        private void StopMusic()
        {
            _components.MusicSource.Stop();
            _playingMusic = null;
        }

        private void StopSoundEffect(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.loop = false;

            _playingSoundEffects.Remove(source);
        }

        private void PlayMusic(SoAudioTrack track, AudioClip clip)
        {
            var source = _components.MusicSource;

            source.clip = clip;
            source.volume = track.Volume;
            source.pitch = track.GetPitch();
            source.loop = track.Loop;
            source.Play();

            _playingMusic = track;
        }

        private void PlaySoundEffect(SoAudioTrack track, AudioClip clip)
        {
            var source = GetSoundEffectSource();

            if (source == null)
            {
                Debug.LogWarning(
                    $"[{nameof(MusicManager)}] '{track.name}' was not played, the pool is empty.", this);
                return;
            }

            StopSoundEffect(source);

            source.pitch = track.GetPitch();

            if (track.Loop)
            {
                source.clip = clip;
                source.volume = track.Volume;
                source.loop = true;
                source.Play();
            }
            else
            {
                source.PlayOneShot(clip, track.Volume);
            }

            _playingSoundEffects[source] = track;
        }

        private AudioSource GetSoundEffectSource()
        {
            if (_soundEffectSources.Count == 0) return null;

            foreach (var source in _soundEffectSources)
            {
                if (source.isPlaying) continue;

                _playingSoundEffects.Remove(source);
                return source;
            }

            var stolen = _soundEffectSources[_nextSoundEffectSource];
            _nextSoundEffectSource = (_nextSoundEffectSource + 1) % _soundEffectSources.Count;

            return stolen;
        }

        private void CreateSoundEffectSources()
        {
            for (var index = _soundEffectSources.Count; index < _configurations.SoundEffectPoolSize; index++)
            {
                var host = new GameObject($"{nameof(AudioSource)} {index}");
                host.transform.SetParent(transform, worldPositionStays: false);

                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.outputAudioMixerGroup = _components.SoundEffectGroup;

                _soundEffectSources.Add(source);
            }
        }

        [Serializable]
        private class Configurations : ValidatableFields
        {
            [field: SerializeField] public int SoundEffectPoolSize { get; set; } = 8;

            [field: SerializeField] public string MasterVolumeParameter { get; set; } = "Master_Volume";
            [field: SerializeField] public string MusicVolumeParameter { get; set; } = "Music_Volume";
            [field: SerializeField] public string SoundEffectVolumeParameter { get; set; } = "SoundEffect_Volume";

            public string GetVolumeParameter(VolumeChannel channel) => channel switch
            {
                VolumeChannel.Master => MasterVolumeParameter,
                VolumeChannel.Music => MusicVolumeParameter,
                VolumeChannel.SoundEffect => SoundEffectVolumeParameter,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(channel), channel, $"{channel} has no single volume parameter."),
            };
        }

        [Serializable]
        private class Components : ValidatableFields
        {
            [field: SerializeField] public AudioSource MusicSource { get; set; }
            [field: SerializeField] public AudioListener AudioListener { get; set; }

            [field: SerializeField] public AudioMixer Mixer { get; set; }
            [field: SerializeField] public AudioMixerGroup SoundEffectGroup { get; set; }

            protected override void Validate()
            {
                ValidateNull(MusicSource, nameof(MusicSource));
                ValidateNull(AudioListener, nameof(AudioListener));
                ValidateNull(Mixer, nameof(Mixer));
                ValidateNull(SoundEffectGroup, nameof(SoundEffectGroup));
            }
        }
    }
}
