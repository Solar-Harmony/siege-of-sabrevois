using System.Linq;
using Sabrevois.AI.Actions;

namespace Sabrevois.AI
{
    public class DecisionMakingService
    {
        public IAction ChooseAction(ActionCandidate[] candidates, ActionContext ctx)
        {
            if (candidates.Length == 0)
                return null;
            
            float bestScore = float.NegativeInfinity;
            IAction bestAction = null;

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
                    bestAction = candidate.Action;
                }
            }

            return bestAction;
        }
    }
}