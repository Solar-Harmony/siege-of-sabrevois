using Sabrevois.AI.Actions;

namespace Sabrevois.AI.Parallel
{
    public record ParallelResponse(int GameObjectId, ActionInstance ChosenAction);
}