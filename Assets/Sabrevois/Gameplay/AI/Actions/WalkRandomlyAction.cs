using System;
using Sabrevois.AI.Actions;
using Sabrevois.Gameplay.Dialogue;
using Sabrevois.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class MoveRandomlyActionConfig : ActionConfigBase<MoveRandomlyAction, MoveRandomlyActionState>
    {
        [Min(1.0f)]
        public float Radius = 5f;
    }

    public class MoveRandomlyActionState : IActionState
    {
    }
    
    public record MoveRandomlyAction(FuckYouService Service) : IAction<MoveRandomlyActionConfig, MoveRandomlyActionState>
    {
        public Interruptible Interruptible => Interruptible.ExceptSelf;

        public ActionStatus Begin(ActionContext ctx, MoveRandomlyActionConfig config, MoveRandomlyActionState state)
        {
            Service.GetTest();
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * config.Radius;
            randomDirection += ctx.Agent.transform.position;
            NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 5f, 1);
            Vector3 finalPosition = hit.position;
            ctx.Agent.GetComponent<NavMeshAgent>().SetDestination(finalPosition);
            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, MoveRandomlyActionConfig config, MoveRandomlyActionState state)
        {
            var agent = ctx.Agent.GetComponent<NavMeshAgent>();
            bool isPathing = agent.pathPending || agent.remainingDistance > agent.stoppingDistance;

            if (isPathing)
                return ActionStatus.Running;
            
            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);
            return ActionStatus.Done;
        }
    }
}