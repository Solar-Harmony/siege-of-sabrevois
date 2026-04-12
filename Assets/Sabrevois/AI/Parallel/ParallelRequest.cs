using System;
using System.Diagnostics;
using Sabrevois.AI.Actions;
using Sabrevois.AI.DataSources;

namespace Sabrevois.AI.Parallel
{
    public record ParallelRequest(int GameObjectId, string AgentName, ActionCandidate[] Candidates, AgentWorldSnapshot Context, Type CurrentActionType, float Hysteresis, Stopwatch stopwatc);
}