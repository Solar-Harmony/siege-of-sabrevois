using System;
using Sabrevois.AI.Actions;
using UnityEngine;
using UnityEngine.AI;

namespace Sabrevois.Gameplay.AI.Actions
{
    [Serializable]
    public class FindConversationPartnerActionConfig : ActionConfigBase<FindConversationPartnerAction, FindConversationPartnerActionState>
    {
        public float SearchRadius = 10f;
    }

    public class FindConversationPartnerActionState : IActionState
    {
        public Transform Partner;
    }

    public record FindConversationPartnerAction : IAction<FindConversationPartnerActionConfig, FindConversationPartnerActionState>
    {
        private static int _conversationSeekers = 0;
        private const int MaxConversationSeekers = 3;
        public Interruptible Interruptible => Interruptible.Always;

        public ActionStatus Begin(ActionContext ctx, FindConversationPartnerActionConfig config, FindConversationPartnerActionState state)
        {
            if (_conversationSeekers >= MaxConversationSeekers)
            {
                return ActionStatus.Done;
            }

            var colliders = Physics.OverlapSphere(ctx.Agent.transform.position, config.SearchRadius);
            foreach (var collider in colliders)
            {
                var conversationComponent = collider.GetComponent<ConversationComponent>();
                if (conversationComponent != null && conversationComponent.WantsConversation && conversationComponent.ConversationPartner == null && conversationComponent.gameObject != ctx.Agent)
                {
                    var myConversationComponent = ctx.Agent.GetComponent<ConversationComponent>();
                    if (myConversationComponent != null)
                    {
                        state.Partner = conversationComponent.transform;
                        myConversationComponent.ConversationPartner = state.Partner;
                        conversationComponent.ConversationPartner = ctx.Agent.transform;
                        
                        var navMeshAgent = ctx.Agent.GetComponent<NavMeshAgent>();
                        if (navMeshAgent != null)
                        {
                            navMeshAgent.SetDestination(state.Partner.position);
                            _conversationSeekers++;
                            return ActionStatus.Running;
                        }
                    }
                }
            }
            
            ctx.Agent.GetComponent<Energy>().SpendEnergy(config.EnergyCost);
            return ActionStatus.Done;
        }

        public ActionStatus Update(ActionContext ctx, FindConversationPartnerActionConfig config, FindConversationPartnerActionState state)
        {
            if (state.Partner == null)
            {
                return ActionStatus.Done;
            }

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

        public void End(ActionContext ctx, FindConversationPartnerActionConfig config, FindConversationPartnerActionState state)
        {
            if (state.Partner != null)
            {
                _conversationSeekers--;
            }
            
            var navMeshAgent = ctx.Agent.GetComponent<NavMeshAgent>();
            if (navMeshAgent != null && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.ResetPath();
            }
        }
    }
}