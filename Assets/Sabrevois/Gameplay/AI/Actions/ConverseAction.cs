using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class ConverseActionConfig : IActionConfig<ConverseAction> {}
    
    public record ConverseAction(ConversationService Conversation) : IAction<ConverseActionConfig>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;
        
        public bool Execute(ActionContext ctx, ConverseActionConfig config)
        {
            Debug.Log(Conversation.GetText());
            return true;
        }
    }
}