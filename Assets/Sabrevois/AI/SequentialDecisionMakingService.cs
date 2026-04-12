using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Sabrevois.AI.Actions;
using Sabrevois.AI.DataSources;
using Zenject;

namespace Sabrevois.AI
{
    public class SequentialDecisionMakingService : IDecisionMakingService
    {
        private float _averageChoosingTimeAccumulator = 0;
        private int _nbChoicesTaken = 0;
        private Stopwatch _startTime = new();
        
        private readonly Dictionary<Type, IAction> _actions;
        private Dictionary<int, Agent> _idToAgent = new();
        
        [Inject]
        private AgentWorldService _agentWorldService;

        public SequentialDecisionMakingService(IEnumerable<IAction> actions)
        {
            _actions = actions.ToDictionary(a => a.GetType(), a => a);
            _startTime.Start();
        }

        public float GetAverageChoosingTime()
        {
            return _averageChoosingTimeAccumulator / _nbChoicesTaken;
        }
        
        public float GetAverageThroughput()
        {
            return _nbChoicesTaken / (_startTime.ElapsedMilliseconds / 1000f);
        }

        [CanBeNull]
        public void ChooseAction(ActionCandidate[] candidates, ActionContext ctx, ActionInstance currentAction, float hysteresisBias = 0.1f)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            
            int gameObjectId = ctx.Agent.GetInstanceID();
            if (!_idToAgent.ContainsKey(gameObjectId))
                _idToAgent[gameObjectId] = ctx.Agent.GetComponent<Agent>();
            AgentWorldSnapshot snap = _agentWorldService.RequestDataSnapshot(_idToAgent[gameObjectId]);
            
            if (candidates.Length == 0)
            {
                _idToAgent[gameObjectId].ReceiveAction(null);
                return;
            }
            
            float bestScore = float.NegativeInfinity;
            IActionConfig bestActionConfig = null;

            foreach (var candidate in candidates)
            {
                if (candidate.Preconditions.Any(p => p.Evaluate(snap.GetData(p.Source)) == 0f)) 
                    continue;

                float utility = ComputeUtility(snap, candidate);

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

            stopwatch.Stop();
            _averageChoosingTimeAccumulator += stopwatch.Elapsed.Milliseconds / 1000f;
            _nbChoicesTaken++;
            // This is technically not needed, but it provides a unified interface for both the sequential and parallel
            // implementation
            _idToAgent[gameObjectId].ReceiveAction(chosenAction);
        }

        private static float ComputeUtility(AgentWorldSnapshot ctx, ActionCandidate candidate)
        {
            float utility = 1f;
            foreach (var c in candidate.Considerations)
            {
                utility *= c.Evaluate(ctx.GetData(c.Source));
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