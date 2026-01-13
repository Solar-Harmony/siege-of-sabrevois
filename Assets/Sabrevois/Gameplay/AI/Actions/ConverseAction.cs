using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using TMPro;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class SaySomethingActionConfig : IActionConfig<SaySomethingAction, SaySomethingActionState>
    {
    }

    public class SaySomethingActionState : IActionState
    {
    }
    
    public record SaySomethingAction(ConversationService Conversation) : IAction<SaySomethingActionConfig, SaySomethingActionState>
    {
        public Interruptible Interruptible => Interruptible.Always;

        public ActionStatus Begin(ActionContext ctx, SaySomethingActionConfig config, SaySomethingActionState state)
        {
            ctx.Agent.GetComponentInChildren<TextMeshPro>().text = Conversation.GetText();
            return ActionStatus.Done;
        }

        // todo: make update optional ig lol
        // todo: support a cooldown between actions of same type?
        public ActionStatus Update(ActionContext ctx, SaySomethingActionConfig config, SaySomethingActionState state)
        {
            return ActionStatus.Done;
        }
    }
}