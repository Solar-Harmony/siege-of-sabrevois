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
    
    public record SleepAction : IAction<SleepActionConfig, SleepActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;
        
        private WorldObjectCategory _sleepSpotCategory = WorldObjectCategory.House;

        public ActionStatus Begin(ActionContext ctx, SleepActionConfig config, SleepActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();
            state.sleepTimer = config.sleepDuration;
            

            List<GameObject> spots = WorldObjectRegistry.Instance.Get(_sleepSpotCategory);

            Transform closestSpot = null;
            float currentDistance = Mathf.Infinity;

            foreach (GameObject sleepSpot in spots)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, sleepSpot.transform.position);
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    closestSpot = sleepSpot.transform;
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
            
            if (state.sleepTimer > 0f)
                return ActionStatus.Running;

            ctx.Agent.GetComponent<Energy>().ResetEnergy();
            
            return ActionStatus.Done;
        }
    }
}
