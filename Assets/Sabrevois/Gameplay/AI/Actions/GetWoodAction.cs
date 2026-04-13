using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using Sabrevois.Utils;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class GetWoodActionConfig : ActionConfigBase<GetWoodAction, GetWoodActionState>
    {
        public float gatheringDuration = 5f;
    }

    public class GetWoodActionState : IActionState
    {
        public float gatheringTimer;
    }

    public record GetWoodAction(ConversationService Conversation) : IAction<GetWoodActionConfig, GetWoodActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;

        private WorldObjectCategory _woodStorageCategory = WorldObjectCategory.House;
        private WorldObjectCategory _woodLocationCategory = WorldObjectCategory.Tree;

        public ActionStatus Begin(ActionContext ctx, GetWoodActionConfig config, GetWoodActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();
            state.gatheringTimer = config.gatheringDuration;


            List<GameObject> possibleDepositSpots = WorldObjectRegistry.Instance.Get(_woodStorageCategory);

            Transform closestHouse = null;
            float currentDistance = Mathf.Infinity;

            foreach (GameObject houseSpot in possibleDepositSpots)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, houseSpot.transform.position);
                Wood houseWood = houseSpot.GetComponent<Wood>();
                if (distance < currentDistance && (houseWood.CurrentWood + 5f) < houseWood.MaxWood)
                {
                    currentDistance = distance;
                    closestHouse = houseSpot.transform;
                }
            }

            if (closestHouse != null)
                agent.SetDestination(closestHouse.position);

            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, GetWoodActionConfig config, GetWoodActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();

            bool isPathing = agent.pathPending || agent.remainingDistance > agent.stoppingDistance;

            if (isPathing)
                return ActionStatus.Running;

            state.gatheringTimer -= Time.deltaTime;

            if (state.gatheringTimer > 0f)
                return ActionStatus.Running;

            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);

            return ActionStatus.Done;
        }
    }
}

