using System;
using System.Collections.Generic;
using System.Linq;
using Sabrevois.AI.Actions;
using VContainer;

namespace Sabrevois.AI
{
    public class DecisionMakingService
    {
        [Inject] 
        private Dictionary<Type, IAction> _actions;
        
        public record ActionChoice(IAction Action, IActionConfig Config);
        
        /// <summary>
        /// Chooses the best action from the candidates.
        /// It evaluates the utility for each action using its considerations and picks the action with the highest score.
        /// If multiple actions have the same score, the first one encountered is chosen, so the order of candidates matters.
        /// </summary>
        /// <param name="candidates">List of actions with considerations.</param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public ActionChoice ChooseAction(ActionCandidate[] candidates, ActionContext ctx)
        {
            if (candidates.Length == 0)
                return null;
            
            float bestScore = float.NegativeInfinity;
            ActionCandidate bestCandidate = null;

            foreach (var action in candidates)
            {
                bool ignoreAction = action.Preconditions.Any(p => p.Evaluate(ctx.Agent) == 0f);
                if (ignoreAction) 
                    continue;

                float utility = 1f;
                foreach (var c in action.Considerations)
                {
                    utility *= c.Evaluate(ctx.Agent);
                }

                if (utility > bestScore)
                {
                    bestScore = utility;
                    bestCandidate = action;
                }
            }

            if (bestCandidate == null)
                return null;

            return new ActionChoice(_actions[bestCandidate.ActionConfig.ActionType], bestCandidate.ActionConfig);
        }
    }
}