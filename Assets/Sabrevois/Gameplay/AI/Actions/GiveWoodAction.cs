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
    public class GiveWoodActionConfig : ActionConfigBase<GiveWoodAction, GiveWoodActionState>
    {
        public float depositDuration = 5f;
    }

    public class GiveWoodActionState : IActionState
    {
        public float depositTimer;
        public GameObject chosenHouse;
    }

    public record GiveWoodAction(ConversationService Conversation) : IAction<GiveWoodActionConfig, GiveWoodActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;

        private WorldObjectCategory _woodStorageCategory = WorldObjectCategory.House;

        public ActionStatus Begin(ActionContext ctx, GiveWoodActionConfig config, GiveWoodActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();
            state.depositTimer = config.depositDuration;


            List<GameObject> possibleDepositSpots = WorldObjectRegistry.Instance.Get(_woodStorageCategory);

            Transform closestHouse = null;
            float currentDistance = Mathf.Infinity;

            foreach (GameObject houseSpot in possibleDepositSpots)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, houseSpot.transform.position);
                Wood houseWood = houseSpot.GetComponent<Wood>();
                Wood agentWood = agent.GetComponent<Wood>();
                if (distance < currentDistance && 
                    houseWood.CurrentWood < houseWood.MaxWood &&
                    agentWood.CurrentWood > 0)
                {
                    currentDistance = distance;
                    state.chosenHouse = houseSpot;
                    closestHouse = houseSpot.transform;
                }
            }

            if (closestHouse != null)
                agent.SetDestination(closestHouse.position);
            if (!closestHouse)
            {
                Debug.LogWarning("No wood needed");
                return ActionStatus.Done;
            }

            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, GiveWoodActionConfig config, GiveWoodActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();

            bool isPathing = agent.pathPending || agent.remainingDistance > agent.stoppingDistance;

            if (isPathing)
                return ActionStatus.Running;

            state.depositTimer -= Time.deltaTime;

            if (state.depositTimer > 0f)
                return ActionStatus.Running;

            Wood houseWood = state.chosenHouse.GetComponent<Wood>();
            Wood agentWood = ctx.Agent.GetComponent<Wood>();

            float depositedWood = Mathf.Min(
                houseWood.MaxWood - houseWood.CurrentWood,
                agentWood.CurrentWood);

            agentWood.SpendWood(depositedWood);
            houseWood.AddWood(depositedWood);
            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);

            return ActionStatus.Done;
        }
    }
}
