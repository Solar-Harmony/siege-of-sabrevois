using UnityEngine;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// What one NPC can currently perceive around it: the character it is reacting to, the
    /// player, and the closest world object. Rebuilt a few times per second and fed into
    /// both the LLM prompt and the fallback line substitution, so barks can name things
    /// instead of being generic.
    /// </summary>
    public sealed class BarkContext
    {
        /// <summary>Character the NPC is currently reacting to, if any.</summary>
        public Transform Target;

        /// <summary>How the target is addressed in a line ("traveller", "Cyborg Canid").</summary>
        public string TargetLabel = "traveller";

        /// <summary>Nearest registered world object ("tree", "house", "food"), if any.</summary>
        public string ObjectLabel;

        public float ObjectDistance;

        /// <summary>Human-readable list for the prompt, e.g. "the traveller (4m), a tree (3m)".</summary>
        public string NearbyDescription = string.Empty;

        public bool HasTarget => Target != null;
        public bool HasObject => !string.IsNullOrEmpty(ObjectLabel);
    }
}
