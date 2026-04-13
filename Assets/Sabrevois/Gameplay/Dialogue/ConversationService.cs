namespace Sabrevois.Gameplay.Dialogue
{
    public class ConversationService
    {
        public string GetText()
        {
            return _strings[UnityEngine.Random.Range(0, _strings.Length)];
        }
        
        public string GetReactionHurt()
        {
            return _onHurt[UnityEngine.Random.Range(0, _strings.Length)];
        }
        
        private readonly string[] _strings = {
            "Long live the King!",
            "Yes? Make it quick.",
            "I've got my eye on you.",
            "Have you seen any immigrants?",
            "God bless.",
            "Nice to see you, traveller.",
            "Remember to treat the King with respect.",
            "Are you new here?"
        };

        private readonly string[] _onHurt = {
            "Ouch! That hurt!",
            "Why would you do that?",
            "I'm wounded!",
            "You'll pay for that!",
            "Is that all you've got?"
        };
    }
}