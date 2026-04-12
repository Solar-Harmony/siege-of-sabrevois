using System;
using System.Collections.Generic;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class FeedSelfActionConfig : ActionConfigBase<FeedSelfAction, FeedSelfActionState>
    {
    
    }

    public class FeedSelfActionState : IActionState
    {
        public Food chosenFood;
    }

    public record FeedSelfAction : IAction<FeedSelfActionConfig,FeedSelfActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;
    
        private WorldObjectCategory _foodCategory = WorldObjectCategory.Food;


        public ActionStatus Begin(ActionContext ctx, FeedSelfActionConfig config, FeedSelfActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();

            List<GameObject> foods = WorldObjectRegistry.Instance.Get(_foodCategory);

            GameObject closestFood = null;
            float currentDistance = Mathf.Infinity;

            foreach (GameObject food in foods)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, food.transform.position);
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    closestFood = food;
                }
            }

            if (!closestFood)
            {
                Debug.LogWarning("No food found");
                return ActionStatus.Done;
            }
        
            state.chosenFood = closestFood.GetComponent<Food>();
            agent.SetDestination(closestFood.transform.position);

            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, FeedSelfActionConfig config, FeedSelfActionState state)
        {
            var agent = ctx.Agent.GetComponent<NavMeshAgent>();
            
            //Si quelqu'un d'autre a déja manger la food
            if(!state.chosenFood)
                return ActionStatus.Done;
            
            bool isPathing = agent.pathPending || agent.remainingDistance > agent.stoppingDistance;
            
            if (isPathing)
                return ActionStatus.Running;
            
            state.chosenFood.Eat(ctx.Agent);
        
            return ActionStatus.Done;
        }
    }
}