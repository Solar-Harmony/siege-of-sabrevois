using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using JetBrains.Annotations;
using Sabrevois.AI.Actions;
using Sabrevois.AI.DataSources;
using Sabrevois.AI.Parallel;
using Zenject;
using Debug = UnityEngine.Debug;

namespace Sabrevois.AI
{
    public partial class ParallelDecisionMakingService : IDecisionMakingService, ITickable, IDisposable
    {
        private float _averageChoosingTimeAccumulator = 0;
        private int _nbChoicesTaken = 0;
        private Stopwatch _startTime = new();
        private int currentMetricResetDelay = 10;

        private readonly Dictionary<Type, IAction> _actions;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Dictionary<int, Agent> _idToAgent = new();
        private readonly ConcurrentQueue<ParallelRequest> _requests = new();
        private readonly SemaphoreSlim _requestSemaphore = new(0);
        private readonly ConcurrentQueue<ParallelResponse> _responses = new();
        private static readonly ConcurrentDictionary<Type, Func<IActionState>> _stateFactories = new();
        private bool _started = false;

#if UNITY_EDITOR
        partial void EditorInitThreadState(Thread thread);
        partial void EditorBeginRequest(int threadId, ParallelRequest request, ref object reqInfoObj);
        partial void EditorEndRequest(object reqInfoObj, ActionInstance chosenAction, Stopwatch sw);
#endif

        [Inject]
        private AgentWorldService _agentWorldService;

        #region Main thread
        public void Dispose()
        {
            Stop();
        }
        
        public ParallelDecisionMakingService(IEnumerable<IAction> actions)
        {
            _actions = actions.ToDictionary(a => a.GetType(), a => a);
        }
        
        public float GetAverageChoosingTime()
        {
            return _averageChoosingTimeAccumulator / _nbChoicesTaken;
        }
        
        public float GetAverageThroughput()
        {
            float ellapsed = _startTime.ElapsedMilliseconds / 1000f;
            return _nbChoicesTaken / ellapsed;
        }
        
        private IActionState InstantiateState(Type stateType)
        {
            if (stateType == null) 
                return null;

            var factory = _stateFactories.GetOrAdd(stateType, type =>
            {
                var newExpr = Expression.New(type);
                var castExpr = Expression.Convert(newExpr, typeof(IActionState));
                var lambda = Expression.Lambda<Func<IActionState>>(castExpr);
                return lambda.Compile();
            });

            return factory();
        }

        public void Start()
        {
            int threadCount = Math.Max(1, Environment.ProcessorCount - 1);
            for (int i = 0; i < threadCount; i++)
            {
                Thread thread = new Thread(WorkerThreadLoop) {
                    IsBackground = true,
                    Name = $"AI Worker Default {i}"
                };
#if UNITY_EDITOR
                EditorInitThreadState(thread);
#endif
                thread.Start();
            }
            _startTime.Start();
            _started = true;
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _started = false;
        }
        
        public void ChooseAction(ActionCandidate[] candidates, ActionContext ctx, ActionInstance currentAction, float hysteresisBias = 0.1f)
        {
            if (!_started)
                Start();
            
            Stopwatch stopwatch = new();
            stopwatch.Start();
            
            int gameObjectId = ctx.Agent.GetInstanceID();
            if (!_idToAgent.ContainsKey(gameObjectId))
                _idToAgent[gameObjectId] = ctx.Agent.GetComponent<Agent>();

            _requests.Enqueue(new ParallelRequest(
                gameObjectId,
                _idToAgent[gameObjectId].Name,
                candidates,
                _agentWorldService.RequestDataSnapshot(_idToAgent[gameObjectId]),
                currentAction?.Config?.ActionType,
                hysteresisBias,
                stopwatch
                ));
            
            _requestSemaphore.Release();
        }
        
        public void Tick()
        {
            while (_responses.TryDequeue(out var response))
            {
                if (_idToAgent.TryGetValue(response.GameObjectId, out var agent))
                {
                    if (_startTime.Elapsed.Seconds > currentMetricResetDelay)
                    {
                        currentMetricResetDelay *= 10;
                        _startTime.Restart();
                        _averageChoosingTimeAccumulator = 0;
                        _nbChoicesTaken = 0;
                    }
                    _nbChoicesTaken++;
                    long ticks = response.stopwatch.ElapsedTicks;
                    long frequency = Stopwatch.Frequency;
                    double elapsedNanoseconds = (double)ticks / frequency * 1_000_000_000;
                    _averageChoosingTimeAccumulator += (float)(elapsedNanoseconds / 1000000000d);
                    agent.ReceiveAction(response.ChosenAction);
                }
                else
                    Debug.LogWarning($"Received response for unknown GameObjectId {response.GameObjectId}");
            }
        }
        #endregion

        #region Worker threads
        private void WorkerThreadLoop()
        {
            while (true)
            {
                try
                {
                    _requestSemaphore.Wait(_cancellationTokenSource.Token);

                    if (!_requests.TryDequeue(out ParallelRequest request))
                        continue;

                    request.stopwatch.Restart();

#if UNITY_EDITOR
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    object reqInfoObj = null;
                    EditorBeginRequest(threadId, request, ref reqInfoObj);
                    Stopwatch sw = Stopwatch.StartNew();
#endif

                    ActionInstance chosenAction = ThreadChooseAction(
                        request.Candidates,
                        request.Context,
                        request.CurrentActionType,
                        request.Hysteresis);

#if UNITY_EDITOR
                    sw.Stop();
                    EditorEndRequest(reqInfoObj, chosenAction, sw);
#endif
                    request.stopwatch.Stop();

                    _responses.Enqueue(new ParallelResponse(request.GameObjectId, chosenAction, request.stopwatch));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
        
        [CanBeNull]
        public ActionInstance ThreadChooseAction(ActionCandidate[] candidates, AgentWorldSnapshot ctx, Type actionType, float hysteresisBias = 0.1f)
        {
            // Respect the challenges of parallelism!!! Maybe
            
            // Since an agent will never request another action before getting a reply for
            // the one it already requested, we are sure only one worker thread AND the
            // main thread may touch the stuff in the ParallelRequest at one time. We also know
            // The actions for an archetype are set in stone and the current action for the
            // said Agent is too until we give it a new one. We therefore conclude that
            // we only need to be careful around the game object. I think hahahhaa
            
            // Due to the small size of our brains, we weren't able to come up with
            // a complex enough logic so we will artificially inflate the algorithmic complexity
            // Random random = new Random();
            // Thread.Sleep(random.Next(50, 500));
            
            if (candidates.Length == 0)
                return null;
                
            float bestScore = float.NegativeInfinity;
            IActionConfig bestActionConfig = null;

            foreach (var candidate in candidates)
            {
                // Manage concurrent access correctly
                if (candidate.Preconditions.Any(p => p.Evaluate(ctx.GetData(p.Source)) == 0f)) 
                    continue;

                float utility = ComputeUtility(ctx, candidate);

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
                State = InstantiateState(bestActionConfig.StateType)
            };
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
        #endregion

    }
}