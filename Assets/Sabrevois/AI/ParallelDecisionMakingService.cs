using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Sabrevois.AI.Actions;
using Sabrevois.AI.Parallel;
using UnityEngine;
using UnityEngine.PlayerLoop;
using Zenject;

namespace Sabrevois.AI
{
    public class ParallelDecisionMakingService : IDecisionMakingService, ITickable
    {
        private readonly Dictionary<Type, IAction> _actions;
        private CancellationTokenSource _cancellationTokenSource = new();

        private Dictionary<int, Agent> _idToAgent = new();
        private ConcurrentQueue<ParallelRequest> _requests = new();
        private ConcurrentQueue<ParallelResponse> _responses = new();
        private bool _started = false;

        #region Main thread
        public ParallelDecisionMakingService(IEnumerable<IAction> actions)
        {
            _actions = actions.ToDictionary(a => a.GetType(), a => a);
        }

        public void Start()
        {
            // Starting all the worker threads (One for each core - 1 to avoid starving the main thread)
            for (int i = 0; i < System.Environment.ProcessorCount - 1; i++)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    WorkerThreadLoop();
                });
            }
            _started = true;
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _started = false;
        }
        
        [CanBeNull]
        public void ChooseAction(ActionCandidate[] candidates, ActionContext ctx, ActionInstance currentAction, float hysteresisBias = 0.1f)
        {
            if (!_started)
                Start();
            
            int gameObjectId = ctx.Agent.GetInstanceID();
            if (!_idToAgent.ContainsKey(gameObjectId))
                _idToAgent[gameObjectId] = ctx.Agent.GetComponent<Agent>();
            
            _requests.Enqueue(new ParallelRequest(gameObjectId, candidates, ctx, currentAction?.Config?.ActionType, hysteresisBias)); 
        }
        
        public void Tick()
        {
            Update();
        }

        public void Update()
        {
            while (_responses.TryDequeue(out var response))
            {
                if (_idToAgent.TryGetValue(response.GameObjectId, out var agent))
                    agent.ReceiveAction(response.ChosenAction);
                else
                    Debug.LogWarning($"Received response for unknown GameObjectId {response.GameObjectId}");
            }
        }
        #endregion

        #region Worker threads
        private void WorkerThreadLoop()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!_requests.TryDequeue(out ParallelRequest request))
                    continue;
                
                ActionInstance chosenAction = ThreadChooseAction(request.Candidates, request.Context,
                    request.CurrentActionType, request.Hysteresis);
                
                _responses.Enqueue(new ParallelResponse(request.GameObjectId, chosenAction));
            }
        }
        
        
        [CanBeNull]
        public ActionInstance ThreadChooseAction(ActionCandidate[] candidates, ActionContext ctx, Type actionType, float hysteresisBias = 0.1f)
        {
            // Respect the challenges of parallelism!!! Maybe
            
            // Since an agent will never request another action before getting a reply for
            // the one it already requested, we are sure only one worker thread AND the
            // main thread may touch the stuff in the ParallelRequest at one time. We also know
            // The actions for an archetype are set in stone and the current action for the
            // said Agent is too until we give it a new one. We therefore conclude that
            // we only need to be careful around the game object. I think hahahhaa
            
            if (candidates.Length == 0)
                return null;
                
            float bestScore = float.NegativeInfinity;
            IActionConfig bestActionConfig = null;

            foreach (var candidate in candidates)
            {
                float utility = 1f;
                
                // Manage concurrent access correctly
                if (candidate.Preconditions.Any(p => p.Evaluate(ctx.Agent) == 0f)) 
                    continue;
                foreach (var c in candidate.Considerations)
                {
                    utility *= c.Evaluate(ctx.Agent);
                }

                // geometric mean to normalize utility
                utility = (float)Math.Pow(utility, 1.0 / candidate.Considerations.Count);
                
                // score compensation to prevent utility from lowering the more considerations there are
                float compensationFactor = 1f - (1f / candidate.Considerations.Count);
                utility += (1f - utility) * compensationFactor;

                if (actionType == candidate.ActionConfig.ActionType)
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
        #endregion

    }
}