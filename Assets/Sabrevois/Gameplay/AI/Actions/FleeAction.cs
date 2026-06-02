using System;
using Sabrevois.AI.Actions;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class FleeActionConfig : ActionConfigBase<FleeAction, FleeActionState>
    {
        public float FleeDistance = 10f;
    }

    public class FleeActionState : IActionState
    {
    }

    public record FleeAction : IAction<FleeActionConfig, FleeActionState>
    {
        public Interruptible Interruptible => Interruptible.Always;

        public ActionStatus Begin(ActionContext ctx, FleeActionConfig config, FleeActionState state)
        {
            var opponentComponent = ctx.Agent.GetComponent<OpponentComponent>();
            var navMeshAgent = ctx.Agent.GetComponent<NavMeshAgent>();

            if (opponentComponent != null && opponentComponent.CurrentOpponent != null && navMeshAgent != null)
            {
                Vector3 directionAwayFromOpponent = (ctx.Agent.transform.position - opponentComponent.CurrentOpponent.position).normalized;
                Vector3 targetPosition = ctx.Agent.transform.position + directionAwayFromOpponent * config.FleeDistance;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPosition, out hit, config.FleeDistance, NavMesh.AllAreas))
                {
                    navMeshAgent.SetDestination(hit.position);
                }
                else
                {
                    navMeshAgent.SetDestination(targetPosition);
                }
            }

            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);
            return ActionStatus.Running;
        }

        public ActionStatus Update(ActionContext ctx, FleeActionConfig config, FleeActionState state)
        {
            var navMeshAgent = ctx.Agent.GetComponent<NavMeshAgent>();
            if (navMeshAgent != null)
            {
                if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
                {
                    if (!navMeshAgent.hasPath || navMeshAgent.velocity.sqrMagnitude == 0f)
                    {
                        return ActionStatus.Done;
                    }
                }
            }
            return ActionStatus.Running;
        }

        public void End(ActionContext ctx, FleeActionConfig config, FleeActionState state)
        {
            var navMeshAgent = ctx.Agent.GetComponent<NavMeshAgent>();
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
            }
        }
    }
}