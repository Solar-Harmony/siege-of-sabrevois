using System;
using System.Collections.Generic;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class SleepActionConfig : ActionConfigBase<SleepAction, SleepActionState>
    {
        public float sleepDuration = 5f;
    }
    
    public class SleepActionState : IActionState
    {
        public float sleepTimer;
    }
    
    public record SleepAction(ConversationService Conversation) : IAction<SleepActionConfig, SleepActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;
        
        private string _sleepSpotCategory = "SleepSpot";
        

        public ActionStatus Begin(ActionContext ctx, SleepActionConfig config, SleepActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();
            state.sleepTimer = config.sleepDuration;
            

            List<Transform> spots = WorldObjectRegistry.Instance.Get(_sleepSpotCategory);

            Transform closestSpot = null;
            float currentDistance = Mathf.Infinity;

            foreach (Transform sleepSpot in spots)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, sleepSpot.position);
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    closestSpot = sleepSpot;
                }
            }

            if (closestSpot != null)
                agent.SetDestination(closestSpot.position);

            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, SleepActionConfig config, SleepActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();

            bool isPathing = agent.pathPending || agent.remainingDistance > agent.stoppingDistance;

            if (isPathing)
                return ActionStatus.Running;
            
            state.sleepTimer -= Time.deltaTime;
            
            Debug.Log("Sleep timer remaining: " + state.sleepTimer);
            if (state.sleepTimer > 0f)
                return ActionStatus.Running;

            ctx.Agent.GetComponent<Energy>().ResetEnergy();
            
            return ActionStatus.Done;
        }
    }
}
