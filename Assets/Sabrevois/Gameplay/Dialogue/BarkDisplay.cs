using System;
using System.Threading;
using System.Threading.Tasks;
using Piper;
using UnityEngine;

namespace Sabrevois.Gameplay
{
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

            if (_currentClip != null)
            {
                _piperManager?.ReleaseClip(_currentClip);
                _currentClip = null;
            }
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

        public async Task SpeakAsync(string text, float volume)
        {
            if (_piperManager == null || string.IsNullOrWhiteSpace(text))
                return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                var clip = await _piperManager.TextToSpeechAsync(text, ct);

                if (this == null || _audioSource == null)
                    return;

                if (_currentClip != null)
                    _piperManager.ReleaseClip(_currentClip);

                _audioSource.volume = Mathf.Clamp(volume, 0f, 1f);
                _audioSource.Stop();
                _audioSource.clip = clip;
                _audioSource.Play();
                _currentClip = clip;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[BarkDisplay] Speak failed for {name}: {e}", this);
            }
        }

        public void Speak(string text, float volume)
        {
            _ = SpeakAsync(text, volume);
        }
    }
}
