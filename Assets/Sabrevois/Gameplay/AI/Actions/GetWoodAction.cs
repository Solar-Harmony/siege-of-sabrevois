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
        public float gatheringDuration = 10f;
        public float woodQuantity = 10f; //TODO : Fellable Trees, remove
    }

    public class GetWoodActionState : IActionState
    {
        public float gatheringTimer;
    }

    public record GetWoodAction(ConversationService Conversation) : IAction<GetWoodActionConfig, GetWoodActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;

        private WorldObjectCategory _woodLocationCategory = WorldObjectCategory.Tree;

        public ActionStatus Begin(ActionContext ctx, GetWoodActionConfig config, GetWoodActionState state)
        {
            NavMeshAgent agent = ctx.Agent.GetComponent<NavMeshAgent>();
            state.gatheringTimer = config.gatheringDuration;


            List<GameObject> possibleGatheringSpots = WorldObjectRegistry.Instance.Get(_woodLocationCategory);

            Transform closestTree = null;
            float currentDistance = Mathf.Infinity;

            foreach (GameObject treeSpot in possibleGatheringSpots)
            {
                float distance = Vector3.Distance(ctx.Agent.transform.position, treeSpot.transform.position);
                //TODO Fellable Trees
                //Wood treeWood = treeSpot.GetComponent<Wood>();
                Wood agentWood = agent.GetComponent<Wood>();
                if (distance < currentDistance &&
                    agentWood.CurrentWood > 0)
                {
                    currentDistance = distance;
                    closestTree = treeSpot.transform;
                }
            }

            if (closestTree != null)
                agent.SetDestination(closestTree.position);

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


            Wood agentWood = ctx.Agent.GetComponent<Wood>();
            float gatheredWood = Mathf.Min(
                config.woodQuantity,
                agentWood.MaxWood - agentWood.CurrentWood);

            agentWood.AddWood(gatheredWood);
            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);

            return ActionStatus.Done;
        }
    }
}