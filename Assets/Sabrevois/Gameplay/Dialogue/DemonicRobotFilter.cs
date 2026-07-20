using UnityEngine;

namespace Sabrevois.Gameplay
{
    [RequireComponent(typeof(AudioSource))]
    public class DemonicRobotFilter : MonoBehaviour
    {
        [SerializeField] private DemonicVoiceConfig _config;

        private int _outputSampleRate;
        private int _channels;

        private float _ringPhase;
        private float _vibratoPhase;
        private float _clarityLpState;

        private float[] _ringBuffer;
        private int _ringBufferSize;
        private int _ringWritePos;

        private bool _inGlitch;
        private int _glitchCooldownSamples;
        private int _glitchAnchorPos;
        private float _glitchReadPos;
        private float _glitchSpeed;
        private int _glitchCurWindow;
        private int _glitchRepeatCount;
        private int _glitchRepCount;
        private int _glitchCrossfadeLen;

        private System.Random _rng;

        public DemonicVoiceConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                if (isActiveAndEnabled)
                    Rebuild();
            }
        }

        private void Awake()
        {
            if (_config != null)
                Rebuild();
        }

        private void Rebuild()
        {
            if (_config == null) return;

            _outputSampleRate = AudioSettings.outputSampleRate;
            _channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;

            var audioSource = GetComponent<AudioSource>();
            audioSource.pitch = _config.Pitch;

            int maxWindowSamples = Mathf.CeilToInt(_outputSampleRate * _config.GlitchWindowMs / 1000f);
            _ringBufferSize = maxWindowSamples * _channels * 8;
            _ringBuffer = new float[_ringBufferSize];

            _rng = new System.Random();
            _glitchCooldownSamples = Mathf.RoundToInt(
                _outputSampleRate * _config.AvgGlitchInterval * (float)(0.3 + _rng.NextDouble() * 1.7));
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_config == null) return;

            _channels = channels;

            float ringOmega = 2f * Mathf.PI * _config.RingModFrequency / _outputSampleRate;
            float vibratoOmega = 2f * Mathf.PI * _config.VibratoRate / _outputSampleRate;

            float clarityAmount = _config.Clarity;
            float shelfHz = 2200f + clarityAmount * 3000f;
            float shelfAlpha = Mathf.Exp(-2f * Mathf.PI * shelfHz / _outputSampleRate);
            float shelfBoost = 1f + clarityAmount * 5f;

            for (int i = 0; i < data.Length; i += channels)
            {
                if (!_inGlitch)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        _ringBuffer[_ringWritePos] = data[i + c];
                        _ringWritePos = (_ringWritePos + 1) % _ringBufferSize;
                    }
                }

                if (_config.GlitchEnabled)
                {
                    if (_inGlitch)
                    {
                        ProcessGlitchRead(data, i, channels);
                    }
                    else
                    {
                        _glitchCooldownSamples--;
                        if (_glitchCooldownSamples <= 0 && _rng.NextDouble() < 0.03)
                            EnterGlitch();
                    }
                }

                float sample = data[i];

                _ringPhase += ringOmega;
                if (_ringPhase > Mathf.PI * 2f)
                    _ringPhase -= Mathf.PI * 2f;

                _vibratoPhase += vibratoOmega;
                if (_vibratoPhase > Mathf.PI * 2f)
                    _vibratoPhase -= Mathf.PI * 2f;

                float carrier = Mathf.Sin(_ringPhase);
                float vibrato = 1f + Mathf.Sin(_vibratoPhase) * _config.VibratoDepth;

                sample = Mathf.Lerp(sample, sample * carrier, _config.RingModMix);
                sample *= vibrato;

                if (_config.Saturation > 0.001f)
                {
                    float drive = 1f + _config.Saturation * 6f;
                    float wet = Mathf.Clamp(sample * drive, -1f, 1f);
                    float dryFrac = 1f / (1f + _config.Saturation * 5f);
                    sample = sample * dryFrac + wet * (1f - dryFrac);
                }

                if (clarityAmount > 0.001f)
                {
                    _clarityLpState += shelfAlpha * (sample - _clarityLpState);
                    float high = sample - _clarityLpState;
                    sample = _clarityLpState + high * shelfBoost;
                }

                sample = Mathf.Clamp(sample, -1f, 1f);

                for (int c = 0; c < channels; c++)
                    data[i + c] = sample;
            }
        }

        private void ProcessGlitchRead(float[] data, int i, int channels)
        {
            float readPosTotal = _glitchReadPos * channels;
            int intPos = (int)readPosTotal;
            float frac = readPosTotal - intPos;

            int count = (channels > data.Length - i) ? (data.Length - i) : channels;
            int actualChannels = channels;

            for (int c = 0; c < count && c < actualChannels; c++)
            {
                int idxA = (_glitchAnchorPos + intPos + c) % _ringBufferSize;
                int idxB = (_glitchAnchorPos + intPos + actualChannels + c) % _ringBufferSize;

                float sA = _ringBuffer[idxA];
                float sB = _ringBuffer[idxB];
                float val = sA + (sB - sA) * frac;

                float fade = 1f;
                if (_glitchCrossfadeLen > 0)
                {
                    int fromStart = Mathf.RoundToInt(_glitchReadPos);
                    int fromEnd = _glitchCurWindow - fromStart;
                    if (fromStart < _glitchCrossfadeLen)
                        fade = (float)fromStart / _glitchCrossfadeLen;
                    else if (fromEnd < _glitchCrossfadeLen)
                        fade = (float)fromEnd / _glitchCrossfadeLen;
                }

                data[i + c] = val * fade;
            }

            _glitchReadPos += _glitchSpeed;

            if (_glitchReadPos >= _glitchCurWindow)
            {
                _glitchReadPos -= _glitchCurWindow;
                _glitchRepCount++;

                if (_glitchRepCount >= _glitchRepeatCount)
                {
                    _inGlitch = false;
                    _glitchCooldownSamples = Mathf.RoundToInt(
                        _outputSampleRate * _config.AvgGlitchInterval * (float)(0.3 + _rng.NextDouble() * 1.7));
                }
                else
                {
                    _glitchSpeed = 0.95f + (float)(_rng.NextDouble() * 0.10f)
                                   + (float)(_rng.NextDouble() * _config.GlitchPitchWobble * 2f - _config.GlitchPitchWobble);
                }
            }
        }

        private void EnterGlitch()
        {
            _inGlitch = true;

            float windowScale = (float)(0.4 + _rng.NextDouble() * 1.2);
            _glitchCurWindow = Mathf.Max(1, Mathf.CeilToInt(
                _outputSampleRate * _config.GlitchWindowMs * windowScale / 1000f));

            _glitchRepeatCount = Mathf.Max(2,
                _config.GlitchRepeatCount + _rng.Next(-2, 3));

            _glitchCrossfadeLen = Mathf.Min(
                _glitchCurWindow / 3,
                Mathf.RoundToInt(_outputSampleRate * _config.GlitchCrossfadeMs / 1000f));

            _glitchAnchorPos = (_ringWritePos - _glitchCurWindow * _channels + _ringBufferSize) % _ringBufferSize;
            _glitchReadPos = 0f;
            _glitchRepCount = 0;

            _glitchSpeed = 0.95f + (float)(_rng.NextDouble() * 0.10f)
                           + (float)(_rng.NextDouble() * _config.GlitchPitchWobble * 2f - _config.GlitchPitchWobble);
        }
    }
}
