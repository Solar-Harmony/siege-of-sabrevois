using System.Text;
using UnityEngine;

namespace Sabrevois.Gameplay
{
    [CreateAssetMenu(fileName = "NewBarkPersonality", menuName = "Sabrevois/Bark Personality")]
    public class BarkPersonality : ScriptableObject
    {
        [Header("Character Identity")]
        [TextArea(3, 10)]
        [Tooltip("Who this character is — background, role, values.")]
        [SerializeField] private string _identity =
            "You are a soldier in a medieval siege.";

        [Header("Personality Traits (0–1)")]
        [SerializeField, Range(0f, 1f), Tooltip("Willingness to threaten, insult, and attack.")]
        private float _aggression = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Arrogance, dignity, and adherence to a personal code.")]
        private float _pride = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Self-preservation — tendency to beg, flee, or grovel.")]
        private float _cowardice = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Honesty — high = truthful, low = deceitful and manipulative.")]
        private float _sincerity = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Talkativeness — high = long-winded, low = terse and blunt.")]
        private float _verbosity = 0.3f;

        [SerializeField, Range(0f, 1f), Tooltip("Enjoyment of others' pain and suffering.")]
        private float _cruelty = 0.5f;

        public float Aggression => _aggression;
        public float Pride => _pride;
        public float Cowardice => _cowardice;
        public float Sincerity => _sincerity;
        public float Verbosity => _verbosity;
        public float Cruelty => _cruelty;

        [Header("Situational Prompts")]
        [TextArea(2, 6)]
        [SerializeField] private string _idlePrompt =
            "You are relaxing. Try to make a deal with the person nearby — offer your services, ask for personal attention and care.";
        [TextArea(2, 6)]
        [SerializeField] private string _amusedByAttackPrompt =
            "A weak attack just landed on you. You find it amusing — laugh it off and taunt them.";
        [TextArea(2, 6)]
        [SerializeField] private string _angryPrompt =
            "You're getting hurt and it's making you furious. Yell, rage, and threaten your attacker.";
        [TextArea(2, 6)]
        [SerializeField] private string _desperateBargainPrompt =
            "You're badly beaten and desperate. Beg for mercy, offer anything to make it stop.";
        [TextArea(2, 6)]
        [SerializeField] private string _deathBargainPrompt =
            "You've been destroyed but some part of you still lives. Offer a deal — repairs in exchange for service or loyalty.";

        public string IdlePrompt => _idlePrompt;
        public string AmusedByAttackPrompt => _amusedByAttackPrompt;
        public string AngryPrompt => _angryPrompt;
        public string DesperateBargainPrompt => _desperateBargainPrompt;
        public string DeathBargainPrompt => _deathBargainPrompt;

        public string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine(_identity);
            sb.AppendLine();
            sb.AppendLine($"Aggression: {_aggression:F1} — "
                + TraitLabel(_aggression, "pacifist", "combative"));
            sb.AppendLine($"Pride: {_pride:F1} — "
                + TraitLabel(_pride, "humble", "arrogant"));
            sb.AppendLine($"Cowardice: {_cowardice:F1} — "
                + TraitLabel(_cowardice, "fearless", "cowardly"));
            sb.AppendLine($"Sincerity: {_sincerity:F1} — "
                + TraitLabel(_sincerity, "deceitful", "honest"));
            sb.Append($"Verbosity: {_verbosity:F1} — ");
            sb.AppendLine(_verbosity < 0.3f
                ? "extremely terse, one-word answers"
                : _verbosity < 0.5f
                    ? "blunt, short sentences"
                    : _verbosity < 0.7f
                        ? "moderate length"
                        : "long-winded and elaborate");
            sb.AppendLine($"Cruelty: {_cruelty:F1} — "
                + TraitLabel(_cruelty, "merciful", "sadistic"));
            sb.Append("Keep responses concise. Speak only dialogue — no narration or actions.");
            return sb.ToString();
        }

        private static string TraitLabel(float value, string low, string high)
        {
            if (value < 0.2f) return $"very {low}";
            if (value < 0.4f) return low;
            if (value < 0.6f) return "balanced";
            if (value < 0.8f) return high;
            return $"very {high}";
        }
    }
}
