using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using TMPro;
using UnityEngine;
using Zenject;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class SaySomethingActionConfig : ActionConfigBase<SaySomethingAction, SaySomethingActionState>
    {
        
    }

    public class SaySomethingActionState : IActionState
    {
        public TextMeshPro Text;
    }
    
    public record SaySomethingAction(ConversationService Conversation) : IAction<SaySomethingActionConfig, SaySomethingActionState>
    {
        public Interruptible Interruptible => Interruptible.Always;

        public ActionStatus Begin(ActionContext ctx, SaySomethingActionConfig config, SaySomethingActionState state)
        {
            state.Text = ctx.Agent.GetComponentInChildren<TextMeshPro>();
            state.Text.text = Conversation.GetText();
            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);
            return ActionStatus.Done;
        }

        // todo: make update optional ig lol
        // todo: support a cooldown between actions of same type?
        public ActionStatus Update(ActionContext ctx, SaySomethingActionConfig config, SaySomethingActionState state)
        {
            return ActionStatus.Done;
        }

        public void End(ActionContext ctx, SaySomethingActionConfig config, SaySomethingActionState state)
        {
            state.Text.text = "";
        }
    }
}