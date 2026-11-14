using System;
using System.Threading;
using System.Threading.Tasks;
using Piper;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Spatial audio output for one NPC. Playback itself is serialized by
    /// <see cref="BarkSpeechScheduler"/>; this component only synthesizes and plays a clip
    /// when the scheduler hands it a turn.
    /// </summary>
    public class BarkDisplay : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;

        private PiperManager _piperManager;
        private DemonicRobotFilter _robotFilter;
        private AudioClip _currentClip;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            _robotFilter = GetComponent<DemonicRobotFilter>();
            ConfigureSpatialAudio();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            StopAndRelease();
        }

        private void ConfigureSpatialAudio()
        {
            if (_audioSource == null) return;

            _audioSource.spatialBlend = 1f;
            _audioSource.minDistance = 3f;
            _audioSource.maxDistance = 25f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.spread = 60f;
            _audioSource.dopplerLevel = 0f;
        }

        public void Setup(PiperManager manager)
        {
            _piperManager = manager;
        }

        /// <summary>
        /// Synthesizes and plays one line. Returns the clip length in seconds (0 on failure),
        /// so the scheduler knows how long this NPC occupies the voice.
        /// </summary>
        public async Task<float> SpeakAsync(string text, float volume, CancellationToken externalToken)
        {
            if (_piperManager == null || string.IsNullOrWhiteSpace(text))
                return 0f;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            CancellationToken ct = _cts.Token;

            try
            {
                AudioClip clip = await _piperManager.TextToSpeechAsync(text, ct);

                if (this == null || _audioSource == null || clip == null)
                    return 0f;

                // Stop before releasing: a pooled clip that is still playing must never be
                // handed to another NPC, otherwise two voices share the same buffer.
                _audioSource.Stop();

                if (_currentClip != null)
                    _piperManager.ReleaseClip(_currentClip);

                _audioSource.volume = Mathf.Clamp(volume, 0f, 1f);
                _audioSource.clip = clip;
                _audioSource.Play();
                _currentClip = clip;

                return clip.length;
            }
            catch (OperationCanceledException)
            {
                return 0f;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BarkDisplay] Speak failed for {name}: {e}", this);
                return 0f;
            }
        }

        /// <summary>Silences this NPC immediately (death, teardown).</summary>
        public void StopSpeaking()
        {
            _cts?.Cancel();

            if (_audioSource != null)
                _audioSource.Stop();

            StopAndRelease();
        }

        private void StopAndRelease()
        {
            if (_currentClip == null) return;

            _piperManager?.ReleaseClip(_currentClip);
            _currentClip = null;
        }
    }
}
