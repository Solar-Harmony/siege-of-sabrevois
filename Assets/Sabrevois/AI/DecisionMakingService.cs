using System;
using System.Collections.Generic;
using System.Linq;
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
        
        public (IAction, IActionConfig) ChooseAction(ActionCandidate[] candidates, ActionContext ctx)
        {
            if (candidates.Length == 0)
                return (null, null);
            
            float bestScore = float.NegativeInfinity;
            IActionConfig bestActionConfig = null;

            foreach (var candidate in candidates)
            {
                if (candidate.Preconditions.Any(p => p.Evaluate(ctx.Agent) == 0f)) 
                    continue;

                float utility = 1f;
                foreach (var c in candidate.Considerations)
                {
                    utility *= c.Evaluate(ctx.Agent);
                }

                if (utility > bestScore)
                {
                    bestScore = utility;
                    bestActionConfig = candidate.ActionConfig;
                }
            }

            if (bestActionConfig == null)
                return (null, null);
            
            return (_actions[bestActionConfig.ActionType], bestActionConfig);
        }
    }
}