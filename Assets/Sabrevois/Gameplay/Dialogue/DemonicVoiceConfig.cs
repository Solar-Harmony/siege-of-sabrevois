using UnityEngine;

namespace Sabrevois.Gameplay
{
    [CreateAssetMenu(fileName = "NewDemonicVoiceConfig", menuName = "Sabrevois/Demonic Voice Config")]
    public class DemonicVoiceConfig : ScriptableObject
    {
        [Header("Pitch (lower = deeper, more menacing)")]
        [SerializeField, Range(0.3f, 0.95f)] private float _pitch = 0.55f;

        [Header("Clarity (high-frequency boost to keep speech intelligible at low pitch)")]
        [SerializeField, Range(0f, 1f)] private float _clarity = 0.55f;

        [Header("Ring Modulation (metallic rasp)")]
        [SerializeField, Range(0f, 0.5f)] private float _ringModMix = 0.12f;
        [SerializeField, Range(80f, 800f)] private float _ringModFrequency = 220f;

        [Header("Vibrato (unearthly warble)")]
        [SerializeField, Range(0f, 0.15f)] private float _vibratoDepth = 0.03f;
        [SerializeField, Range(0.5f, 8f)] private float _vibratoRate = 3.5f;

        [Header("Saturation (adds power and grit)")]
        [SerializeField, Range(0f, 0.7f)] private float _saturation = 0.32f;

        [Header("Broken-record glitch")]
        [Tooltip("Enable the stutter/glitch effect.")]
        [SerializeField] private bool _glitchEnabled = true;

        [Tooltip("Average seconds between glitch bursts.")]
        [SerializeField, Range(0.3f, 8f)] private float _avgGlitchInterval = 1.2f;

        [Tooltip("Length of the audio chunk that gets repeated (ms).")]
        [SerializeField, Range(20f, 200f)] private float _glitchWindowMs = 75f;

        [Tooltip("How many times the chunk repeats in one glitch burst.")]
        [SerializeField, Range(2, 8)] private int _glitchRepeatCount = 4;

        [Tooltip("Random pitch variation between repeats (0 = none, higher = wobblier).")]
        [SerializeField, Range(0f, 0.12f)] private float _glitchPitchWobble = 0.04f;

        [Tooltip("Crossfade duration at glitch loop boundaries to reduce clicking (ms).")]
        [SerializeField, Range(1f, 12f)] private float _glitchCrossfadeMs = 4f;

        public float Pitch => _pitch;
        public float Clarity => _clarity;
        public float RingModMix => _ringModMix;
        public float RingModFrequency => _ringModFrequency;
        public float VibratoDepth => _vibratoDepth;
        public float VibratoRate => _vibratoRate;
        public float Saturation => _saturation;
        public bool GlitchEnabled => _glitchEnabled;
        public float AvgGlitchInterval => _avgGlitchInterval;
        public float GlitchWindowMs => _glitchWindowMs;
        public int GlitchRepeatCount => _glitchRepeatCount;
        public float GlitchPitchWobble => _glitchPitchWobble;
        public float GlitchCrossfadeMs => _glitchCrossfadeMs;
    }
}
