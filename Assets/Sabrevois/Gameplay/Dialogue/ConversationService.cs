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
            "Hello, traveler!",
            "The weather is nice today.",
            "Have you heard the latest news?",
            "Be careful out there.",
            "Good luck on your journey!"
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