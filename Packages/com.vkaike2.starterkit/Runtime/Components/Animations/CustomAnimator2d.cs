using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Vkaike2.StarterKit.Components.Animations
{
    public partial class CustomAnimator2d : MonoBehaviour
    {
        [SerializeField] private bool _isUI;
        [SerializeField] private bool _useUnscaledTime;
        [SerializeField] private bool _startPlaying;
        [SerializeField] private List<Clip> _clips;

        private readonly Dictionary<string, Clip> _clipsByName = new();
        private readonly Dictionary<string, ClipEvents> _eventsByClip = new();
        private readonly HashSet<string> _layerNames = new();

        private Image _image;
        private SpriteRenderer _spriteRenderer;
        private CancellationTokenSource _playback;
        private bool _isReady;

        private Clip _currentClip;
        private Clip.Frame _currentFrame;
        private int _currentLayerIndex;

        public string CurrentClipName { get; private set; }
        public string CurrentLayerName { get; private set; }
        public bool IsPlaying => _playback != null;

        private void OnValidate()
        {
            ValidateRenderer();
            ValidateClips();
        }

        private void OnEnable()
        {
            if (!_isReady || CurrentClipName == null) return;

            Play(CurrentClipName);
        }

        private void OnDisable()
        {
            CancelPlayback();
        }

        public void Initialize(params ClipEvents[] clipEvents)
        {
            EnsureReady();
            BuildEventLookup(clipEvents);

            if (!_startPlaying || _clips.Count == 0) return;

            Play(_clips[0].Name);
        }

        public void Play(string clipName)
        {
            EnsureReady();

            if (!_clipsByName.TryGetValue(clipName, out var clip))
            {
                LogError(this, $"has no clip named '{clipName}'.");
                return;
            }

            CancelPlayback();

            var playback = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            _playback = playback;
            _ = PlayClips(clip, playback);
        }

        public void SetLayer(string layerName)
        {
            EnsureReady();

            if (!_layerNames.Contains(layerName))
            {
                LogError(this, $"has no layer named '{layerName}'.");
                return;
            }

            if (string.Equals(CurrentLayerName, layerName, StringComparison.Ordinal)) return;

            CurrentLayerName = layerName;

            if (_currentClip != null) _currentLayerIndex = _currentClip.GetLayerIndex(layerName);

            RefreshSprite();
        }

        public void Stop()
        {
            CancelPlayback();
            CurrentClipName = null;
        }

        private void EnsureReady()
        {
            if (_isReady) return;
            _isReady = true;

            ResolveRenderer();
            BuildClipLookup();
        }

        private void ResolveRenderer()
        {
            if (_isUI)
            {
                _image = GetComponent<Image>();
                if (_image == null) LogError(this, $"is set to UI but has no {nameof(Image)}.");
                return;
            }

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null) LogError(this, $"has no {nameof(SpriteRenderer)}.");
        }

        private void BuildClipLookup()
        {
            _clipsByName.Clear();
            _layerNames.Clear();

            foreach (var clip in _clips)
            {
                foreach (var layerName in clip.LayerNames)
                {
                    if (string.IsNullOrWhiteSpace(layerName)) continue;

                    _layerNames.Add(layerName);
                }

                if (string.IsNullOrWhiteSpace(clip.Name)) continue;

                _clipsByName[clip.Name] = clip;
            }
        }

        private void BuildEventLookup(ClipEvents[] clipEvents)
        {
            _eventsByClip.Clear();

            if (clipEvents == null) return;

            foreach (var events in clipEvents)
            {
                if (events == null) continue;

                if (!_clipsByName.TryGetValue(events.ClipName, out var clip))
                {
                    LogError(this, $"got events for '{events.ClipName}', which is not one of its clips.");
                    continue;
                }

                if (_eventsByClip.ContainsKey(events.ClipName))
                {
                    LogError(this, $"got events for clip '{events.ClipName}' more than once.");
                    continue;
                }

                ValidateEventFrames(events, clip.GetFrames(this).Count);

                _eventsByClip[events.ClipName] = events;
            }
        }

        private void ValidateEventFrames(ClipEvents events, int frameCount)
        {
            foreach (var (index, callback) in events.ByFrame)
            {
                if (index < 0 || index >= frameCount)
                {
                    LogError(this, $"got an event on frame {index} of clip '{events.ClipName}', "
                        + $"which has {frameCount} frame(s).");
                    continue;
                }

                if (callback != null) continue;

                LogError(this, $"got a null event on frame {index} of clip '{events.ClipName}'.");
            }
        }

        private async Awaitable PlayClips(Clip clip, CancellationTokenSource playback)
        {
            var token = playback.Token;

            try
            {
                while (clip != null)
                {
                    CurrentClipName = clip.Name;
                    _currentClip = clip;
                    _currentLayerIndex = clip.GetLayerIndex(CurrentLayerName);

                    var events = _eventsByClip.GetValueOrDefault(clip.Name);
                    var frames = clip.GetFrames(this);
                    if (frames.Count == 0) break;

                    do
                    {
                        for (var i = 0; i < frames.Count; i++)
                        {
                            _currentFrame = frames[i];
                            RefreshSprite();
                            RaiseFrameEvent(events, i);
                            await Wait(frames[i].SecondsDuration, token);
                        }
                    }
                    while (clip.ItLoops);

                    RaiseEvent(events?.Finished);
                    token.ThrowIfCancellationRequested();

                    clip = string.IsNullOrWhiteSpace(clip.NextClip)
                        ? null
                        : _clipsByName.GetValueOrDefault(clip.NextClip);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_playback != playback) return;

            Stop();
        }

        private void RaiseFrameEvent(ClipEvents events, int index)
        {
            if (events == null) return;
            if (!events.ByFrame.TryGetValue(index, out var callback)) return;

            RaiseEvent(callback);
        }

        private void RaiseEvent(Action callback)
        {
            if (callback == null) return;

            try
            {
                callback();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private async Awaitable Wait(float seconds, CancellationToken token)
        {
            if (seconds <= 0f)
            {
                await Awaitable.NextFrameAsync(token);
                return;
            }

            if (!_useUnscaledTime)
            {
                await Awaitable.WaitForSecondsAsync(seconds, token);
                return;
            }

            var elapsed = 0f;
            while (elapsed < seconds)
            {
                await Awaitable.NextFrameAsync(token);
                elapsed += Time.unscaledDeltaTime;
            }
        }

        private void RefreshSprite()
        {
            if (_currentFrame == null) return;

            UpdateSprite(_currentFrame.GetSprite(_currentLayerIndex));
        }

        private void UpdateSprite(Sprite sprite)
        {
            if (_isUI)
            {
                _image.sprite = sprite;
                return;
            }

            _spriteRenderer.sprite = sprite;
        }

        private void CancelPlayback()
        {
            if (_playback == null) return;

            _playback.Cancel();
            _playback.Dispose();
            _playback = null;
        }

        private void ValidateRenderer()
        {
            if (_isUI && GetComponent<Image>() == null)
            {
                LogError(this, $"is set to UI but has no {nameof(Image)}.");
                return;
            }

            if (!_isUI && GetComponent<SpriteRenderer>() == null)
            {
                LogError(this, $"has no {nameof(SpriteRenderer)}.");
            }
        }

        private void ValidateClips()
        {
            if (_clips == null || _clips.Count == 0)
            {
                LogError(this, "has no clip configured.");
                return;
            }

            var names = new HashSet<string>();

            foreach (var clip in _clips)
            {
                clip.Validate(this);

                if (string.IsNullOrWhiteSpace(clip.Name)) continue;
                if (names.Add(clip.Name)) continue;

                LogError(this, $"has more than one clip named '{clip.Name}'.");
            }

            foreach (var clip in _clips)
            {
                if (string.IsNullOrWhiteSpace(clip.NextClip)) continue;
                if (names.Contains(clip.NextClip)) continue;

                LogError(this, $"clip '{clip.Name}' chains into '{clip.NextClip}', which does not exist.");
            }
        }

        private static void LogError(UnityEngine.Object context, string message)
        {
            Debug.LogError($"[{nameof(CustomAnimator2d)}] '{context.name}' {message}", context);
        }
    }
}
