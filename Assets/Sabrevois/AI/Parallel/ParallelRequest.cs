using System;
using Sabrevois.AI.Actions;

namespace Sabrevois.AI.Parallel
{
    public record ParallelRequest(int GameObjectId, ActionCandidate[] Candidates, ActionContext Context, Type CurrentActionType, float Hysteresis);
}