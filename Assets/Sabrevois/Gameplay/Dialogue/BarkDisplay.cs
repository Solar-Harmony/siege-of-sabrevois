using Piper;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    public class BarkDisplay : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;

        private PiperManager _piperManager;
        private DemonicRobotFilter _robotFilter;

        private void Awake()
        {
            _robotFilter = GetComponent<DemonicRobotFilter>();
            ConfigureSpatialAudio();
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

        public async void Speak(string text, float volume)
        {
            if (_piperManager == null || string.IsNullOrWhiteSpace(text))
                return;

            AudioClip clip = await _piperManager.TextToSpeechAsync(text);
            if (clip != null && _audioSource != null)
            {
                _audioSource.volume = Mathf.Clamp(volume, 0f, 1f);
                _audioSource.Stop();
                _audioSource.clip = clip;
                _audioSource.Play();
            }
        }
    }
}
