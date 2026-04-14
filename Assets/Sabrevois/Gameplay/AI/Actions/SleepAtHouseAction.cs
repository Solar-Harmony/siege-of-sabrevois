using System;
using System.Collections.Generic;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class SleepAtHouseActionConfig : ActionConfigBase<SleepAtHouseAction, SleepAtHouseActionState>
    {
        public float sleepDuration = 5f;
    }
    
    public class SleepAtHouseActionState : IActionState
    {
        public float sleepTimer;
        public House chosenHouse;
    }
    
    public record SleepAtHouseAction : IAction<SleepAtHouseActionConfig, SleepAtHouseActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;
        
        private WorldObjectCategory _sleepSpotCategory = WorldObjectCategory.House;

        public ActionStatus Begin(ActionContext ctx, SleepAtHouseActionConfig config, SleepAtHouseActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();
            state.sleepTimer = config.sleepDuration;

            List<GameObject> spots = WorldObjectRegistry.Instance.Get(_sleepSpotCategory);

            House closestSpot = null;
            float currentDistance = Mathf.Infinity;

            foreach (GameObject sleepSpot in spots)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, sleepSpot.transform.position);
                House house = sleepSpot.GetComponent<House>();
                if (!house.IsFull() && distance < currentDistance)
                {
                    currentDistance = distance;
                    closestSpot = house;
                }
            }

            if (closestSpot != null)
            {
                if (!agent.isOnNavMesh)
                    return ActionStatus.Done;
                
                agent.SetDestination(closestSpot.transform.position);
                state.chosenHouse = closestSpot;
                state.chosenHouse.AddOccupant();
            }
            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx,SleepAtHouseActionConfig config, SleepAtHouseActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();
            if (!agent.isOnNavMesh)
                return ActionStatus.Done;
            
            bool isPathing = agent.pathPending || agent.remainingDistance > agent.stoppingDistance;

            if (isPathing)
                return ActionStatus.Running;
            
            state.sleepTimer -= Time.deltaTime;
            
            if (state.sleepTimer > 0f)
                return ActionStatus.Running;
            
            if(state.chosenHouse != null)
                state.chosenHouse.RemoveOccupant();
            
            ctx.Agent.GetComponent<Energy>().ResetEnergy();
            
            return ActionStatus.Done;
        }
    }
}
