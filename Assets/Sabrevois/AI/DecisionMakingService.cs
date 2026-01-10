using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sabrevois.AI.Actions;

namespace Sabrevois.AI
{
    public class DecisionMakingService
    {
        private readonly Dictionary<Type, IAction> _actions;

        public DecisionMakingService(IEnumerable<IAction> actions)
        {
            _actions = actions.ToDictionary(a => a.GetType(), a => a);
        }
        
        [CanBeNull]
        public ActionInstance ChooseAction(ActionCandidate[] candidates, ActionContext ctx, ActionInstance currentAction, float hysteresisBias = 0.1f)
        {
            if (candidates.Length == 0)
                return null;
            
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
                return null;
            
            return new ActionInstance
            {
                Action = _actions[bestActionConfig.ActionType],
                Config = bestActionConfig,
                State = Activator.CreateInstance(bestActionConfig.StateType) as IActionState
            };
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