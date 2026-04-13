using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using TMPro;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class ReactHurtActionConfig : ActionConfigBase<ReactHurtAction, ReactHurtActionState>
    {
        public float Interval = 2.0f;
    }

    public class ReactHurtActionState : IActionState
    {
        public float Timer;
    }
    
    public record ReactHurtAction(ConversationService Conversation) : IAction<ReactHurtActionConfig, ReactHurtActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;

        public ActionStatus Begin(ActionContext ctx, ReactHurtActionConfig config, ReactHurtActionState state)
        {
            state.Timer = config.Interval;
            ctx.Agent.GetComponentInChildren<TextMeshPro>().color = Color.red;
            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, ReactHurtActionConfig config, ReactHurtActionState state)
        {
            state.Timer -= Time.deltaTime;
            if (state.Timer <= 0f)
            {
                ctx.Agent.GetComponentInChildren<TextMeshPro>().text = Conversation.GetReactionHurt();
                return ActionStatus.Done;
            }
            return ActionStatus.Running;
        }

        public void End(ActionContext ctx, ReactHurtActionConfig config, ReactHurtActionState state)
        {
            ctx.Agent.GetComponentInChildren<TextMeshPro>().color = Color.white;
        }
    }
}