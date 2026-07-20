using UnityEngine;

namespace Sabrevois.Gameplay
{
    [CreateAssetMenu(fileName = "NewBarkPersonality", menuName = "Sabrevois/Bark Personality")]
    public class BarkPersonality : ScriptableObject
    {
        [TextArea(4, 12)]
        [SerializeField] private string _systemPrompt = "You are a character in a siege battle. React to events with a single short sentence. Be in-character, emotional, and brief. Never use quotes or narration.";

        public string SystemPrompt => _systemPrompt;
    }
}
