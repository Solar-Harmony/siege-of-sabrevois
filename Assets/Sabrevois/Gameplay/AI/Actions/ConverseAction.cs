using Sabrevois.AI;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using VContainer;

namespace Sabrevois.Gameplay.AI.Actions
{
    [System.Serializable]
    public class ConverseActionConfig
    {
    }
    
    public class ConverseAction : IAction<ConverseActionConfig>
    {
        [Inject] 
        private ConversationService _conversations;
        
        public void Execute(ActionContext ctx, ConverseActionConfig config)
        { 
            Debug.Log(_conversations.GetText());
        }
    }
}