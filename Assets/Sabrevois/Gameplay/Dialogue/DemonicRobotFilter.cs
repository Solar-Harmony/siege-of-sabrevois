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
            else
                _rng = new System.Random();
        }

        private void OnEnable()
        {
            if (_config != null)
                Rebuild();
        }

        private void OnDisable()
        {
            _inGlitch = false;
            _glitchCooldownSamples = 0;
            _glitchRepCount = 0;
            _glitchReadPos = 0f;
            _glitchAnchorPos = 0;
            _ringWritePos = 0;

            _ringPhase = 0f;
            _vibratoPhase = 0f;
            _clarityLpState = 0f;

            if (_ringBuffer != null)
                System.Array.Clear(_ringBuffer, 0, _ringBuffer.Length);
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

            float ringModFreq = _config.RingModFrequency;
            float ringModMix = _config.RingModMix;
            float vibratoRate = _config.VibratoRate;
            float vibratoDepth = _config.VibratoDepth;
            float saturation = _config.Saturation;
            float clarity = _config.Clarity;
            bool glitchEnabled = _config.GlitchEnabled;

            float ringOmega = 2f * Mathf.PI * ringModFreq / _outputSampleRate;
            float vibratoOmega = 2f * Mathf.PI * vibratoRate / _outputSampleRate;

            float shelfHz = 2200f + clarity * 3000f;
            float shelfAlpha = Mathf.Exp(-2f * Mathf.PI * shelfHz / _outputSampleRate);
            float shelfBoost = 1f + clarity * 5f;

            float satDrive = saturation > 0.001f ? 1f + saturation * 6f : 0f;
            float satDry = saturation > 0.001f ? 1f / (1f + saturation * 5f) : 1f;

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

                if (glitchEnabled)
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
                float vibrato = 1f + Mathf.Sin(_vibratoPhase) * vibratoDepth;

                sample = Mathf.Lerp(sample, sample * carrier, ringModMix);
                sample *= vibrato;

                if (saturation > 0.001f)
                {
                    float wet = Mathf.Clamp(sample * satDrive, -1f, 1f);
                    sample = sample * satDry + wet * (1f - satDry);
                }

                if (clarity > 0.001f)
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
