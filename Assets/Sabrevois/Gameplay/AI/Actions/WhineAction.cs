using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using VContainer;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class WhineAction : IAction
    {
        [Inject] 
        private ConversationService _conversations;
        
        public void Execute(ActionContext ctx)
        {
            Debug.Log(_conversations.GetReactionHurt());
        }
    }
}