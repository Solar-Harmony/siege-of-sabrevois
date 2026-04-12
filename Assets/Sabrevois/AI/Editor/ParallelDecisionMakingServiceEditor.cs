using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Sabrevois.AI.Parallel;
using Sabrevois.AI.Actions;

#if UNITY_EDITOR
namespace Sabrevois.AI
{
    public partial class ParallelDecisionMakingService
    {
        public class ThreadState
        {
            public int ThreadId;
            public string ThreadName;
            public ConcurrentQueue<RequestInfo> History = new();
        }

        public class RequestInfo
        {
            public int GameObjectId;
            public string AgentName;
            public string Status;
            public float TimeElapsedMs;
            public double TimeElapsedNs;
            public string ChosenAction;
            public DateTime Timestamp;
        }

        public readonly ConcurrentDictionary<int, ThreadState> EditorThreadStates = new();
        public ConcurrentQueue<ParallelRequest> EditorRequests => _requests;
        public ConcurrentQueue<ParallelResponse> EditorResponses => _responses;

        partial void EditorInitThreadState(Thread thread)
        {
            EditorThreadStates[thread.ManagedThreadId] = new ThreadState { ThreadId = thread.ManagedThreadId, ThreadName = thread.Name };
        }

        partial void EditorBeginRequest(int threadId, ParallelRequest request, ref object reqInfoObj)
        {
            var reqInfo = new RequestInfo
            {
                GameObjectId = request.GameObjectId,
                AgentName = request.AgentName,
                Status = "In Progress",
                TimeElapsedMs = 0f,
                Timestamp = DateTime.Now
            };
            reqInfoObj = reqInfo;

            if (EditorThreadStates.TryGetValue(threadId, out var ts))
            {
                ts.History.Enqueue(reqInfo);
                if (ts.History.Count > 50)
                    ts.History.TryDequeue(out _);
            }
        }

        partial void EditorEndRequest(object reqInfoObj, ActionInstance chosenAction, Stopwatch sw)
        {
            if (reqInfoObj is RequestInfo reqInfo)
            {
                reqInfo.Status = "Done";
                reqInfo.TimeElapsedMs = (float)sw.Elapsed.TotalMilliseconds;
                reqInfo.TimeElapsedNs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000_000_000;
                reqInfo.ChosenAction = chosenAction?.Action?.GetType().Name ?? "None";
            }
        }
    }
}
#endif

