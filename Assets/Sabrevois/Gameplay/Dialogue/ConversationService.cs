namespace Sabrevois.Gameplay.Dialogue
{
    public class ConversationService
    {
        private int _lastGetTextIndex = -1;
        private int _lastReactionHurtIndex = -1;

        public string GetText()
        {
            if (_strings.Length == 0) return string.Empty;
            if (_strings.Length == 1) return _strings[0];

            int index;
            do
            {
                index = UnityEngine.Random.Range(0, _strings.Length);
            } while (index == _lastGetTextIndex);

            _lastGetTextIndex = index;
            return _strings[index];
        }
        
        public string GetReactionHurt()
        {
            if (_onHurt.Length == 0) return string.Empty;
            if (_onHurt.Length == 1) return _onHurt[0];

            int index;
            do
            {
                index = UnityEngine.Random.Range(0, _onHurt.Length);
            } while (index == _lastReactionHurtIndex);

            _lastReactionHurtIndex = index;
            return _onHurt[index];
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