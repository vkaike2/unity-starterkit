using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Vkaike2.StarterKit.Attributes;

namespace Vkaike2.StarterKit.Components.Animations
{
    public partial class CustomAnimator2d
    {
        [Serializable]
        private class Clip
        {
            private const string TotalDurationKey = "t";
            private const string FrameKey = "f";
            private const char ValueSeparator = ':';
            private const string ConfigurationExample = "t:1 f0:0.4";

            [HideInInspector] public string name;

            [SerializeField] private string _clipName;

            [Tooltip("t:{total seconds} f{index}:{seconds} - frames with no duration split what is left "
                + "of the total. A single frame clip can leave this empty and just holds its sprite")]
            [SerializeField] private string _configuration;

            [SerializeField] private bool _itLoops;
            [SerializeField, HideIf(nameof(_itLoops))] private string _nextClip;
            [SerializeField] private List<Layer> _layers;

            private List<Frame> _frames;

            public string Name => _clipName;
            public bool IsStatic => FrameCount == 1 && string.IsNullOrWhiteSpace(_configuration);
            public bool ItLoops => _itLoops && !IsStatic;
            public string NextClip => _itLoops ? null : _nextClip;

            public IEnumerable<string> LayerNames => _layers == null
                ? Enumerable.Empty<string>()
                : _layers.Select(layer => layer.Name);

            private int FrameCount => _layers is { Count: > 0 } ? _layers[0].FrameCount : 0;

            public int GetLayerIndex(string layerName)
            {
                if (string.IsNullOrWhiteSpace(layerName) || _layers == null) return 0;

                for (var i = 0; i < _layers.Count; i++)
                {
                    if (string.Equals(_layers[i].Name, layerName, StringComparison.Ordinal)) return i;
                }

                return 0;
            }

            public void Validate(UnityEngine.Object context)
            {
                _frames = null;
                UpdateInspectorName();

                if (string.IsNullOrWhiteSpace(_clipName))
                {
                    LogError(context, "has a clip with no name.");
                }

                if (!_itLoops && !string.IsNullOrWhiteSpace(_nextClip)
                    && string.Equals(_clipName, _nextClip, StringComparison.Ordinal))
                {
                    LogError(context, $"clip '{_clipName}' chains into itself, use {nameof(_itLoops)} instead.");
                }

                if (IsStatic && !string.IsNullOrWhiteSpace(NextClip))
                {
                    LogError(context, $"clip '{_clipName}' has a single frame and no configuration, so it holds "
                        + $"its sprite and never reaches '{NextClip}'. Give it a total duration to chain.");
                }

                ValidateLayerNames(context);
                ValidateSprites(context);

                if (TryBuildFrames(out _, out var error)) return;

                LogError(context, $"clip '{name}' {error}");
            }

            public List<Frame> GetFrames(UnityEngine.Object context)
            {
                if (_frames != null) return _frames;

                if (TryBuildFrames(out _frames, out var error)) return _frames;

                LogError(context, $"clip '{name}' {error}");
                _frames = new List<Frame>();

                return _frames;
            }

            private void ValidateLayerNames(UnityEngine.Object context)
            {
                if (_layers is not { Count: > 1 }) return;

                var layerNames = new HashSet<string>();

                foreach (var layer in _layers)
                {
                    if (string.IsNullOrWhiteSpace(layer.Name))
                    {
                        LogError(context, $"clip '{name}' has more than one layer, so every layer needs a name.");
                        continue;
                    }

                    if (layerNames.Add(layer.Name)) continue;

                    LogError(context, $"clip '{name}' has more than one layer named '{layer.Name}'.");
                }
            }

            private void ValidateSprites(UnityEngine.Object context)
            {
                if (_layers == null) return;

                foreach (var layer in _layers)
                {
                    for (var i = 0; i < layer.FrameCount; i++)
                    {
                        if (layer.GetSprite(i) != null) continue;

                        LogError(context, $"clip '{name}' has no sprite on frame {i} of layer '{layer.DisplayName}'.");
                    }
                }
            }

            private bool TryBuildFrames(out List<Frame> frames, out string error)
            {
                frames = null;

                if (!TryValidateLayers(out error)) return false;

                if (IsStatic)
                {
                    frames = new List<Frame> { BuildFrame(0, 0f) };
                    return true;
                }

                var frameCount = FrameCount;

                if (string.IsNullOrWhiteSpace(_configuration))
                {
                    error = $"has {frameCount} frames and an empty configuration, "
                        + $"expected something like '{ConfigurationExample}'.";
                    return false;
                }

                if (!TryReadConfiguration(out var totalDuration, out var setDurations, out error)) return false;

                var setTotal = setDurations.Values.Sum();
                var remainingFrames = frameCount - setDurations.Count;
                var remainingDuration = totalDuration - setTotal;

                if (remainingFrames == 0 && !Mathf.Approximately(remainingDuration, 0f))
                {
                    error = $"gives every frame a duration adding up to {setTotal}s, "
                        + $"which does not match its {totalDuration}s total.";
                    return false;
                }

                if (remainingFrames > 0 && remainingDuration <= 0f)
                {
                    error = $"spends {setTotal}s of its {totalDuration}s total, leaving nothing "
                        + $"for the {remainingFrames} frame(s) without a duration.";
                    return false;
                }

                var sharedDuration = remainingFrames == 0 ? 0f : remainingDuration / remainingFrames;

                frames = new List<Frame>(frameCount);
                for (var i = 0; i < frameCount; i++)
                {
                    frames.Add(BuildFrame(i, setDurations.TryGetValue(i, out var duration) ? duration : sharedDuration));
                }

                return true;
            }

