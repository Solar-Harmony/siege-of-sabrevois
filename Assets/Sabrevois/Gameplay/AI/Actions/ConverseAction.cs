using System;
using Sabrevois.AI;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using VContainer;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class ConverseActionConfig : IActionConfig
    {
        public string Name;
        public Type ActionType { get; } = typeof(ConverseAction);
    }
    
    public sealed class ConverseAction : IAction<ConverseActionConfig>
    {
        [Inject] 
        private ConversationService _conversations;
        
        public void Execute(ActionContext ctx, ConverseActionConfig config)
        { 
            Debug.Log(_conversations.GetText());
        }
    }
}