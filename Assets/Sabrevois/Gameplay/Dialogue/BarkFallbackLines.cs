using UnityEngine;
using Random = UnityEngine.Random;

namespace Sabrevois.Gameplay
{
    /// <summary>
    /// Spoken lines used when the LLM backend cannot produce a bark (endpoint offline,
    /// timed out, or returned junk). Keeps NPCs talking with no backend running.
    ///
    /// Tokens: <c>{part}</c> = body part that was hit/severed, <c>{target}</c> = the person
    /// being addressed, <c>{object}</c> = the nearest world object. Lines that need a token
    /// are skipped when the NPC has nothing to point at, and the chosen line is filled in.
    /// </summary>
    public static class BarkFallbackLines
    {
        private const string PartToken = "{part}";
        private const string TargetToken = "{target}";
        private const string ObjectToken = "{object}";

        private static readonly string[] Idle =
        {
            "Long live the King!",
            "Move along, traveller.",
            "Nothing to see here.",
            "Keep your distance.",
            "God bless.",
            "Quiet. I'm thinking.",
            "Are you new here?",
            "That {object} is in my way.",
            "Nothing out here but {object}.",
            "Fine weather for it."
        };

        private static readonly string[] AngryIdle =
        {
            "I've had enough of you.",
            "Keep walking. Now.",
            "You're still breathing. Enjoy it.",
            "Come back and finish it, coward.",
            "One more word. Just one.",
            "You'll pay for this, {target}!",
            "Run while you still can, {target}.",
            "Next time I won't miss, {target}."
        };

        private static readonly string[] Sight =
        {
            "{target}! Halt!",
            "You there — halt.",
            "Look who wandered in.",
            "I see you, {target}.",
            "State your business.",
            "Don't move, {target}."
        };

        private static readonly string[] HurtLight =
        {
            "Ow! Watch it, {target}!",
            "A scratch. Is that all you've got?",
            "Careful where you point that.",
            "That stung, you idiot.",
            "Did you just hit me, {target}?"
        };

        private static readonly string[] HurtModerate =
        {
            "My {part}! You'll pay for that.",
            "That's going to bleed...",
            "You'll regret that, {target}.",
            "Not bad. My turn."
        };

        private static readonly string[] HurtHeavy =
        {
            "My {part}—! I'm bleeding!",
            "You'll die for that, {target}.",
            "Stay back! STAY BACK!",
            "That's it. No more mercy."
        };

        private static readonly string[] Desperate =
        {
            "Mercy! Please, I beg you!",
            "Stop! I yield!",
            "I'll do anything — just let me live!",
            "No... not like this."
        };

        private static readonly string[] Severed =
        {
            "My {part}! MY {part}!",
            "You took my {part}, you bastard!",
            "I can't— my {part} is gone!",
            "That was my {part}!"
        };

        private static readonly string[] Death =
        {
            "I'm not finished yet...",
            "Repair me... and I will serve you.",
            "You think this kills me?",
            "This is not the end of me."
        };

        private static readonly string[] Reaction =
        {
            "What was that?",
            "Who goes there?",
            "Hmph.",
            "Trouble, I wager."
        };

        public static string GetLine(BarkSituation situation, string bodyPart = null,
            string targetLabel = null, string objectLabel = null)
        {
            string[] lines = situation switch
            {
                BarkSituation.AngryIdle => AngryIdle,
                BarkSituation.Sight => Sight,
                BarkSituation.HurtLight => HurtLight,
                BarkSituation.HurtModerate => HurtModerate,
                BarkSituation.HurtHeavy => HurtHeavy,
                BarkSituation.Desperate => Desperate,
                BarkSituation.Severed => Severed,
                BarkSituation.Death => Death,
                BarkSituation.Reaction => Reaction,
                _ => Idle
            };

            if (lines == null || lines.Length == 0) return "...";

            // Try to pick a line whose tokens can actually be filled in.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                string candidate = lines[Random.Range(0, lines.Length)];
                if (candidate.Contains(TargetToken) && string.IsNullOrEmpty(targetLabel)) continue;
                if (candidate.Contains(ObjectToken) && string.IsNullOrEmpty(objectLabel)) continue;
                return Substitute(candidate, bodyPart, targetLabel, objectLabel);
            }

            return Substitute(lines[0], bodyPart, targetLabel, objectLabel);
        }

        public static float VolumeFor(BarkSituation situation)
        {
            return situation switch
            {
                BarkSituation.HurtLight => 0.65f,
                BarkSituation.HurtModerate => 0.75f,
                BarkSituation.HurtHeavy => 0.85f,
                BarkSituation.AngryIdle => 0.7f,
                BarkSituation.Severed => 0.9f,
                BarkSituation.Desperate => 0.6f,
                BarkSituation.Sight => 0.55f,
                BarkSituation.Death => 0.45f,
                _ => 0.5f
            };
        }

        /// <summary>Fills every token in <paramref name="line"/>; safe on custom lines too.</summary>
        public static string Substitute(string line, string bodyPart, string targetLabel,
            string objectLabel)
        {
            if (string.IsNullOrEmpty(line)) return line;

            if (line.Contains(PartToken))
                line = line.Replace(PartToken, string.IsNullOrEmpty(bodyPart) ? "body" : bodyPart);
            if (line.Contains(TargetToken))
                line = line.Replace(TargetToken, string.IsNullOrEmpty(targetLabel) ? "traveller" : targetLabel);
            if (line.Contains(ObjectToken))
                line = line.Replace(ObjectToken, string.IsNullOrEmpty(objectLabel) ? "this place" : objectLabel);

            return line;
        }
    }
}
