using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using VContainer;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class WhineActionConfig : IActionConfig<WhineAction>
    {
    }
    
    public record WhineAction(ConversationService Conversations) : IAction<WhineActionConfig>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;
        public bool Execute(ActionContext ctx, WhineActionConfig config)
        {
            Debug.Log(Conversations.GetReactionHurt());
            return true;
        }
    }
}