using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class ReactHurtActionConfig : ActionConfigBase<ReactHurtAction, ReactHurtActionState>
    {
        public float Interval = 2.0f;
        public float EnergyCost { get; }
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
            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, ReactHurtActionConfig config, ReactHurtActionState state)
        {
            state.Timer -= Time.deltaTime;
            if (state.Timer <= 0f)
            {
                Debug.Log(Conversation.GetReactionHurt()); // dummy conversation
                return ActionStatus.Done;
            }
            return ActionStatus.Running;
        }
    }
}