using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using VContainer;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class WhineActionConfig
    {
    }
    
    public class WhineAction : IAction<WhineActionConfig>
    {
        [Inject] 
        private ConversationService _conversations;
        
        public void Execute(ActionContext ctx, WhineActionConfig config)
        {
            Debug.Log(_conversations.GetReactionHurt());
        }
    }
}