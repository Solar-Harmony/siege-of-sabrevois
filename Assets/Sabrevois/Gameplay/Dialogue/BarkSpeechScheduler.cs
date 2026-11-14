using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Sabrevois.Gameplay
{
    /// <summary>How badly a line wants to be heard.</summary>
    public enum BarkPriority
    {
        /// <summary>Idle chatter / reactions: dropped when anyone is already speaking or queued.</summary>
        Ambient,

        /// <summary>Damage, sighting, death: always queued (up to a small cap).</summary>
        Important
    }

    /// <summary>
    /// Serializes bark playback so only one NPC speaks at a time.
    ///
    /// Every NPC has its own BarkDisplay/AudioSource, so without this several TTS clips start
    /// together and overlap. Lines are synthesized and played one by one with a short gap;
    /// ambient chatter is dropped while the queue is busy, and a queued line is replaced when
    /// the same NPC speaks again (latest wins).
    /// </summary>
    public sealed class BarkSpeechScheduler : ITickable, IDisposable
    {
        private const int MaxQueued = 3;
        private const float GapSeconds = 0.35f;

        private readonly Queue<Line> _queue = new();
        private readonly Dictionary<BarkDisplay, Line> _pending = new();
        private readonly CancellationTokenSource _cts = new();

        private bool _busy;
        private float _freeAt;

        private sealed class Line
        {
            public BarkDisplay Display;
            public string Text;
            public float Volume;
            public BarkPriority Priority;
        }

        public void Enqueue(BarkDisplay display, string text, float volume, BarkPriority priority)
        {
            if (display == null || string.IsNullOrWhiteSpace(text)) return;

            if (_pending.TryGetValue(display, out var existing))
            {
                // Same NPC speaks again before its turn: keep the newest text.
                existing.Text = text;
                existing.Volume = volume;
                if (priority > existing.Priority) existing.Priority = priority;
                return;
            }

            bool busy = _busy || _queue.Count > 0 || Time.unscaledTime < _freeAt;
            if (priority == BarkPriority.Ambient && busy)
            {
                Debug.Log($"[BarkSpeech] dropped ambient line for {display.name} (voice busy).");
                return;
            }

            if (_queue.Count >= MaxQueued) return;

            var line = new Line
            {
                Display = display,
                Text = text,
                Volume = volume,
                Priority = priority
            };

            _pending[display] = line;
            _queue.Enqueue(line);
        }

        public void Tick()
        {
            if (_busy || _queue.Count == 0) return;
            if (Time.unscaledTime < _freeAt) return;

            Line line = _queue.Dequeue();
            _pending.Remove(line.Display);

            if (line.Display == null)
                return;

            _busy = true;
            Debug.Log($"[BarkSpeech] speaking: {line.Display.name} \"{line.Text}\"");
            _ = PlayAsync(line);
        }

        private async Task PlayAsync(Line line)
        {
            float duration = 0f;
            try
            {
                duration = await line.Display.SpeakAsync(line.Text, line.Volume, _cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[BarkSpeech] Playback failed: {e}");
            }
            finally
            {
                _freeAt = Time.unscaledTime + duration + GapSeconds;
                _busy = false;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _queue.Clear();
            _pending.Clear();
        }
    }
}