            private Frame BuildFrame(int index, float secondsDuration)
            {
                var sprites = new Sprite[_layers.Count];

                for (var i = 0; i < _layers.Count; i++)
                {
                    sprites[i] = _layers[i].GetSprite(index);
                }

                return new Frame
                {
                    SpritesByLayer = sprites,
                    SecondsDuration = secondsDuration,
                };
            }

            private bool TryValidateLayers(out string error)
            {
                error = null;

                if (_layers == null || _layers.Count == 0)
                {
                    error = "has no sprite layer.";
                    return false;
                }

                var frameCount = _layers[0].FrameCount;

                if (frameCount == 0)
                {
                    error = $"has no sprite frames on layer '{_layers[0].DisplayName}'.";
                    return false;
                }

                for (var i = 1; i < _layers.Count; i++)
                {
                    if (_layers[i].FrameCount == frameCount) continue;

                    error = $"has {frameCount} frames on layer '{_layers[0].DisplayName}' but "
                        + $"{_layers[i].FrameCount} on layer '{_layers[i].DisplayName}', "
                        + "every layer has to hold the same frames.";
                    return false;
                }

                return true;
            }

            private bool TryReadConfiguration(
                out float totalDuration,
                out Dictionary<int, float> setDurations,
                out string error)
            {
                totalDuration = 0f;
                setDurations = new Dictionary<int, float>();
                error = null;

                var hasTotalDuration = false;
                var frameCount = FrameCount;

                foreach (var order in _configuration.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = order.Split(ValueSeparator);
                    if (parts.Length != 2)
                    {
                        error = $"reads '{order}', which is not '<key>{ValueSeparator}<seconds>'.";
                        return false;
                    }

                    if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                        || seconds < 0f)
                    {
                        error = $"reads '{order}', whose duration is not a positive number.";
                        return false;
                    }

                    if (parts[0] == TotalDurationKey)
                    {
                        if (hasTotalDuration)
                        {
                            error = $"declares '{TotalDurationKey}' more than once.";
                            return false;
                        }

                        if (seconds <= 0f)
                        {
                            error = $"reads '{order}', but a total duration has to be above zero.";
                            return false;
                        }

                        hasTotalDuration = true;
                        totalDuration = seconds;
                        continue;
                    }

                    if (!parts[0].StartsWith(FrameKey, StringComparison.Ordinal)
                        || !int.TryParse(parts[0][FrameKey.Length..], out var index))
                    {
                        error = $"reads '{order}', which is neither '{TotalDurationKey}{ValueSeparator}<seconds>' "
                            + $"nor '{FrameKey}<index>{ValueSeparator}<seconds>'.";
                        return false;
                    }

                    if (index < 0 || index >= frameCount)
                    {
                        error = $"reads '{order}', but frame {index} is outside 0..{frameCount - 1}.";
                        return false;
                    }

                    if (!setDurations.TryAdd(index, seconds))
                    {
                        error = $"declares frame {index} more than once.";
                        return false;
                    }
                }

                if (hasTotalDuration) return true;

                error = $"has no '{TotalDurationKey}{ValueSeparator}<seconds>' total duration.";
                return false;
            }

            private void UpdateInspectorName()
            {
                if (_layers != null)
                {
                    foreach (var layer in _layers)
                    {
                        layer.UpdateInspectorName();
                    }
                }

                name = string.IsNullOrWhiteSpace(_clipName) ? "<unnamed>" : _clipName;

                if (IsStatic)
                {
                    name += " (static)";
                    return;
                }

                if (_itLoops)
                {
                    name += " (loops)";
                    return;
                }

                if (string.IsNullOrWhiteSpace(_nextClip)) return;

                name += $" -> {_nextClip}";
            }

            [Serializable]
            private class Layer
            {
                [HideInInspector] public string name;

                [SerializeField] private string _layerName;
                [SerializeField] private List<Sprite> _spriteFrames;

                public string Name => _layerName;
                public string DisplayName => string.IsNullOrWhiteSpace(_layerName) ? "<default>" : _layerName;
                public int FrameCount => _spriteFrames?.Count ?? 0;

                public Sprite GetSprite(int index) => _spriteFrames[index];

                public void UpdateInspectorName()
                {
                    name = DisplayName;
                }
            }

            public class Frame
            {
                public Sprite[] SpritesByLayer { get; set; }
                public float SecondsDuration { get; set; }

                public Sprite GetSprite(int layerIndex) => SpritesByLayer[layerIndex];
            }
        }
    }
}
