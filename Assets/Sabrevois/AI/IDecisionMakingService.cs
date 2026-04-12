using Sabrevois.AI.Actions;

namespace Sabrevois.AI
{
    public interface IDecisionMakingService
    {
        void ChooseAction(ActionCandidate[] candidates, ActionContext ctx, ActionInstance currentAction,
            float hysteresisBias = 0.1f);
    }
}