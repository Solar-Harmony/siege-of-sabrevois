using System;
using System.Collections.Generic;
using Sabrevois.AI.Actions;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    
    [Serializable]
    public class SleepOutsideActionConfig : ActionConfigBase<SleepOutsideAction, SleepOutsideActionState>
    {
        public float sleepDuration = 5f;
    }
    
    public class SleepOutsideActionState : IActionState
    {
        public float sleepTimer;
    }
    
    public record SleepOutsideAction : IAction<SleepOutsideActionConfig, SleepOutsideActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;
        
        public ActionStatus Begin(ActionContext ctx,SleepOutsideActionConfig config, SleepOutsideActionState state)
        {
            state.sleepTimer = config.sleepDuration;

            Debug.Log("Sleeping outside...", ctx.Agent);
            
            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx,SleepOutsideActionConfig config, SleepOutsideActionState state)
        {
            ctx.Agent.transform.Rotate(0f, 0f, 90f);

            
            state.sleepTimer -= Time.deltaTime;
            
            if (state.sleepTimer > 0f)
                return ActionStatus.Running;
            
            
            ctx.Agent.GetComponent<Energy>().GainEnergy(50); // arbitrary energy gain for sleeping outside
            ctx.Agent.transform.Rotate(0f, 0f, 0f);

            return ActionStatus.Done;
        }
    }
}