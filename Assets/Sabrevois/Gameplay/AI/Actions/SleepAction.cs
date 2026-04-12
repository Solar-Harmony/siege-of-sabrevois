using System;
using System.Collections.Generic;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class SleepActionConfig : IActionConfig<SleepAction, SleepActionState>
    {
        public List<GameObject> SleepSpots;
    }
    
    public class SleepActionState : IActionState
    {
        
    }
    
    public record SleepAction(ConversationService Conversation) : IAction<SleepActionConfig, SleepActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;

        private NavMeshAgent _agent;

        public ActionStatus Begin(ActionContext ctx, SleepActionConfig config, SleepActionState state)
        {
            _agent = ctx.Agent.GetComponent<NavMeshAgent>();
            
            Transform closestSpot = null;
            float currentDistance = Mathf.Infinity;
            
            foreach (GameObject sleepSpot in config.SleepSpots)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, sleepSpot.transform.position);
                
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    closestSpot = sleepSpot.transform;
                }
            }

            if (closestSpot != null) 
                _agent.SetDestination(closestSpot.position);

            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, SleepActionConfig config, SleepActionState state)
        {
            bool isPathing = _agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance;

            if (isPathing)
                return ActionStatus.Running;

            return ActionStatus.Done;
        }
    }
}
