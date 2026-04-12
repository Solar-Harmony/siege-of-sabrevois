using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sabrevois.AI.Actions;
using Sabrevois.AI.Parallel;

namespace Sabrevois.AI
{
    public class SequentialDecisionMakingService : IDecisionMakingService
    {
        private readonly Dictionary<Type, IAction> _actions;
        private Dictionary<int, Agent> _idToAgent = new();

        public SequentialDecisionMakingService(IEnumerable<IAction> actions)
        {
            _actions = actions.ToDictionary(a => a.GetType(), a => a);
        }
        
        [CanBeNull]
        public void ChooseAction(ActionCandidate[] candidates, ActionContext ctx, ActionInstance currentAction, float hysteresisBias = 0.1f)
        {
            int gameObjectId = ctx.Agent.GetInstanceID();
            if (!_idToAgent.ContainsKey(gameObjectId))
                _idToAgent[gameObjectId] = ctx.Agent.GetComponent<Agent>();
            
            if (candidates.Length == 0)
            {
                _idToAgent[gameObjectId].ReceiveAction(null);
                return;
            }
            
            float bestScore = float.NegativeInfinity;
            IActionConfig bestActionConfig = null;

            foreach (var candidate in candidates)
            {
                if (candidate.Preconditions.Any(p => p.Evaluate(ctx.Agent) == 0f)) 
                    continue;

                float utility = ComputeUtility(ctx, candidate);

                if (currentAction?.Config?.ActionType == candidate.ActionConfig.ActionType)
                {
                    utility += hysteresisBias;
                }

                if (utility > bestScore)
                {
                    bestScore = utility;
                    bestActionConfig = candidate.ActionConfig;
                }
            }

            if (bestActionConfig == null)
            {
                _idToAgent[gameObjectId].ReceiveAction(null);
                return;
            }
            
            ActionInstance chosenAction =  new()
            {
                Action = _actions[bestActionConfig.ActionType],
                Config = bestActionConfig,
                State = Activator.CreateInstance(bestActionConfig.StateType) as IActionState
            };
            
            // This is technically not needed, but it provides a unified interface for both the sequential and parallel
            // implementation
            _idToAgent[gameObjectId].ReceiveAction(chosenAction);
        }

        private static float ComputeUtility(ActionContext ctx, ActionCandidate candidate)
        {
            float utility = 1f;
            foreach (var c in candidate.Considerations)
            {
                utility *= c.Evaluate(ctx.Agent);
            }

            // geometric mean to normalize utility
            utility = (float)Math.Pow(utility, 1.0 / candidate.Considerations.Count);
                
            // score compensation to prevent utility from lowering the more considerations there are
            float compensationFactor = 1f - (1f / candidate.Considerations.Count);
            utility += (1f - utility) * compensationFactor;
            return utility;
        }
    }
}